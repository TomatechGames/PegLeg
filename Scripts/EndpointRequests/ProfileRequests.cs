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
using System.Text.RegularExpressions;
using System.Net.Http.Headers;

public static class ProfileRequests
{
    static readonly Dictionary<string, JsonObject> profileCache = new();
    static readonly Dictionary<string, GameAccount> gameAccountCache = new();

    public static GameAccount GetAccount(string accountId) => gameAccountCache[accountId] ??= new(accountId);

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
        var items = (await GetProfile(FnProfileTypes.AccountItems))["profileChanges"][0]["profile"]["items"].AsObject();
        var preroll = items.FirstOrDefault(kvp => kvp.Value["templateId"].ToString().StartsWith("PrerollData")).Value;
        if(preroll is null)
        {
            var expireTime = DateTime.Parse(preroll["expiration"].ToString(), null, DateTimeStyles.RoundtripKind);
            //check if prerolls arent outdated yet
            if (DateTime.UtcNow.CompareTo(expireTime) >= 0)
            {
                await PerformProfileOperation(FnProfileTypes.AccountItems, "PopulatePrerolledOffers");
                items = (await GetProfile(FnProfileTypes.AccountItems))["profileChanges"][0]["profile"]["items"].AsObject();
            }
        }
        else
        {
            await PerformProfileOperation(FnProfileTypes.AccountItems, "PopulatePrerolledOffers");
            items = (await GetProfile(FnProfileTypes.AccountItems))["profileChanges"][0]["profile"]["items"].AsObject();
        }

