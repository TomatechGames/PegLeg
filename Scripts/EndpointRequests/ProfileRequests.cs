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

public static class ProfileRequests
{
    static readonly Dictionary<string, JsonObject> profileCache = new();
    static readonly List<string> validProfiles = new();

    public static void InvalidateProfileCache(string profileId) => validProfiles.Remove(profileId);
    public static void InvalidateProfileCache() => validProfiles.Clear();

    public static async Task RevalidateProfiles()
    {
        var toBeValidated = validProfiles.ToArray();
        validProfiles.Clear();
        foreach (var item in toBeValidated)
        {
            await GetProfile(item);
        }
    }

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
    public static async Task<bool> ProfileItemExists(string profileID, ProfileItemPredicate predicate, bool preferCache = false)
    {
        if (profileCache.ContainsKey(profileID) && preferCache)
            return ProfileItemExistsUnsafe(profileID, predicate);
       return (await GetProfile(profileID))
            ["profileChanges"][0]["profile"]["items"]
            .AsObject()
            .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.AsObject()))
            .Any(kvp => predicate(kvp));
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
        var matchedItem =
            (await GetProfile(profileID))
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

    static async Task<DateTime> OrderRangeToInterval(OrderRange range) => range switch
    {
        OrderRange.Daily => (await CalenderRequests.DailyShopRefreshTime()).AddDays(-1),
        OrderRange.Weekly => (await CalenderRequests.WeeklyShopRefreshTime()).AddDays(-7),
        _ => throw new NotImplementedException(),
    };

    public static async Task<JsonObject> GetOrderCounts(OrderRange range)
    {
        var orderRange = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"][OrderRangeToAttribute(range)];
        var lastIntervalTime = DateTime.Parse(orderRange["lastInterval"].ToString(), null, DateTimeStyles.RoundtripKind);
        if (lastIntervalTime != await OrderRangeToInterval(range))
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

    public static async Task<bool> IsSACExpired()
    {
        var attributes = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        var lastSetTime = DateTime.Parse(attributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        return (DateTime.UtcNow - lastSetTime).Days > 13;
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

    public static async Task AddPinnedQuest(string uuid)
    {
        var existingPinnedQuests = profileCache[FnProfiles.AccountItems]
            ["profileChanges"][0]["profile"]["stats"]["attributes"]["client_settings"]["pinnedQuestInstances"]
            .AsArray()
            .Select(q => q.ToString())
            .ToList();
        if (!existingPinnedQuests.Contains(uuid))
        {
            existingPinnedQuests.Add(uuid);
            JsonObject content = new()
            {
                ["pinnedQuestIds"] = new JsonArray(existingPinnedQuests.Select(q => (JsonValue)q.ToString()).ToArray())
            };
            GD.Print(content.ToString());
            await PerformProfileOperation(FnProfiles.AccountItems, "SetPinnedQuests", content.ToString());
        }
    }

    public static async Task RemovePinnedQuest(string uuid)
    {
        var existingPinnedQuests = profileCache[FnProfiles.AccountItems]
            ["profileChanges"][0]["profile"]["stats"]["attributes"]["client_settings"]["pinnedQuestInstances"]
            .AsArray()
            .Select(q => q.ToString())
            .ToList();
        if (existingPinnedQuests.Contains(uuid))
        {
            existingPinnedQuests.Remove(uuid);
            JsonObject content = new()
            {
                ["pinnedQuestIds"] = new JsonArray(existingPinnedQuests.Select(q => (JsonValue)q).ToArray())
            };
            GD.Print(content.ToString());
            await PerformProfileOperation(FnProfiles.AccountItems, "SetPinnedQuests", content.ToString());
        }
    }

    public static async Task ClearPinnedQuests()
    {
        GD.Print("clearing all pinned");
        JsonObject content = new()
        {
            ["pinnedQuestIds"] = new JsonArray()
        };
        await PerformProfileOperation(FnProfiles.AccountItems, "SetPinnedQuests", content.ToString());
    }

    public static bool HasPinnedQuestUnsafe(string uuid)
    {
        return profileCache[FnProfiles.AccountItems]
            ["profileChanges"][0]["profile"]["stats"]["attributes"]["client_settings"]["pinnedQuestInstances"]
            .AsArray()
            .Select(q=>q.ToString())
            .Contains(uuid);
    }
    public static async Task<ProfileItemHandle> RerollQuest(string uuid)
    {
        GD.Print("rerolling quest " + uuid);
        JsonObject content = new()
        {
            ["questId"] = uuid
        };
        await PerformProfileOperation(FnProfiles.AccountItems, "FortRerollDailyQuest", content.ToString());
        return null; //TODO: return handle of new profile item
    }

    public static bool CanRerollQuestUnsafe()
    {
        return profileCache[FnProfiles.AccountItems]
            ["profileChanges"][0]["profile"]["stats"]["attributes"]["quest_manager"]["dailyQuestRerolls"].GetValue<int>() > 0;
    }

    public static async Task<JsonObject> GetProfile(string profileId, bool forceRefresh = false)
    {
        if (forceRefresh && validProfiles.Contains(profileId))
            validProfiles.Remove(profileId);
        if (!validProfiles.Contains(profileId))
            GD.Print("profile invalid");
        await PerformProfileOperation(profileId, "QueryProfile");
        return profileCache[profileId];
    }

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
    public static async Task<JsonObject> PerformProfileOperation(string profileId, string operation, string content = "{}")
    {
        await profileOperationSephamore.WaitAsync();
        try
        {
            return await PerformProfileOperationUnsafe(profileId, operation, content);
        }
        finally
        {
            profileOperationSephamore.Release();
        }
    }



    public static async Task<JsonObject> PerformProfileOperationUnsafe(string profileId, string operation, string content = "{}")
    {
        //if this profile has been accessed between sending the query and getting past the sephamore, return the profile
        if (operation == "QueryProfile" && validProfiles.Contains(profileId) && profileCache.ContainsKey(profileId) && profileCache[profileId] is JsonObject cachedProfile)
            return cachedProfile;

        GD.Print("requestingProfile");
        GD.Print(operation);
        GD.Print(profileId);
        GD.Print(operation == "QueryProfile");
        GD.Print(validProfiles.Contains(profileId));
        GD.Print(profileCache.ContainsKey(profileId));

        if (!await LoginRequests.TryLogin())
            return null;

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
            return profileCache[profileId];
        }

        JsonObject changelog = null;
        if (profileCache.ContainsKey(profileId) && profileCache[profileId] is not null)
            changelog = await GenerateChangelog(profileCache[profileId], result);
        else
            GD.Print("no cached profile");

        profileCache[profileId] = result;
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
        if (!validProfiles.Contains(profileId))
            validProfiles.Add(profileId);
        if (!validProfiles.Contains(profileId))
            GD.Print("profile invalid WJHAR");

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
                    GD.Print($"uncached item changed {{type:{changeType}, id:{uuid}");
                    continue;
                }
                ProfileItemId profileItem = new(profile, uuid);
                switch (changeType)
                {
                    case "itemAdded":
                        GD.Print("ADDED: " + uuid);
                        profileCache[profile]["profileChanges"][0]["profile"]["items"][uuid] = change["item"].Reserialise();
                        OnItemAdded?.Invoke(profileItem);
                        break;
                    case "itemRemoved":
                        GD.Print("REMOVED: " + uuid);
                        profileCache[profile]["profileChanges"][0]["profile"]["items"].AsObject().Remove(uuid);
                        OnItemRemoved?.Invoke(profileItem);
                        break;
                    case "statChanged":
                        break;
                    case "itemQuantityChanged":
                        profileCache[profile]["profileChanges"][0]["profile"]["items"][uuid]["quantity"] = change["quantity"].Reserialise();
                        GD.Print("CHANGED (quantity): " + uuid);
                        OnItemUpdated?.Invoke(profileItem);
                        break;
                    case "itemAttrChanged":
                        //TODO: apply change to profile
                        GD.Print("CHANGED (attribute): " + uuid + "[" + change["attributeName"] +"]");
                        profileCache[profile]["profileChanges"][0]["profile"]["items"][uuid][change["attributeName"].ToString()] = change["attributeValue"].Reserialise();
                        OnItemUpdated?.Invoke(profileItem);
                        break;
                    case "itemFullyChanged"://custom one for handling generated changelogs
                        profileCache[profile]["profileChanges"][0]["profile"]["items"][uuid] = change["item"].Reserialise();
                        GD.Print("CHANGED (full): " + uuid);
                        OnItemUpdated?.Invoke(profileItem);
                        break;
                }
            }
        }
    }
}

