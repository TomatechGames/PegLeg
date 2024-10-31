using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

public static class ProfileRequests
{
    static readonly Dictionary<string, JsonObject> profileCache = new();

    public static async Task<int> GetSumOfProfileItems(string profileID, string type) =>
        (await GetProfileItems(profileID, item => item.Value["templateId"].ToString().StartsWith(type))).Select(kvp => kvp.Value["quantity"].GetValue<int>()).Sum();

    public static async Task<JsonObject> GetProfileItems(string profileID, string type) =>
        await GetProfileItems(profileID, item => item.Value["templateId"].ToString().StartsWith(type));

    public delegate bool ProfileItemPredicate(KeyValuePair<string, JsonObject> kvp);
    public static async Task<JsonObject> GetProfileItems(string profileID, ProfileItemPredicate predicate)
    {
        var matchedItems = 
            (await GetProfile(profileID))
            ["profileChanges"][0]["profile"]["items"]
            .AsObject()
            .Select(kvp=>KeyValuePair.Create(kvp.Key,kvp.Value.AsObject()))
            .Where(kvp=>predicate(kvp));
        JsonObject result = new();
        foreach (var item in matchedItems)
            result[item.Key] = JsonNode.Parse(item.Value.ToString());
        return result;
    }

    public static async Task<bool> ProfileItemExists(string profileID, ProfileItemPredicate predicate)
    {
        if (!profileCache.ContainsKey(profileID))
            await GetProfile(profileID);
       return ProfileItemExistsUnsafe(profileID, predicate);
    }