        return items
            .Where(kvp => kvp.Value["templateId"].ToString().StartsWith("PrerollData"))
            .Select(kvp => kvp.Value.AsObject())
            .ToArray();
    }

    public static float GetSurvivorBonusUnsafe(string bonusID, int perSquadRequirement = 2, float boostBase = 5)
    {
        var matchingSurvivors = GetCachedProfileItems(FnProfileTypes.AccountItems, kvp =>
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

    public static async Task<JsonObject> GetOrderCounts(OrderRange range)
    {
        var orderRange = (await GetProfile(FnProfileTypes.Common))["profileChanges"][0]["profile"]["stats"]["attributes"][range.ToAttribute()];
        var lastIntervalTime = DateTime.Parse(orderRange["lastInterval"].ToString(), null, DateTimeStyles.RoundtripKind);
        if (lastIntervalTime != range.ToInterval())
            return null;
        return orderRange["purchaseList"].AsObject();
    }
    public static async Task<JsonObject> GetFufillmentCounts()
    {
        var attributes = (await GetProfile(FnProfileTypes.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        return attributes["in_app_purchases"]["fufillmentCounts"].AsObject();
    }

    public static async Task<string> GetSACCode(bool addExpiredText = true)
    {
        var attributes = (await GetProfile(FnProfileTypes.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        var lastSetTime = DateTime.Parse(attributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        bool isExpired = (DateTime.UtcNow - lastSetTime).Days > 13;
        return attributes["mtx_affiliate"] + (isExpired && addExpiredText ? " (Expired)" : "");
    }

    public static async Task<bool> IsSACExpired() => Mathf.FloorToInt(await GetSACTime()) > 13;

    public static async Task<double> GetSACTime()
    {
        var attributes = (await GetProfile(FnProfileTypes.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        var lastSetTime = DateTime.Parse(attributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        return (DateTime.UtcNow - lastSetTime).TotalDays;
    }

    public static async Task<bool> SetSACCode(string newName)
    {
        await PerformProfileOperation(FnProfileTypes.Common, "SetAffiliateName", "{\"affiliateName\":\"" + newName + "\"}");
        //TODO: return false if creator code not found
        return true;
    }

    static List<string> localPinnedQuests;
    static DateTime questsLastRefreshedAt = DateTime.MinValue;
    static async Task CheckLocalPinnedQuests()
    {
        bool outOfDate = (questsLastRefreshedAt - DateTime.UtcNow).TotalMinutes > 5;
        if (localPinnedQuests != null && !outOfDate)
            return;
        
        localPinnedQuests = (await GetProfile(FnProfileTypes.AccountItems, true))
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
            OnItemUpdated?.Invoke(new(LoginRequests.AccountID, FnProfileTypes.AccountItems, uuid));
            SendLocalPinnedQuests();
        }
    }

    public static async Task RemovePinnedQuest(string uuid)
    {
        await CheckLocalPinnedQuests();
        if (localPinnedQuests.Contains(uuid))
        {
            localPinnedQuests.Remove(uuid);
            OnItemUpdated?.Invoke(new(LoginRequests.AccountID, FnProfileTypes.AccountItems, uuid));
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
            OnItemUpdated?.Invoke(new(LoginRequests.AccountID, FnProfileTypes.AccountItems, uuid));
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
        await PerformProfileOperation(FnProfileTypes.AccountItems, "SetPinnedQuests", content.ToString());
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
            (await PerformProfileOperation(FnProfileTypes.AccountItems, "FortRerollDailyQuest", content.ToString()))
            ["notifications"]?
            .AsArray()
            .FirstOrDefault(n => n["type"].ToString()== "dailyQuestReroll");
        if (notif is null)
            return null;
        var newUUID = GetFirstProfileItemUnsafe(FnProfileTypes.AccountItems, (kvp) => kvp.Value["templateId"].ToString() == notif["newQuestId"].ToString())["uuid"].ToString();
        return ProfileItemHandle.CreateHandleUnsafe(new(LoginRequests.AccountID, FnProfileTypes.AccountItems, newUUID));
    }

    public static bool CanRerollQuestUnsafe()
    {
        return profileCache[FnProfileTypes.AccountItems]
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
            if (!rewardNotificationChecks.ContainsKey(FnProfileTypes.Backpack))
                await GetProfile(FnProfileTypes.Backpack);

            lock (rewardNotificationChecks)
            {
                exists = rewardNotificationChecks[FnProfileTypes.Backpack]?.Any(c => c.IsMatch(notificationKey)) ?? false;
            }
        }
        else
        {
            if (!rewardNotificationChecks.ContainsKey(FnProfileTypes.AccountItems))
                await GetProfile(FnProfileTypes.AccountItems);
            lock (rewardNotificationChecks)
            {
                exists = rewardNotificationChecks[FnProfileTypes.AccountItems]?.Any(c => c.IsMatch(notificationKey)) ?? false;
            }
            if (!exists && collectableTypess.Contains(itemTemplate["Type"].ToString()))
            {
                var collectionProfile = itemTemplate["Type"].ToString() == "Schematic" ? FnProfileTypes.SchematicCollection : FnProfileTypes.PeopleCollection;

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

        string profile = type == "Schematic" ? FnProfileTypes.SchematicCollection : FnProfileTypes.PeopleCollection;
        if (!profileCache.ContainsKey(profile))
            return null;

        return ProfileItemExistsUnsafe(profile, kvp => 
            Regex.IsMatch(kvp.Value["templateId"].ToString(), regexPattern) &&
            itemInstance["attributes"]["personality"]?.ToString() == kvp.Value["attributes"]["personality"]?.ToString()
        );
    }

    public static async Task<JsonObject> GetProfile(string profileId, bool forceRefresh = false) =>
        await PerformProfileOperation(profileId, "QueryProfile", queryIgnoreCache: forceRefresh);

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

        if (profileId == FnProfileTypes.AccountItems)
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
                ProfileItemId profileItem = new(LoginRequests.AccountID, profile, uuid);
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
            items.Add(ProfileItemHandle.CreateHandleUnsafe(new(LoginRequests.AccountID, profile, existingItem.Key)));
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
    public readonly string account = null;
    public readonly string profile = null;
    public readonly string uuid = null;

    public ProfileItemId(JsonObject compositeProfileItemId)
    {
        account = compositeProfileItemId?["account"]?.ToString();
        profile = compositeProfileItemId?["profile"]?.ToString();
        uuid = compositeProfileItemId?["uuid"]?.ToString();
    }

    public ProfileItemId(string account, string profile, string uuid)
    {
        this.account = account;
        this.profile = profile;
        this.uuid = uuid;
    }

    public ProfileItemId() { }

    public readonly JsonObject Composite => new()
    {
        ["account"] = account,
        ["profile"] = profile,
        ["uuid"] = uuid,
    };


    public static bool operator ==(ProfileItemId left, ProfileItemId right) =>
        left.uuid == right.uuid && left.profile == right.profile && left.account == right.account;
    public static bool operator !=(ProfileItemId left, ProfileItemId right) =>
        left.uuid != right.uuid || left.profile != right.profile || left.account != right.account;

    //public static bool operator ==(JsonObject left, ProfileItemId right) =>
    //    new ProfileItemId(left) == right;
    //public static bool operator !=(JsonObject left, ProfileItemId right) =>
    //    new ProfileItemId(left) != right;

    //public static bool operator ==(ProfileItemId left, JsonObject right) =>
    //    left == new ProfileItemId(right);
    //public static bool operator !=(ProfileItemId left, JsonObject right) =>
    //    left != new ProfileItemId(right);

    public override readonly bool Equals(object obj) =>
        (obj is ProfileItemId profileIDObj && profileIDObj == this);
    //    || (obj is JsonObject compObj && compObj == this);

    public override readonly int GetHashCode() => HashCode.Combine(uuid, profile, account);
}

public enum OrderRange
{
    Daily,
    Weekly,
    Monthly
}

public readonly struct FORTStats
{
    public readonly float fortitude;
    public readonly float offense;
    public readonly float resistance;
    public readonly float technology;
    public FORTStats(float fortitude, float offense, float resistance, float technology)
    {
        this.fortitude = fortitude;
        this.offense = offense;
        this.resistance = resistance;
        this.technology = technology;
    }
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
        await ProfileRequests.PerformProfileOperation(FnProfileTypes.AccountItems, "MarkItemSeen", content);
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

public class GameAccount
{
    public GameAccount(string accountId)
    {
        this.accountId = accountId;
    }
    public bool isOwned { get; set; }
    public string accountId { get; private set; }

    Dictionary<string, GameProfile> profiles = new();

    public GameProfile this[string profileId] => GetProfile(profileId);
    public GameProfile GetProfile(string profileId) => profiles[profileId] ??= new(this, profileId);
    public bool HasProfile(string profileId) => profiles.ContainsKey(profileId);

    public async Task<GameAccount> EnsureProfile(string profileId, bool force = false)
    {
        await GetProfile(profileId).Query(force);
        return this;
    }

    public void HandleProfileChanges(JsonArray multiUpdate)
    {
        foreach (var profileUpdate in multiUpdate)
        {
            string profileId = profileUpdate["profileId"].ToString();
            if (profiles.ContainsKey(profileId))
                profiles[profileId].ApplyProfileChanges(profileUpdate["profileChanges"].AsArray());
        }
    }


    int authExpiresAt = -999999;
    int refreshExpiresAt = -999999;
    AuthenticationHeaderValue accountAuthHeader;
    public bool AuthTokenValid => authExpiresAt > Time.GetTicksMsec() * 0.001;
    bool RefreshTokenValid => refreshExpiresAt > Time.GetTicksMsec() * 0.001;
    public AuthenticationHeaderValue AuthHeader => accountAuthHeader;
    public async Task<bool> Authenticate()
    {
        //LoginRequests.AccountAuthHeader;
        return false;
    }


    public event Action<GameAccount> OnFortStatsChanged;
    FORTStats? fortStats;
    bool fortStatsDirty = true;
    FORTStats FortStats => GetFORTStats();
    public FORTStats GetFORTStats(bool force = false) => (force || fortStatsDirty) ? CalculateFORTStats() : fortStats ?? CalculateFORTStats();

    FORTStats CalculateFORTStats()
    {
        var accountItems = GetProfile(FnProfileTypes.AccountItems);
        var researchStats = accountItems.statAttributes["research_levels"];
        var statItems = accountItems.GetItems("Stat");
        var equippedWorkerItems = accountItems.GetItems("Worker", item => item.attributes.ContainsKey("squad_id"));

        int LookupStatItem(string statId) => statItems.First(item => item.templateId == statId).quantity;

        float LookupWorkers(string squadId)
        {
            var matchingWorkers = equippedWorkerItems
                .Where(item => item.attributes["squad_id"].ToString() == squadId);
            return matchingWorkers.Select(item => item.Rating).Sum();
        }

        //+ profileStats["fortitude"].GetValue<int>()
        float fortitude = LookupStatItem("Stat:fortitude") + LookupStatItem("Stat:fortitude_team") + LookupWorkers("squad_attribute_medicine_trainingteam") + LookupWorkers("squad_attribute_medicine_emtsquad");
        float offense = LookupStatItem("Stat:offense") + LookupStatItem("Stat:offense_team") + LookupWorkers("squad_attribute_arms_fireteamalpha") + LookupWorkers("squad_attribute_arms_closeassaultsquad");
        float resistance = LookupStatItem("Stat:resistance") + LookupStatItem("Stat:resistance_team") + LookupWorkers("squad_attribute_scavenging_scoutingparty") + LookupWorkers("squad_attribute_scavenging_gadgeteers");
        float technology = LookupStatItem("Stat:technology") + LookupStatItem("Stat:technology_team") + LookupWorkers("squad_attribute_synthesis_corpsofengineering") + LookupWorkers("squad_attribute_synthesis_thethinktank");


        GD.Print($"FORTITUDE: {fortitude}");
        GD.Print($"OFFENCE: {offense}");
        GD.Print($"RESISTANCE: {resistance}");
        GD.Print($"TECH: {technology}");

        fortStats = new(fortitude, offense, resistance, technology);
        fortStatsDirty = false;
        OnFortStatsChanged?.Invoke(this);
        return fortStats.Value;
    }

    public async Task<GameItem[]> GetAllPrerollData()
    {
        var accountItems = GetProfile(FnProfileTypes.AccountItems);
        var allPrerolls = accountItems.GetItems("PrerollData");

        if 
        (
            !accountItems.hasProfile || 
            allPrerolls.FirstOrDefault() is not GameItem firstPreroll ||
            DateTime.UtcNow.CompareTo(DateTime.Parse(firstPreroll.attributes["expiration"].ToString(), null, DateTimeStyles.RoundtripKind)) >= 0
        )
        {
            await accountItems.PerformOperation("PopulatePrerolledOffers");
            allPrerolls = accountItems.GetItems("PrerollData");
        }

        if (!accountItems.hasProfile)
        {
            await accountItems.Query();
            allPrerolls = accountItems.GetItems("PrerollData");
        }

        return allPrerolls;
    }

    public float GetSurvivorBonusUnsafe(string bonusID, int perSquadRequirement = 2, float boostBase = 5)
    {
        var matchingSurvivors = GetProfile(FnProfileTypes.AccountItems).GetItems("Worker", gameItem =>
        {
            if (gameItem.attributes["squad_id"] is null || gameItem.attributes["set_bonus"] is null)
                return false;
            var thisBonus = gameItem.attributes["set_bonus"].ToString().Split(".")[^1];
            return thisBonus == bonusID;
        })
        .GroupBy(gameItem => gameItem.attributes["squad_id"].ToString());

        int boostMatchCount = matchingSurvivors.Select(g => g.Count() / perSquadRequirement).Sum();

        return boostBase * boostMatchCount;
    }

    public JsonObject GetOrderCounts(OrderRange range)
    {
        var commonData = GetProfile(FnProfileTypes.Common);
        var orderRange = commonData.statAttributes[range.ToAttribute()];
        var lastIntervalTime = DateTime.Parse(orderRange["lastInterval"].ToString(), null, DateTimeStyles.RoundtripKind);
        if (lastIntervalTime != range.ToInterval())
            return null;
        return orderRange["purchaseList"].AsObject();
    }
    public string GetSACCode(bool addExpiredText = true)
    {
        var commonData = GetProfile(FnProfileTypes.Common);
        var lastSetTime = DateTime.Parse(commonData.statAttributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        bool isExpired = (DateTime.UtcNow - lastSetTime).Days > 13;
        return commonData.statAttributes["mtx_affiliate"] + (isExpired && addExpiredText ? " (Expired)" : "");
    }

    public bool IsSACExpired() => Mathf.FloorToInt(GetSACTime()) > 13;

    public double GetSACTime()
    {
        var commonData = GetProfile(FnProfileTypes.Common);
        var lastSetTime = DateTime.Parse(commonData.statAttributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        return (DateTime.UtcNow - lastSetTime).TotalDays;
    }

    public async Task<bool> SetSACCode(string newName)
    {
        await GetProfile(FnProfileTypes.Common).PerformOperation("SetAffiliateName", "{\"affiliateName\":\"" + newName + "\"}");
        //TODO: return false if creator code not found
        return true;
    }

    List<GameItem> localPinnedQuests;
    DateTime questsLastRefreshedAt = DateTime.MinValue;
    async Task<GameProfile> CheckLocalPinnedQuests()
    {
        var accountItems = GetProfile(FnProfileTypes.AccountItems);
        bool outOfDate = (questsLastRefreshedAt - DateTime.UtcNow).TotalMinutes > 5;
        if (localPinnedQuests != null && !outOfDate)
            return accountItems;

        await accountItems.Query();
        localPinnedQuests = accountItems.statAttributes["client_settings"]["pinnedQuestInstances"]
            .AsArray()
            .Select(q => accountItems.GetItem(q.ToString()))
            .ToList();
        return accountItems;
    }

    public async Task AddPinnedQuest(GameItem item)
    {
        var accountItems = await CheckLocalPinnedQuests();
        if (item.profile == accountItems && !localPinnedQuests.Contains(item))
        {
            localPinnedQuests.Add(item);
            accountItems.SendItemUpdate(item);
            SendLocalPinnedQuests(accountItems);
        }
    }

    public async Task RemovePinnedQuest(GameItem item)
    {
        var accountItems = await CheckLocalPinnedQuests();
        if (localPinnedQuests.Contains(item))
        {
            localPinnedQuests.Remove(item);
            accountItems.SendItemUpdate(item);
            SendLocalPinnedQuests(accountItems);
        }
    }

    public void ClearPinnedQuests()
    {
        GD.Print("clearing all pinned");
        var unpinnedQuests = localPinnedQuests?.ToArray() ?? Array.Empty<GameItem>();
        localPinnedQuests?.Clear();
        var accountItems = GetProfile(FnProfileTypes.AccountItems);
        foreach (var item in unpinnedQuests)
        {
            accountItems.SendItemUpdate(item);
        }
        SendLocalPinnedQuests(accountItems);
    }

    async void SendLocalPinnedQuests(GameProfile accountItems)
    {
        localPinnedQuests ??= new();
        JsonObject content = new()
        {
            ["pinnedQuestIds"] = new JsonArray(localPinnedQuests.Select(q => (JsonValue)q.uuid).ToArray())
        };
        await accountItems.PerformOperation("SetPinnedQuests", content.ToString());
    }

    public bool HasPinnedQuest(string uuid)
    {
        localPinnedQuests ??= new();
        return localPinnedQuests.Exists(q=>q.uuid==uuid);
    }

    public bool HasPinnedQuest(GameItem item)
    {
        localPinnedQuests ??= new();
        return localPinnedQuests.Contains(item);
    }

    public async Task<GameItem> RerollQuest(GameItem item)
    {
        var accountItems = GetProfile(FnProfileTypes.AccountItems);
        if (item.profile != accountItems)
            return null;
        GD.Print("rerolling quest " + item.uuid);
        JsonObject content = new()
        {
            ["questId"] = item.uuid
        };
        var notif = (await accountItems.PerformOperation("FortRerollDailyQuest", content.ToString()))
            .FirstOrDefault(n => n["type"].ToString() == "dailyQuestReroll");
        if (notif is null)
            return null;
        return accountItems.GetItems("Quest", item => item.templateId==notif["newQuestId"].ToString()).FirstOrDefault();
    }

    public bool CanRerollQuest() => (GetProfile(FnProfileTypes.AccountItems).statAttributes?["quest_manager"]?["dailyQuestRerolls"]?.GetValue<int>() ?? 0) > 0;
}

public class GameProfile
{
    public GameProfile(GameAccount account, string profileId)
    {
        this.account = account;
        this.profileId = profileId;
    }

    public bool hasProfile { get; private set; }
    public GameAccount account { get; private set; }
    public string profileId { get; private set; }
    public JsonObject statAttributes { get; private set; }

    Dictionary<string, GameItem> items = new();
    Dictionary<string, List<GameItem>> groupedItems;

    public static event Action<GameProfile> OnStatChanged;
    public static event Action<GameItem> OnItemAdded;
    public static event Action<GameItem> OnItemUpdated;
    public static event Action<GameItem> OnItemRemoved;

    public GameItem GetItem(string uuid) => items.ContainsKey(uuid) ? items[uuid] : null;
    public GameItem[] GetItems(GameItemPredicate predicate) => GetItems(null, predicate);
    public GameItem[] GetItems(string type = null, GameItemPredicate predicate = null)
    {
        var typedItems = items.Values.AsEnumerable();
        if(type is not null)
        {
            if(!groupedItems.ContainsKey(type))
                return Array.Empty<GameItem>();
            typedItems = groupedItems[type].AsEnumerable();
        }
        if (predicate is null)
            return typedItems.ToArray();
        return typedItems.Where(gameItem => predicate(gameItem)).ToArray();
    }

    public void SendItemUpdate(GameItem item)
    {
        if (item.profile == this)
            OnItemUpdated?.Invoke(item);
    }

    public async Task<GameProfile> Query(bool force=false)
    {
        if (!hasProfile || force)
            await PerformOperation("QueryProfile");
        return this;
    }

    static SemaphoreSlim profileOperationSephamore = new(1);
    public async Task<JsonArray> PerformOperation(string operation, string content = "{}")
    {
        await profileOperationSephamore.WaitAsync();
        try
        {
            return await PerformOperationUnsafe(operation, content);
        }
        finally
        {
            profileOperationSephamore.Release();
        }
    }

    public async Task<JsonArray> PerformOperationUnsafe(string operation, string content = "{}")
    {
        if (!await LoginRequests.TryLogin())
        {
            GD.Print("profile request failed: not logged in");
            return null;
        }

        //Todo: move to Account
        account.isOwned = LoginRequests.AccountID == account.accountId;

        if (account.isOwned && operation == "QueryProfile")
            operation = "QueryPublicProfile";

        if (!account.isOwned && operation != "QueryPublicProfile")
        {
            GD.Print($"cannot perform {operation} on unowned profile");
            return null;
        }

        if (!account.isOwned && profileId != FnProfileTypes.AccountItems && profileId != FnProfileTypes.Common)
        {
            GD.Print($"cannot access unowned profile of type {profileId}");
            return null;
        }

        if (operation == "MarkItemSeen")
        {
            var targetItemIDs = JsonNode.Parse(content)["itemIds"].AsArray().Select(n => n.ToString());
            foreach (var itemId in targetItemIDs)
            {
                if (items.ContainsKey(itemId))
                    OnItemUpdated?.Invoke(items[itemId].SetSeenLocal());
            }
        }

        StringContent jsonContent = new(content, Encoding.UTF8, "application/json");
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"fortnite/api/game/v2/profile/{LoginRequests.AccountID}/{(account.isOwned ? "public" : "client")}/{operation}?profileId={profileId}&rvn=-1"
            )
            {
                Content = jsonContent
            };

        request.Headers.Authorization = LoginRequests.AccountAuthHeader;

        var result = (await Helpers.MakeRequest(FNEndpoints.gameEndpoint, request)).AsObject();
        if (result.ContainsKey("errorCode"))
        {
            var _ = GenericConfirmationWindow.ShowErrorForWebResult(result);
            return null;
        }

        var resultStats = result["profileChanges"][0]["profile"]["stats"]["attributes"].AsObject();
        var resultItems = result["profileChanges"][0]["profile"]["items"].AsObject();

        if (hasProfile)
            ApplyProfileChanges(GenerateChanges(resultItems, resultStats));
        else
        {
            items = resultItems.Select(kvp => new GameItem(this, kvp.Key, kvp.Value.AsObject())).ToDictionary(item => item.uuid);
            groupedItems = new(
                    items.GroupBy(kvp => kvp.Value.templateId.Split(":")[0])
                    .Select(grouping => KeyValuePair.Create(grouping.Key, grouping.Select(kvp=>kvp.Value).ToList()))
                );
            hasProfile = true;
        }
        statAttributes = resultStats;

        if (result.ContainsKey("multiUpdate"))
            account.HandleProfileChanges(result["multiUpdate"].AsArray());

        GD.Print("operation complete");

        if (result.ContainsKey("notifications"))
            return result["notifications"].AsArray();
        return new();
    }

    JsonArray GenerateChanges(JsonObject newItems, JsonObject newStats = null)
    {
        var oldKeys = items.Keys.ToArray();
        var newKeys = newItems.Select(x => x.Key).ToArray();

        var addedKeys = newKeys.Except(oldKeys);
        var removedKeys = oldKeys.Except(newKeys);
        var possiblyChangedKeys = oldKeys.Intersect(newKeys);

        JsonArray profileChanges = new();

        foreach (var itemKey in removedKeys)
        {
            profileChanges.Add(new JsonObject()
            {
                ["changeType"] = "itemRemoved",
                ["itemId"] = itemKey
            });
        }
        foreach (var itemKey in possiblyChangedKeys)
        {
            var from = items[itemKey].RawData.ToString();
            var to = newItems[itemKey].ToString();
            if (from != to)
            {
                GD.Print($"FROM ({from}) >>> ({to})");
                profileChanges.Add(new JsonObject()
                {
                    ["changeType"] = "itemFullyChanged",
                    ["itemId"] = itemKey,
                    ["item"] = newItems[itemKey].Reserialise()
                });
            }
        }
        foreach (var itemKey in addedKeys)
        {
            profileChanges.Add(new JsonObject()
            {
                ["changeType"] = "itemAdded",
                ["itemId"] = itemKey,
                ["item"] = newItems[itemKey].Reserialise()
            });
        }
        if (newStats.ToString() != statAttributes.ToString())
        {
            profileChanges.Add(new JsonObject()
            {
                ["changeType"] = "statChanged"
            });
        }

        return profileChanges;
    }

    public void ApplyProfileChanges(JsonArray profileChanges)
    {
        foreach (var change in profileChanges)
        {
            string changeType = change["changeType"].ToString();
            string uuid = change["itemId"]?.ToString();

            //ProfileItemId profileItemId = new(profileId, uuid);
            GameItem targetItem;
            switch (changeType)
            {
                case "itemAdded":
                    targetItem = new(this, uuid, change["item"].AsObject());
                    items[uuid] = targetItem;
                    GD.Print($"ADDED: {uuid} ({items[uuid]})");
                    OnItemAdded?.Invoke(targetItem);
                    break;
                case "itemRemoved":
                    targetItem = items[uuid];
                    GD.Print($"REMOVING: {uuid} ({targetItem})");
                    targetItem.SendRemovingEvent();
                    items.Remove(uuid);
                    OnItemRemoved?.Invoke(targetItem);
                    targetItem.DisconnectFromProfile();
                    break;
                case "statChanged":
                    GD.Print($"STAT CHANGED: ? ({change})");
                    OnStatChanged?.Invoke(this);
                    break;
                case "itemQuantityChanged":
                    targetItem = items[uuid];
                    targetItem.SetQuantity(change["quantity"].GetValue<int>());
                    GD.Print($"CHANGED (quantity): {uuid} ({targetItem})");
                    targetItem.SendChangedEvent();
                    OnItemUpdated?.Invoke(targetItem);
                    break;
                case "itemAttrChanged":
                    targetItem = items[uuid];
                    targetItem.attributes[change["attributeName"].ToString()] = change["attributeValue"].Reserialise();
                    GD.Print($"CHANGED (attribute): {uuid}[{change["attributeName"]}] ({targetItem})");
                    targetItem.SendChangedEvent();
                    OnItemUpdated?.Invoke(targetItem);
                    break;
                case "itemFullyChanged":
                    //custom one for handling generated changelogs
                    targetItem = items[uuid];
                    targetItem.SetRawData(change["item"].AsObject());
                    GD.Print($"CHANGED (full): {uuid} ({targetItem})");
                    targetItem.SendChangedEvent();
                    OnItemUpdated?.Invoke(targetItem);
                    break;
            }
        }
    }
}

public delegate bool GameItemPredicate(GameItem gameItem);

public class GameItem
{
    public event Action<GameItem> OnChanged;
    public event Action<GameItem> OnRemoving;


    public GameItem(GameProfile profile, string uuid, JsonObject rawData)
    {
        this.uuid = uuid;
        this.profile = profile;
        SetRawData(rawData);
    }

    public GameItem(GameItemTemplate template, int quantity, JsonObject attributes = null, GameItem inspectorOverride = null)
    {
        attemptedTemplateSearch = true;
        _template = template;
        templateId = template.TemplateId;
        this.quantity = quantity;
        this.attributes = attributes;
        this.inspectorOverride = inspectorOverride;
    }

    public GameProfile profile { get; private set; }
    public string uuid { get; private set; }

    public GameItem inspectorOverride { get; private set; }

    bool attemptedTemplateSearch;
    public string templateId { get; private set; }
    public GameItemTemplate template
    {
        get
        {
            if (attemptedTemplateSearch)
                return _template;
            _template ??= BanjoAssets.TryGetNewTemplate(templateId);
            attemptedTemplateSearch = true;
            return _template;
        }
    }
    GameItemTemplate _template;

    public JsonObject attributes { get; private set; }

    public int quantity { get; private set; }
    public void SetQuantity(int newQuant) => quantity = newQuant;

    public JsonObject RawData => new()
    {
        ["templateId"] = templateId,
        ["quantity"] = quantity,
        ["attributes"] = attributes.Reserialise(),
        ["searchTags"] = template?["searchTags"]
    };

    public void SetRawData(JsonObject rawData)
    {
        if (templateId != rawData["templateId"].ToString())
        {
            attemptedTemplateSearch = false;
            _template = null;
            templateId = rawData["templateId"].ToString();
        }
        quantity = rawData["quantity"].GetValue<int>();
        attributes = rawData["attributes"]?.AsObject();
        _rating = null;
    }

    bool isSeenLocal = false;
    public bool IsSeen => isSeenLocal || (attributes?["item_seen"]?.GetValue<bool>() ?? false);
    public GameItem SetSeenLocal()
    {
        isSeenLocal = true;
        SendChangedEvent();
        return this;
    }

    public bool IsPinnedQuest => profile?.account.HasPinnedQuest(this) ?? false;

    public void ClearRating() => _rating = null;
    int? _rating;
    public int Rating => _rating ??= CalculateRating();
    public int UpdateRating() => (_rating = CalculateRating()) ?? 0;

    public int CalculateRating(string survivorSquad = null)
    {
        if (!BanjoAssets.TryGetSource("ItemRatings", out var ratings))
            return 0;
        var tier = template.Tier;
        if (tier == 0)
            return 0;

        var level = attributes?["level"]?.GetValue<int>() ?? 0;
        level = Mathf.Clamp(level, Mathf.Min(1, (tier * 10) - 10), tier * 10);
        string ratingCategory = template.Type == "Worker" ? (template.SubType is null ? "Survivor" : "LeadSurvivor") :"Default";

        string ratingKey = template.GetCompactRarityAndTier();
        if (ratingCategory == "LeadSurvivor")
            ratingKey = ratingKey.Replace("UR_", "SR_");

        var ratingSet = ratings[ratingCategory]["Tiers"][ratingKey];
        if (ratingSet is null)
        {
            GD.Print($"no rating set {ratingCategory}:{ratingKey}");
            return 0;
        }
        int subLevel = level - ratingSet["FirstLevel"].GetValue<int>();
        int rating = (int)ratingSet["Ratings"][subLevel].GetValue<float>();

        survivorSquad ??= attributes?["squad_id"]?.ToString();
        if (template.Type != "Worker" && survivorSquad is null)
            return rating;

        if (template.SubType is string leadType)
        {
            //check for lead synergy match
            var matchedSquadID = BanjoAssets.supplimentaryData.SynergyToSquadId[leadType.Replace(" ", "")];
            if (matchedSquadID == survivorSquad)
                rating *= 2;
        }
        else if (profile.profileId == FnProfileTypes.AccountItems)
        {
            var leadSurvivor = profile.GetItems("Worker", item =>
                item.attributes?["squad_id"]?.ToString() == survivorSquad &&
                item.attributes["squad_slot_idx"].GetValue<int>() == 0
            ).FirstOrDefault();

            string leaderRarity = leadSurvivor?.template.Rarity ?? "";
            int rarityBoost = leaderRarity switch
            {
                "Mythic" => 8,
                "Legendary" => 8,
                "Epic" => 5,
                "Rare" => 4,
                "Uncommon" => 3,
                "Common" => 2,
                _ => 3
            };

            int rarityPenalty = (leaderRarity == "Mythic") ? 2 : 0;

            string targetPersonality = leadSurvivor?.attributes["personality"].ToString().Split(".")[^1] ?? "";
            string currentPersonality = attributes["personality"].ToString().Split(".")[^1];

            rating += currentPersonality == targetPersonality ? rarityBoost : -rarityPenalty;
            rating = Mathf.Max(rating, 1);
        }

        return rating;
    }

    Dictionary<ItemTextureType, Texture2D> textures = new();
    public Texture2D GetTexture(ItemTextureType textureType = ItemTextureType.Preview, Texture2D fallbackIcon = null)
    {
        if (textures.ContainsKey(textureType))
            return textures[textureType];

        if (textureType == ItemTextureType.Personality)
            return GetPersonalityTexture(fallbackIcon);

        if (textureType == ItemTextureType.SetBonus)
            return GetSetBonusTexture(fallbackIcon);

        if (textureType == ItemTextureType.Preview && (attributes?["portrait"]?.TryGetTemplate(out var portraitTemplate) ?? false))
        {
            var portraitTexture = portraitTemplate.GetItemTexture(fallbackIcon);
            if (portraitTexture is not null)
                return textures[textureType] = portraitTexture;
        }

        return template?.GetTexture(textureType);
    }
    
    Texture2D GetPersonalityTexture(Texture2D fallbackIcon = null)
    {
        if (template.Type != "Worker")
            return fallbackIcon;

        var personalityId = template.Personality ?? attributes?["personality"]?.ToString()?.Split(".")?[^1];

        if (personalityId is not null && BanjoAssets.supplimentaryData.PersonalityIcons.ContainsKey(personalityId))
            return textures[ItemTextureType.Personality] = BanjoAssets.supplimentaryData.PersonalityIcons[personalityId];

        return fallbackIcon;
    }

    Texture2D GetSetBonusTexture(Texture2D fallbackIcon = null)
    {
        if (template.Type != "Worker")
            return fallbackIcon;

        if (template.SubType is string subType)
        {
            subType = subType.Replace("Martial Artist", "MartialArtist");
            if (BanjoAssets.supplimentaryData.SquadIcons.ContainsKey(subType))
                return textures[ItemTextureType.SetBonus] = BanjoAssets.supplimentaryData.SquadIcons[subType];
        }
        else if(attributes?["set_bonus"]?.ToString()?.Split(".")?[^1] is string setBonus)
        {
            if (BanjoAssets.supplimentaryData.SetBonusIcons.ContainsKey(setBonus))
                return textures[ItemTextureType.SetBonus] = BanjoAssets.supplimentaryData.SetBonusIcons[setBonus];
        }

        return fallbackIcon;
    }

    public void GenerateSearchTags(bool assumeUncommon = true) =>
        template.GenerateSearchTags(assumeUncommon);

    public override string ToString() => $"{{\n  id:{uuid}\n  template:{templateId}\n  quantity:{quantity}\n  attributes:{attributes}}}";

    public void SendChangedEvent() => OnChanged?.Invoke(this);
    public void SendRemovingEvent() => OnRemoving?.Invoke(this);
    public void DisconnectFromProfile()
    {
        profile = null;
        uuid = null;
    }
}


static class FnProfileTypes
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