//listens to addition and remove events of a profile
//when created, will do a full scan and run the OnAdded event for every matched item
public class ProfileListener
{
    public event Action<ProfileItemHandle> OnAdded;
    public event Action<ProfileItemHandle> OnUpdated;
    public event Action<ProfileItemHandle> OnRemoved;
    public readonly string profile;
    public readonly ProfileRequests.ProfileItemPredicate predicate;
    readonly List<ProfileItemHandle> items = new();

    public static async Task<ProfileListener> CreateListener(string profile, string type)
    {
        ProfileRequests.ProfileItemPredicate predicate = kvp => kvp.Value["templateId"]?.ToString()?.StartsWith(type) ?? false;
        return new(profile, predicate, await ProfileRequests.GetProfileItems(profile, predicate));
    }

    public static async Task<ProfileListener> CreateListener(string profile, ProfileRequests.ProfileItemPredicate predicate)
    {
        return new(profile, predicate, await ProfileRequests.GetProfileItems(profile, predicate));
    }

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

    ~ProfileListener()
    {
        Unlink();
    }

    public void Unlink()
    {
        ProfileRequests.OnItemAdded -= ItemAddedDetector;
        ProfileRequests.OnItemUpdated -= ItemUpdatedDetector;
        ProfileRequests.OnItemRemoved -= ItemRemovalDetector;
        foreach (var item in items)
        {
            item.Unlink();
        }
    }