    public static bool ProfileItemExistsUnsafe(string profileID, ProfileItemPredicate predicate)
    {
        return profileCache[profileID]
             ["profileChanges"][0]["profile"]["items"]
             .AsObject()
             .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.AsObject()))
             .Any(kvp => predicate(kvp));
    }

    public static async Task<JsonObject> GetFirstProfileItem(string profileID, ProfileItemPredicate predicate)
    {
        if (!profileCache.ContainsKey(profileID))
            await GetProfile(profileID);
        return GetFirstProfileItemUnsafe(profileID, predicate);
    }
    public static JsonObject GetFirstProfileItemUnsafe(string profileID, ProfileItemPredicate predicate)
    {
        var matchedItem =
            profileCache[profileID]
            ["profileChanges"][0]["profile"]["items"]
            .AsObject()
            .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.AsObject()))
            .Where(kvp => predicate(kvp))
            .FirstOrDefault();
        var result = (matchedItem.Key == default) ? null : matchedItem.Value.Reserialise();
        if (result is not null)
            result["uuid"] = matchedItem.Key;
        return result;
    }

    public static JsonObject GetCachedProfileItems(string profileID, ProfileItemPredicate predicate)
    {
        var matchedItems =
            profileCache[profileID]
            ["profileChanges"][0]["profile"]["items"]
            .AsObject()
            .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.AsObject()))
            .Where(kvp => predicate(kvp));
        JsonObject result = new();
        foreach (var item in matchedItems)
            result[item.Key] = JsonNode.Parse(item.Value.ToString());
        return result;
    }

    public static JsonObject GetCachedProfileItemInstance(ProfileItemId profileItem) =>
        profileCache[profileItem.profile]["profileChanges"][0]["profile"]["items"][profileItem.uuid]?.AsObject();

    public static async Task<JsonObject> GetProfileItemInstance(ProfileItemId profileItem) =>
        (await GetProfile(profileItem.profile))["profileChanges"][0]["profile"]["items"][profileItem.uuid].AsObject();

    public static async Task<int> GetProfileItemsCount(string profileID)
    {
        return (await GetProfile(profileID))["profileChanges"][0]["profile"]["items"].AsObject().Count;
    }

    public static async Task<JsonObject[]> GetAllPrerollDatas()
    {
        var items = (await GetProfile(FnProfiles.AccountItems))["profileChanges"][0]["profile"]["items"].AsObject();
        var preroll = items.FirstOrDefault(kvp => kvp.Value["templateId"].ToString().StartsWith("PrerollData")).Value;
        if(preroll is null)
        {
            var expireTime = DateTime.Parse(preroll["expiration"].ToString(), null, DateTimeStyles.RoundtripKind);
            //check if prerolls arent outdated yet
            if (DateTime.UtcNow.CompareTo(expireTime) >= 0)
            {
                await PerformProfileOperation(FnProfiles.AccountItems, "PopulatePrerolledOffers");
                items = (await GetProfile(FnProfiles.AccountItems))["profileChanges"][0]["profile"]["items"].AsObject();
            }
        }
        else
        {
            await PerformProfileOperation(FnProfiles.AccountItems, "PopulatePrerolledOffers");
            items = (await GetProfile(FnProfiles.AccountItems))["profileChanges"][0]["profile"]["items"].AsObject();
        }

        return items
            .Where(kvp => kvp.Value["templateId"].ToString().StartsWith("PrerollData"))
            .Select(kvp => kvp.Value.AsObject())
            .ToArray();
    }

    public static float GetSurvivorBonusUnsafe(string bonusID, int perSquadRequirement = 2, float boostBase = 5)
    {
        var matchingSurvivors = GetCachedProfileItems(FnProfiles.AccountItems, kvp =>
        {
            if (!kvp.Value["templateId"].ToString().StartsWith("Worker"))
                return false;
            if (kvp.Value["attributes"]["squad_id"] is null || kvp.Value["attributes"]["set_bonus"] is null)
                return false;
            var thisBonus = kvp.Value["attributes"]["set_bonus"].ToString().Split(".")[^1];
            return thisBonus == bonusID;
        })
        .GroupBy(kvp=> kvp.Value["attributes"]["squad_id"].ToString());

        int boostMatchCount = matchingSurvivors.Select(g => g.Count() / perSquadRequirement).Sum();

        return boostBase * boostMatchCount;
    }

    public enum OrderRange
    {
        Daily,
        Weekly,
        Monthly
    }

    static string OrderRangeToAttribute(OrderRange range) => range switch
    {
        OrderRange.Daily => "daily_purchases",
        OrderRange.Weekly => "weekly_purchases",
        OrderRange.Monthly => "monthly_purchases",
        _ => throw new NotImplementedException(),
    };

    static DateTime OrderRangeToInterval(OrderRange range) => range switch
    {
        OrderRange.Daily => RefreshTimerController.GetRefreshTime(RefreshTimeType.Daily).AddDays(-1),
        OrderRange.Weekly => RefreshTimerController.GetRefreshTime(RefreshTimeType.Weekly).AddDays(-7),
        _ => throw new NotImplementedException(),
    };

    public static async Task<JsonObject> GetOrderCounts(OrderRange range)
    {
        var orderRange = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"][OrderRangeToAttribute(range)];
        var lastIntervalTime = DateTime.Parse(orderRange["lastInterval"].ToString(), null, DateTimeStyles.RoundtripKind);
        if (lastIntervalTime != OrderRangeToInterval(range))
            return null;
        return orderRange["purchaseList"].AsObject();
    }
    public static async Task<JsonObject> GetFufillmentCounts()
    {
        var attributes = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        return attributes["in_app_purchases"]["fufillmentCounts"].AsObject();
    }

    public static async Task<string> GetSACCode(bool addExpiredText = true)
    {
        var attributes = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        var lastSetTime = DateTime.Parse(attributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        bool isExpired = (DateTime.UtcNow - lastSetTime).Days > 13;
        return attributes["mtx_affiliate"] + (isExpired && addExpiredText ? " (Expired)" : "");
    }

    public static async Task<bool> IsSACExpired() => Mathf.FloorToInt(await GetSACTime()) > 13;

    public static async Task<double> GetSACTime()
    {
        var attributes = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        var lastSetTime = DateTime.Parse(attributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        return (DateTime.UtcNow - lastSetTime).TotalDays;
    }

    public static async Task<bool> SetSACCode(string newName)
    {
        await PerformProfileOperation(FnProfiles.Common, "SetAffiliateName", "{\"affiliateName\":\"" + newName + "\"}");
        //TODO: return false if creator code not found
        return true;
    }
    //public static async Task<List<string>> GetPinnedQuests()
    //{
    //    return (await GetProfile(FnProfiles.AccountItems))
    //        ["profileChanges"]["profile"]["stats"]["attributes"]["client_settings"]["pinnedQuestInstances"]
    //        .AsArray()
    //        .Select(q=>q.ToString())
    //        .ToList();
    //}
    //public static List<string> GetPinnedQuestsUnsafe()
    //{
    //    return profileCache[FnProfiles.AccountItems]
    //        ["profileChanges"][0]["profile"]["stats"]["attributes"]["client_settings"]["pinnedQuestInstances"]
    //        .AsArray()
    //        .Select(q => q.ToString())
    //        .ToList();
    //}

    static List<string> localPinnedQuests;
    static DateTime questsLastRefreshedAt = DateTime.MinValue;
    static async Task CheckLocalPinnedQuests()
    {
        bool outOfDate = (questsLastRefreshedAt - DateTime.UtcNow).TotalMinutes > 5;
        if (localPinnedQuests != null && !outOfDate)
            return;
        
        localPinnedQuests = (await GetProfile(FnProfiles.AccountItems, true))
            ["profileChanges"][0]["profile"]["stats"]["attributes"]["client_settings"]["pinnedQuestInstances"]
            .AsArray()
            .Select(q => q.ToString())
            .ToList();
    }

    public static async Task AddPinnedQuest(string uuid)
    {
        await CheckLocalPinnedQuests();
        if (!localPinnedQuests.Contains(uuid))
        {
            localPinnedQuests.Add(uuid);
            OnItemUpdated?.Invoke(new(FnProfiles.AccountItems, uuid));
            SendLocalPinnedQuests();
        }
    }

    public static async Task RemovePinnedQuest(string uuid)
    {
        await CheckLocalPinnedQuests();
        if (localPinnedQuests.Contains(uuid))
        {
            localPinnedQuests.Remove(uuid);
            OnItemUpdated?.Invoke(new(FnProfiles.AccountItems, uuid));
            SendLocalPinnedQuests();
        }
    }

    public static void ClearPinnedQuests()
    {
        GD.Print("clearing all pinned");
        var unpinnedQuests = localPinnedQuests?.ToArray() ?? Array.Empty<string>();
        localPinnedQuests?.Clear();
        foreach (var uuid in unpinnedQuests)
        {
            OnItemUpdated?.Invoke(new(FnProfiles.AccountItems, uuid));
        }
        SendLocalPinnedQuests();
    }

    static async void SendLocalPinnedQuests()
    {
        localPinnedQuests ??= new();
        JsonObject content = new()
        {
            ["pinnedQuestIds"] = new JsonArray(localPinnedQuests.Select(q => (JsonValue)q).ToArray())
        };
        await PerformProfileOperation(FnProfiles.AccountItems, "SetPinnedQuests", content.ToString());
    }

    public static bool HasPinnedQuest(string uuid)
    {
        localPinnedQuests ??= new();
        return localPinnedQuests.Contains(uuid);
    }

    public static async Task<ProfileItemHandle> RerollQuest(string uuid)
    {
        GD.Print("rerolling quest " + uuid);
        JsonObject content = new()
        {
            ["questId"] = uuid
        };
        var notif = 
            (await PerformProfileOperation(FnProfiles.AccountItems, "FortRerollDailyQuest", content.ToString()))
            ["notifications"]?
            .AsArray()
            .FirstOrDefault(n => n["type"].ToString()== "dailyQuestReroll");
        if (notif is null)
            return null;
        var newUUID = GetFirstProfileItemUnsafe(FnProfiles.AccountItems, (kvp) => kvp.Value["templateId"].ToString() == notif["newQuestId"].ToString())["uuid"].ToString();
        return ProfileItemHandle.CreateHandleUnsafe(new(FnProfiles.AccountItems, newUUID));
    }

    public static bool CanRerollQuestUnsafe()
    {
        return profileCache[FnProfiles.AccountItems]
            ["profileChanges"][0]["profile"]["stats"]["attributes"]["quest_manager"]["dailyQuestRerolls"].GetValue<int>() > 0;
    }

    static readonly List<string> collectableTypess = new()
    {
        "Hero",
        "Worker",
        "Defender",
        "Schematic"
    };

    struct RewardNotificationCheck
    {
        public string typeAndName { get; init; }
        public int rarity { get; init; }

        public RewardNotificationCheck(string typeAndName, int rarity)
        {
            this.typeAndName = typeAndName;
            this.rarity = rarity;
        }

        public bool IsMatch(RewardNotificationCheck itemToCheck)
        {
            //if(itemToCheck.typeAndName == typeAndName)
            //{
            //    GD.Print($"partial match \"{typeAndName}\":({rarity}/{itemToCheck.rarity})");
            //}
            return itemToCheck.typeAndName == typeAndName && rarity >= itemToCheck.rarity;
        }
    }
    static readonly Dictionary<string, List<RewardNotificationCheck>> rewardNotificationChecks = new();

    public static async Task<JsonObject> SetItemRewardNotification(this JsonObject itemData)
    {
        //temporarily disabled until i figure out a workaround to the lag spikes
        //itemData["attributes"] ??= new JsonObject();
        //itemData["attributes"]["item_seen"] = true;
        //return itemData;
        var itemTemplate = itemData.GetTemplate();
        if (itemData["attributes"]?["item_seen"] is not null)
            itemData["attributes"]["item_seen"] = false;

        if (itemTemplate["Type"].ToString() == "Accolades")
        {
            itemData["attributes"] ??= new JsonObject();
            itemData["attributes"]["item_seen"] = true;
            return itemData;
        }
        if (itemTemplate["Type"].ToString() == "CardPack")
        {
            itemData["attributes"] ??= new JsonObject();
            itemData["attributes"]["item_seen"] = true;
            return itemData;
        }
        if (itemTemplate["Name"].ToString() == "Campaign_Event_Currency")
        {
            itemData["attributes"] ??= new JsonObject();
            itemData["attributes"]["item_seen"] = true;
            return itemData;
        }
        if (itemTemplate["Name"].ToString() == "Currency_MtxSwap")
        {
            itemData["attributes"] ??= new JsonObject();
            itemData["attributes"]["item_seen"] = true;
            return itemData;
        }

        bool exists = false;
        var notificationKey = GenerateRewardNotificationKey(itemTemplate);
        if (itemTemplate["Type"].ToString() == "Weapon")
        {
            if (!rewardNotificationChecks.ContainsKey(FnProfiles.Backpack))
                await GetProfile(FnProfiles.Backpack);

            lock (rewardNotificationChecks)
            {
                exists = rewardNotificationChecks[FnProfiles.Backpack]?.Any(c => c.IsMatch(notificationKey)) ?? false;
            }
        }
        else
        {
            if (!rewardNotificationChecks.ContainsKey(FnProfiles.AccountItems))
                await GetProfile(FnProfiles.AccountItems);
            lock (rewardNotificationChecks)
            {
                exists = rewardNotificationChecks[FnProfiles.AccountItems]?.Any(c => c.IsMatch(notificationKey)) ?? false;
            }
            if (!exists && collectableTypess.Contains(itemTemplate["Type"].ToString()))
            {
                var collectionProfile = itemTemplate["Type"].ToString() == "Schematic" ? FnProfiles.SchematicCollection : FnProfiles.PeopleCollection;

                if (!rewardNotificationChecks.ContainsKey(collectionProfile))
                    await GetProfile(collectionProfile);
                lock (rewardNotificationChecks)
                {
                    exists = rewardNotificationChecks[collectionProfile]?.Any(c => c.IsMatch(notificationKey)) ?? false;
                }
            }
        }
        itemData["attributes"] ??= new JsonObject();
        itemData["attributes"]["item_seen"] = exists;
        return itemData;
    }


    public static bool? IsItemCollectedUnsafe(JsonObject itemInstance)
    {
        string itemId = itemInstance["templateId"].ToString();
        string regexPattern= Regex.Replace(itemId, "_t\\d\\d", "_t\\d\\d");
        string type = itemId.Split(":")[0];

        if (type != "Schematic" && type != "Hero" && type != "Worker" && type != "Defender")
            return null;

        if (type == "Worker" && itemInstance["attributes"] is null)
            return null;

        string profile = type == "Schematic" ? FnProfiles.SchematicCollection : FnProfiles.PeopleCollection;
        if (!profileCache.ContainsKey(profile))
            return null;

        return ProfileItemExistsUnsafe(profile, kvp => 
            Regex.IsMatch(kvp.Value["templateId"].ToString(), regexPattern) &&
            itemInstance["attributes"]["personality"]?.ToString() == kvp.Value["attributes"]["personality"]?.ToString()
        );
    }

    public static async Task<JsonObject> GetProfile(string profileId, bool forceRefresh = false) =>
        await PerformProfileOperation(profileId, "QueryProfile", queryIgnoreCache: forceRefresh);

    static DateTime lastPopulatedLlamasOnDate = DateTime.MinValue;
    public static async Task PopulateLlamas(bool force = false)
    {
        //check if already populated today
        if (!force && DateTime.UtcNow.Date.CompareTo(lastPopulatedLlamasOnDate)>=0)
            return;
        lastPopulatedLlamasOnDate = DateTime.UtcNow.Date;
        await PerformProfileOperation("campaign", "QueryProfile");
    }

    static SemaphoreSlim profileOperationSephamore = new(1);
    public static event Action<string, JsonObject> OnNotification;
    public static async Task<JsonObject> PerformProfileOperation(string profileId, string operation, string content = "{}", bool queryIgnoreCache = false)
    {
        if (PerformProfileOperationWithCache(profileId, operation, content, queryIgnoreCache) is JsonObject cacheResult)
            return cacheResult;
        await profileOperationSephamore.WaitAsync();
        try
        {
            return PerformProfileOperationWithCache(profileId, operation, content, queryIgnoreCache) ?? await PerformProfileOperationUnsafe(profileId, operation, content);
        }
        finally
        {
            profileOperationSephamore.Release();
        }
    }

    static JsonObject PerformProfileOperationWithCache(string profileId, string operation, string content = "{}", bool queryIgnoreCache = false)
    {
        if (!profileCache.ContainsKey(profileId))
            return null;

        if (operation == "QueryProfile" && !queryIgnoreCache)
        {
            return profileCache[profileId];
        }
        if (operation == "MarkItemSeen")
        {
            var targetItems = JsonNode.Parse(content)["itemIds"].AsArray();
            var targetProfile = profileCache[profileId];
            foreach (var itemId in targetItems)
            {
                targetProfile["profileChanges"][0]["profile"]["items"][itemId.ToString()]["attributes"]["item_seen"] = true;
            }
            profileCache[profileId] = targetProfile;
            PerformProfileOperationUnsafe(profileId, operation, content).RunSafely();
            return profileCache[profileId];
        }
        return null;
    }

    public static async Task<JsonObject> PerformProfileOperationUnsafe(string profileId, string operation, string content = "{}")
    {
        if (!await LoginRequests.TryLogin())
        {
            GD.Print("profile request failed: not logged in");
            if (profileCache.ContainsKey(profileId))
                return profileCache[profileId];
            return null;
        }

        bool publicMode = (operation == "QueryPublicProfile");
        string route = publicMode ? "public" : "client";

        StringContent jsonContent = new(content, Encoding.UTF8, "application/json");
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"fortnite/api/game/v2/profile/{LoginRequests.AccountID}/{route}/{operation}?profileId={profileId}&rvn=-1"
            )
            {
                Content = jsonContent
            };

        request.Headers.Authorization = LoginRequests.AccountAuthHeader;

        var result = (await Helpers.MakeRequest(FNEndpoints.gameEndpoint, request)).AsObject();
        if (result.ContainsKey("errorCode"))
        {
            var _ = GenericConfirmationWindow.ShowErrorForWebResult(result);
            if (profileCache.ContainsKey(profileId))
                return profileCache[profileId];
            return null;
        }

        JsonObject changelog = null;
        if (profileCache.ContainsKey(profileId) && profileCache[profileId] is not null)
            changelog = await GenerateChangelog(profileCache[profileId], result);
        //else
            //GD.Print("no cached profile");

        profileCache[profileId] = result;

        if (profileId == FnProfiles.AccountItems)
        {
            localPinnedQuests ??= result["profileChanges"][0]["profile"]["stats"]["attributes"]["client_settings"]["pinnedQuestInstances"]
                .AsArray()
                .Select(q => q.ToString())
                .ToList();
        }
        lock (rewardNotificationChecks)
        {
            if (!rewardNotificationChecks.ContainsKey(profileId))
                rewardNotificationChecks[profileId] = new();
            var checks = GenerateRewardNotificationChecks(result);
            rewardNotificationChecks[profileId] = checks;
            var trimmedChecks = checks.ToArray()[..Mathf.Min(10, checks.Count)];
            //GD.Print($"{profileId}: [{trimmedChecks.Select(n=>n.typeAndName+":"+n.rarity).ToArray().Join(", ")}]");
        }
        if (result.ContainsKey("notifications"))
        {
            var notifications = result["notifications"].AsArray();
            foreach (var item in notifications)
                OnNotification?.Invoke(profileId, item.AsObject());
            //TODO: handle notifications (such as invoking OnItemAdded for llama results)
        }
        JsonArray multiUpdateArray = new();
        if (result.ContainsKey("multiUpdate"))
            multiUpdateArray = result["multiUpdate"].AsArray();
        if (changelog is not null)
            multiUpdateArray.Add(changelog);
        HandleChanges(multiUpdateArray);

        GD.Print("request complete");
        return profileCache[profileId];
    }

    const int operationsPer10ms = 50;
    static async Task<JsonObject> GenerateChangelog(JsonObject oldProfile, JsonObject newProfile)
    {
        var oldItems = oldProfile?["profileChanges"]?[0]?["profile"]?["items"]?.AsObject();
        var newItems = newProfile?["profileChanges"]?[0]?["profile"]?["items"]?.AsObject();

        if(oldItems is null || newItems is null)
            return null;

        var oldKeys = oldItems.Select(x => x.Key).ToArray();
        var newKeys = newItems.Select(x => x.Key).ToArray();

        var addedKeys = newKeys.Except(oldKeys);
        var removedKeys = oldKeys.Except(newKeys);
        var possiblyChangedKeys = oldKeys.Intersect(newKeys);

        JsonArray profileChanges = new();

        int currentOperation = 0;
        foreach (var itemKey in removedKeys)
        {
            profileChanges.Add(new JsonObject()
            {
                ["changeType"] = "itemRemoved",
                ["itemId"] = itemKey
            });
            currentOperation++;
            if(currentOperation%operationsPer10ms==0)
                await Task.Delay(10);
        }
        foreach (var itemKey in possiblyChangedKeys)
        {
            if (oldItems[itemKey].AsObject().ContainsKey("template"))
                newItems[itemKey].AsObject().GetTemplate();
            if (newItems[itemKey].ToString() != oldItems[itemKey].ToString())
            {
                profileChanges.Add(new JsonObject()
                {
                    ["changeType"] = "itemFullyChanged",
                    ["itemId"] = itemKey,
                    ["item"] = newItems[itemKey].Reserialise()
                });
            }
            currentOperation++;
            if (currentOperation % operationsPer10ms == 0)
                await Task.Delay(10);
        }
        foreach (var itemKey in addedKeys)
        {
            GD.Print("prepping addittion: " + itemKey);
            profileChanges.Add(new JsonObject()
            {
                ["changeType"] = "itemAdded",
                ["itemId"] = itemKey,
                ["item"] = newItems[itemKey].Reserialise()
            });
            currentOperation++;
            if (currentOperation % operationsPer10ms == 0)
                await Task.Delay(10);
        }

        return new()
        {
            ["profileId"] = newProfile["profileChanges"][0]["profile"]["profileId"].ToString(),
            ["profileChanges"] = profileChanges
        };
    }

    static List<RewardNotificationCheck> GenerateRewardNotificationChecks(JsonObject newProfile)
    {
        var items = newProfile?["profileChanges"]?[0]?["profile"]?["items"]?.AsObject();
        if (items is null)
            return new();
        return items
            .GroupBy(i => GenerateRewardNotificationKey(i.Value.GetTemplate()))
            .Select(g => g.Key)
            .ToList();
    }

    static RewardNotificationCheck GenerateRewardNotificationKey(JsonObject itemTemplate)
    {
        string type = itemTemplate?["Type"]?.ToString();
        string name = itemTemplate?["Name"]?.ToString();
        string displayName = itemTemplate?["DisplayName"]?.ToString();
        if (name?.StartsWith("eventcurrency") ?? false)
            displayName = "Campaign Event Currency";
        if (name == "currency_xrayllama")
            displayName = "V-Bucks Voucher";

        return new($"{type}_{displayName}", itemTemplate?.GetItemRarity() ?? 0);
    }

    public static event Action<ProfileItemId> OnItemAdded;
    public static event Action<ProfileItemId> OnItemUpdated;
    public static event Action<ProfileItemId> OnItemRemoved;
    static void HandleChanges(JsonArray multiUpdate)
    {
        foreach (var profileUpdate in multiUpdate)
        {
            string profile = profileUpdate["profileId"].ToString();
            foreach (var change in profileUpdate["profileChanges"].AsArray())
            {
                string changeType = change["changeType"].ToString();
                string uuid = change["itemId"]?.ToString();

                if (!profileCache.ContainsKey(profile))
                {
                    GD.Print($"uncached item changed in {profile} {{type:{changeType}, id:{uuid}");
                    continue;
                }
                ProfileItemId profileItem = new(profile, uuid);
                var items = profileCache[profile]["profileChanges"][0]["profile"]["items"].AsObject();
                JsonNode tempClone = null;
                switch (changeType)
                {
                    case "itemAdded":
                        items[uuid] = change["item"].Reserialise();
                        GD.Print($"ADDED: {uuid} ({items[uuid]})");
                        OnItemAdded?.Invoke(profileItem);
                        break;
                    case "itemRemoved":
                        tempClone = items[uuid]?.Reserialise();
                        if (tempClone?["template"] is not null)
                            tempClone["template"] = null;
                        GD.Print($"REMOVED: {uuid} ({tempClone})");
                        items.Remove(uuid);
                        OnItemRemoved?.Invoke(profileItem);
                        break;
                    case "statChanged":
                        break;
                    case "itemQuantityChanged":
                        items[uuid]["quantity"] = change["quantity"].Reserialise();
                        tempClone = items[uuid]?.Reserialise();
                        if (tempClone?["template"] is not null)
                            tempClone["template"] = null;
                        GD.Print($"CHANGED (quantity): {uuid} ({tempClone})");
                        OnItemUpdated?.Invoke(profileItem);
                        break;
                    case "itemAttrChanged":
                        tempClone = items[uuid]?.Reserialise();
                        if (tempClone?["template"] is not null)
                            tempClone["template"] = null;
                        GD.Print($"CHANGED (attribute): {uuid}[{change["attributeName"]}] ({tempClone})");
                        items[uuid][change["attributeName"].ToString()] = change["attributeValue"].Reserialise();
                        OnItemUpdated?.Invoke(profileItem);
                        break;
                    case "itemFullyChanged":
                        //custom one for handling generated changelogs
                        items[uuid] = change["item"].Reserialise();
                        tempClone = items[uuid].Reserialise();
                        if (tempClone?["template"] is not null)
                            tempClone["template"] = null;
                        GD.Print($"CHANGED (full): {uuid} ({tempClone})");
                        OnItemUpdated?.Invoke(profileItem);
                        break;
                }
            }
        }
    }
}

//listens to addition and remove events of a profile
//when created, will do a full scan and run the OnAdded event for every matched item
public partial class ProfileListener : RefCounted
{
    public event Action<ProfileItemHandle> OnAdded;
    public event Action<ProfileItemHandle> OnUpdated;
    public event Action<ProfileItemHandle> OnRemoved;
    public readonly string profile;
    public readonly ProfileRequests.ProfileItemPredicate predicate;
    readonly List<ProfileItemHandle> items = new();

    public static async Task<ProfileListener> CreateListener(string profile, string type) =>
        await CreateListener(profile, kvp => kvp.Value["templateId"]?.ToString()?.StartsWith(type) ?? false);

    public static async Task<ProfileListener> CreateListener(string profile, ProfileRequests.ProfileItemPredicate predicate)=>
        new(profile, predicate, await ProfileRequests.GetProfileItems(profile, predicate));

    ProfileListener(string profile, ProfileRequests.ProfileItemPredicate predicate, JsonObject existingItems)
    {
        this.profile = profile;
        this.predicate = predicate;
        ProfileRequests.OnItemAdded += ItemAddedDetector;
        ProfileRequests.OnItemUpdated += ItemUpdatedDetector;
        ProfileRequests.OnItemRemoved += ItemRemovalDetector;

        foreach (var existingItem in existingItems)
        {
            items.Add(ProfileItemHandle.CreateHandleUnsafe(new(profile, existingItem.Key)));
        }
    }