    public ProfileItemHandle[] Items => items.ToArray();

    bool debug = false;
    public void EnableDebug() => debug = true;

    void ItemAddedDetector(ProfileItemId profileItem)
    {
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

public class ProfileItemHandle
{
    public event Action<ProfileItemHandle> OnChanged;
    public event Action<ProfileItemHandle> OnRemoved;
    public ProfileItemId profileItem { get; private set; }
    public bool isValid { get; private set; } = true;

    public static async Task<ProfileItemHandle> CreateHandle(ProfileItemId profileItem)
    {
        if((await ProfileRequests.GetProfileItemInstance(profileItem)) is not null)
            return new ProfileItemHandle(profileItem);
        return new ProfileItemHandle();
    }
    public static ProfileItemHandle CreateHandleUnsafe(ProfileItemId profileItem) =>
        new(profileItem);

    public ProfileItemHandle()
    {
        isValid = false;
    }

    ProfileItemHandle(ProfileItemId profileItem)
    {
        this.profileItem = profileItem;
        ProfileRequests.OnItemUpdated += ItemChangeDetector;
        ProfileRequests.OnItemRemoved += ItemRemovalDetector;
    }

    ~ProfileItemHandle()
    {
        Unlink();
    }

    public void Unlink()
    {
        isValid = false;
        profileItem = new();
        ProfileRequests.OnItemUpdated -= ItemChangeDetector;
        ProfileRequests.OnItemRemoved -= ItemRemovalDetector;
    }

    public async Task ReplaceWith(ProfileItemId profileItem)
    {
        if (this.profileItem == profileItem)
            return;
        if ((await ProfileRequests.GetProfileItemInstance(profileItem)) is not null)
        {
            this.profileItem = profileItem;
            isValid = true;
            OnChanged?.Invoke(this);
        }
    }

    public bool IsMatch(string uuid)
    {
        return !isValid || profileItem.uuid == uuid;
    }

    void ItemChangeDetector(ProfileItemId profileItem)
    {
        if (this.profileItem == profileItem)
            OnChanged?.Invoke(this);
    }

    void ItemRemovalDetector(ProfileItemId profileItem)
    {
        if (this.profileItem == profileItem)
        {
            isValid = false;
            OnRemoved?.Invoke(this);
        }
    }

    public async Task<JsonObject> GetItem() => isValid ? await ProfileRequests.GetProfileItemInstance(profileItem) : null;
    public JsonObject GetItemUnsafe() => isValid ? ProfileRequests.GetCachedProfileItemInstance(profileItem) : null;
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