    public ProfileItemHandle[] Items => items.ToArray();

    bool debug = false;
    public void EnableDebug() => debug = true;

    void ItemAddedDetector(ProfileItemId profileItem)
    {
        if (GetReferenceCount() == 0)
            return;
        var addedItem = ProfileRequests.GetCachedProfileItemInstance(profileItem);
        if (debug)
            GD.Print($"checking if added item ({profileItem.uuid}) is a match...");
        if (predicate(new(profileItem.uuid, addedItem)))
        {
            if (debug)
                GD.Print($"item ({profileItem.uuid}) is valid");
            var handle = ProfileItemHandle.CreateHandleUnsafe(profileItem);
            items.Add(handle);
            OnAdded?.Invoke(handle);
        }
    }
    void ItemUpdatedDetector(ProfileItemId profileItem)
    {
        if (GetReferenceCount() == 0)
            return;
        var handle = items.FirstOrDefault(i => i.IsMatch(profileItem.uuid));
        if (debug)
            GD.Print($"checking if changed item ({profileItem.uuid}) is a match...");
        if (handle is not null)
        {
            if (debug)
                GD.Print($"item ({profileItem.uuid}) is valid");
            OnUpdated?.Invoke(handle);
        }
    }
    void ItemRemovalDetector(ProfileItemId profileItem)
    {
        if (GetReferenceCount() == 0)
            return;
        var handle = items.FirstOrDefault(i=>i.IsMatch(profileItem.uuid));
        if (debug)
            GD.Print($"checking if removed item ({profileItem.uuid}) is a match...");
        if (handle is not null)
        {
            if (debug)
                GD.Print($"item ({profileItem.uuid}) is valid");
            OnRemoved?.Invoke(handle);
            items.Remove(handle);
        }
    }

    //protected void free()
    //{
    //    ProfileRequests.OnItemAdded -= ItemAddedDetector;
    //    ProfileRequests.OnItemUpdated -= ItemUpdatedDetector;
    //    ProfileRequests.OnItemRemoved -= ItemRemovalDetector;
    //    foreach (var item in items)
    //    {
    //        item.Free();
    //    }
    //}
}

public readonly struct ProfileItemId
{
    public readonly string profile = null;
    public readonly string uuid = null;
    public ProfileItemId(string compositeProfileItemId)
    {
        var splitItem = compositeProfileItemId.Split(':');
        if (splitItem.Length != 2)
        {
            profile = null;
            uuid = null;
            return;
        }
        profile = splitItem[0];
        uuid = splitItem[1];
    }
    public ProfileItemId(string profile, string uuid)
    {
        this.profile = profile;
        this.uuid = uuid;
    }
    public ProfileItemId()
    {
        profile = null;
        uuid = null;
    }
    public readonly string Composite => profile + ":" + uuid;


    public static bool operator ==(ProfileItemId left, ProfileItemId right)=>
        left.uuid==right.uuid && left.profile==right.profile;
    public static bool operator !=(ProfileItemId left, ProfileItemId right) =>
        left.uuid != right.uuid || left.profile != right.profile;

    public static bool operator ==(string left, ProfileItemId right) =>
        new ProfileItemId(left) == right;
    public static bool operator !=(string left, ProfileItemId right) =>
        new ProfileItemId(left) != right;

    public static bool operator ==(ProfileItemId left, string right) =>
        left == new ProfileItemId(right);
    public static bool operator !=(ProfileItemId left, string right) =>
        left != new ProfileItemId(right);

    public override readonly bool Equals(object obj) =>
        (obj is ProfileItemId profileIDObj && profileIDObj == this) ||
        (obj is string stringObj && stringObj == this);

    public override readonly int GetHashCode() => HashCode.Combine(uuid, profile);
}

public partial class ProfileItemHandle : RefCounted
{
    public event Action<ProfileItemHandle> OnChanged;
    public event Action<ProfileItemHandle> OnRemoved;
    public ProfileItemId itemID { get; private set; }
    public bool isValid { get; private set; } = true;

    bool overrideItemSeen;
    public bool ItemSeen => overrideItemSeen || (GetItemUnsafe()["attributes"]?["item_seen"]?.GetValue<bool>() ?? false);
    public async void MarkItemSeen()
    {
        if (!isValid || ItemSeen)
            return;
        overrideItemSeen = true;
        OnChanged?.Invoke(this);
        string content = @$"{{""itemIds"": [""{itemID.uuid}""]}}";
        await ProfileRequests.PerformProfileOperation(FnProfiles.AccountItems, "MarkItemSeen", content);
    }

    public static async Task<ProfileItemHandle> CreateHandle(ProfileItemId itemID)
    {
        if((await ProfileRequests.GetProfileItemInstance(itemID)) is not null)
            return new ProfileItemHandle(itemID);
        return new ProfileItemHandle();
    }

    public static ProfileItemHandle CreateHandleUnsafe(ProfileItemId itemID) =>
        new(itemID);

    public ProfileItemHandle()
    {
        isValid = false;
    }

    ProfileItemHandle(ProfileItemId itemID)
    {
        this.itemID = itemID;
        ProfileRequests.OnItemUpdated += ItemChangeDetector;
        ProfileRequests.OnItemRemoved += ItemRemovalDetector;
    }

    public ProfileItemHandle Clone() => isValid ? new(itemID) { overrideItemSeen = overrideItemSeen } : new();

    public async Task ReplaceWith(ProfileItemId profileItem)
    {
        if (itemID == profileItem)
            return;
        if ((await ProfileRequests.GetProfileItemInstance(profileItem)) is not null)
        {
            itemID = profileItem;
            isValid = true;
            OnChanged?.Invoke(this);
        }
        else
        {
            GD.Print("replaced with invalid");
        }
    }

    public bool IsMatch(string uuid)
    {
        return !isValid || itemID.uuid == uuid;
    }

    void ItemChangeDetector(ProfileItemId profileItem)
    {
        if (GetReferenceCount() == 0)
        {
            isValid = false;
            return;
        }
        if (itemID == profileItem && GetReferenceCount() != 0)
            OnChanged?.Invoke(this);
    }

    void ItemRemovalDetector(ProfileItemId profileItem)
    {
        if (itemID == profileItem)
        {
            isValid = false;
            if (GetReferenceCount() != 0)
                OnRemoved?.Invoke(this);
        }
    }

    //protected void free()
    //{
    //    ProfileRequests.OnItemUpdated -= ItemChangeDetector;
    //    ProfileRequests.OnItemRemoved -= ItemRemovalDetector;
    //}
    public KeyValuePair<string, JsonObject> CreateKVPUnsafe() => KeyValuePair.Create(itemID.uuid, GetItemUnsafe());

    public async Task<JsonObject> GetItem() => isValid ? await ProfileRequests.GetProfileItemInstance(itemID) : null;
    public JsonObject GetItemUnsafe() => isValid ? ProfileRequests.GetCachedProfileItemInstance(itemID) : null;
}

static class FnProfiles
{
    public const string AccountItems = "campaign";
    public const string Backpack = "theater0";
    public const string Storage = "outpost0";
    //const string VentureBackpack = "theater2";
    public const string PeopleCollection = "collection_book_people0";
    public const string SchematicCollection = "collection_book_schematics0";
    public const string CosmeticInventory = "athena";
    public const string Common = "common_core";
}
