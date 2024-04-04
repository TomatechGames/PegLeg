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

static class ProfileRequests
{
    static readonly Dictionary<string, JsonObject> profileCache = new();
    static readonly List<string> validProfiles = new();

    static DataTableCurve homebaseRatingCurve;
    static DataTable survivorRatingDataTable = new("res://External/DataTables/SurvivorItemRating.json");
    //TODO: rework to support getting ratings of any item
    public static float GetSurvivorRating(JsonObject itemInstance)=>
        survivorRatingDataTable[itemInstance.GetTemplate()["RatingLookup"].ToString()]
                .Sample(itemInstance["attributes"]["level"].GetValue<float>());

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

    public static DataTableCurve GetHomebaseRatingCurve()
    {
        if (homebaseRatingCurve is not null)
            return homebaseRatingCurve;

        using FileAccess missionFile = FileAccess.Open("res://External/DataTables/HomebaseRatingMapping.json", FileAccess.ModeFlags.Read);
        var curveJson = JsonNode.Parse(missionFile.GetAsText())[0]["Rows"]["UIMonsterRating"].AsObject();
        homebaseRatingCurve = new(curveJson);
        return homebaseRatingCurve;
    }

    static readonly Dictionary<string, string> synergyMap = new()
    {
        ["IsDoctor"] = "squad_attribute_medicine_emtsquad",
        ["IsTrainer"] = "squad_attribute_medicine_trainingteam",
        ["IsSoldier"] = "squad_attribute_arms_fireteamalpha",
        ["IsGadgeteer"] = "squad_attribute_scavenging_gadgeteers",
        ["IsEngineer"] = "squad_attribute_synthesis_corpsofengineering",
        ["IsMartialArtist"] = "squad_attribute_arms_closeassaultsquad",
        ["IsExplorer"] = "squad_attribute_scavenging_scoutingparty",
        ["IsInventor"] = "squad_attribute_synthesis_thethinktank",
    };

    public struct FORTStats
    {
        public float fortitude;
        public float offense;
        public float resistance;
        public float technology;
        public FORTStats(float fortitude, float offense, float resistance, float technology)
        {
            this.fortitude = fortitude;
            this.offense = offense;
            this.resistance = resistance;
            this.technology = technology;
        }
    }
    static FORTStats? currentFortStats = null;
    public static async Task<FORTStats> GetCurrentFortStats()
    {
        if(currentFortStats.HasValue)
            return currentFortStats.Value;

        var profileStats = (await GetProfile(FnProfiles.AccountItems))["profileChanges"][0]["profile"]["stats"]["attributes"]["research_levels"];
        var profileStatAndWorkerItems = await GetProfileItems(FnProfiles.AccountItems, item =>
            item.Value["templateId"].ToString().StartsWith("Stat") ||
            (
                item.Value["templateId"].ToString().StartsWith("Worker") &&
                item.Value["attributes"].AsObject().ContainsKey("squad_id")
            )
        );
        int LookupStatItem(string statId) =>
            profileStatAndWorkerItems.First(
                item => item.Value["templateId"].ToString() == statId
            ).Value["quantity"].GetValue<int>();

        //TODO: move this into a purpose-built SurvivorRequests class, and cache survivor squads
        float LookupWorkers(string squadId)
        {
            float summedValue = 0;
            string personalityTarget = "";
            string leaderRarity = "";

            var matchingWorkers = profileStatAndWorkerItems.Where(item => item.Value["attributes"].AsObject().ContainsKey("squad_id") && item.Value["attributes"]["squad_id"].ToString() == squadId);

            var leadSurvivor = matchingWorkers.FirstOrDefault(item => item.Value["templateId"].ToString().Contains("manager"), new(null, null));
            if (leadSurvivor.Key is not null)
            {
                float thisRating = GetSurvivorRating(leadSurvivor.Value.AsObject());

                bool synergyMatch = synergyMap[leadSurvivor.Value["attributes"]["managerSynergy"].ToString().Split(".")[^1]] == squadId;

                if (synergyMatch)
                    thisRating *= 2;

                summedValue += thisRating;

                personalityTarget = leadSurvivor.Value["attributes"]["personality"].ToString();
                leaderRarity = leadSurvivor.Value.AsObject().GetTemplate()["Rarity"].ToString();
            }

            foreach (var item in matchingWorkers)
            {
                if (item.Key == leadSurvivor.Key)
                    continue;
                float thisRating = GetSurvivorRating(item.Value.AsObject());

                if (item.Value["attributes"]["personality"].ToString() == personalityTarget)
                {
                    thisRating += leaderRarity switch
                    {
                        "Mythic" => 8,
                        "Legendary" => 8,
                        "Epic" => 5,
                        "Rare" => 4,
                        "Uncommon" => 3,
                        "Common" => 2,
                        _ => 0
                    };
                }
                else if (leaderRarity == "Mythic" && thisRating >= 2)
                {
                    thisRating -= 2;
                }
                summedValue += thisRating;
            }

            return summedValue;
        }
        //+ profileStats["fortitude"].GetValue<int>()
        float fortitude = LookupStatItem("Stat:fortitude") + LookupStatItem("Stat:fortitude_team") + LookupWorkers("squad_attribute_medicine_trainingteam") + LookupWorkers("squad_attribute_medicine_emtsquad");
        float offense = LookupStatItem("Stat:offense") + LookupStatItem("Stat:offense_team") + LookupWorkers("squad_attribute_arms_fireteamalpha") + LookupWorkers("squad_attribute_arms_closeassaultsquad");
        float resistance = LookupStatItem("Stat:resistance") + LookupStatItem("Stat:resistance_team") + LookupWorkers("squad_attribute_scavenging_scoutingparty") + LookupWorkers("squad_attribute_scavenging_gadgeteers");
        float technology = LookupStatItem("Stat:technology") + LookupStatItem("Stat:technology_team") + LookupWorkers("squad_attribute_synthesis_corpsofengineering") + LookupWorkers("squad_attribute_synthesis_thethinktank");

        currentFortStats = new(fortitude, offense, resistance, technology);
        return currentFortStats.Value;
    }

    public static async Task<float> GetHomebasePowerLevel()
    {
        var stats = await GetCurrentFortStats();
        
        var homebaseRatingKey = 4 * (stats.fortitude + stats.offense + stats.resistance + stats.technology);

        return GetHomebaseRatingCurve().Sample(homebaseRatingKey);
    }

    public static async Task<int> GetSumOfProfileItems(string profileID, string type) =>
        (await GetProfileItems(profileID, item => item.Value["templateId"].ToString().StartsWith(type))).Select(kvp => kvp.Value["quantity"].GetValue<int>()).Sum();

    public static async Task<JsonObject> GetProfileItems(string profileID, string type) =>
        await GetProfileItems(profileID, item => item.Value["templateId"].ToString().StartsWith(type));

    public static async Task<JsonObject> GetProfileItems(string profileID, Func<KeyValuePair<string,JsonObject>, bool> predicate)
    {
        var matchedItems = 
            (await GetProfile(profileID))
            ["profileChanges"][0]["profile"]["items"]
            .AsObject()
            .Select(kvp=>KeyValuePair.Create(kvp.Key,kvp.Value.AsObject()))
            .Where(predicate);
        JsonObject result = new();
        foreach (var item in matchedItems)
            result[item.Key] = JsonNode.Parse(item.Value.ToString());
        return result;
    }

    public static JsonObject GetCachedProfileItemInstance(string profileID, string uuid) =>
        profileCache[profileID]["profileChanges"][0]["profile"]["items"][uuid]?.AsObject();

    public static async Task<JsonObject> GetProfileItemInstance(string profileID, string uuid) =>
        (await GetProfile(profileID))["profileChanges"][0]["profile"]["items"][uuid].AsObject();

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

public static async Task<JsonObject> GetOrderCounts(OrderRange range)
    {
        var orderRange = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"][OrderRangeToAttribute(range)];
        var lastIntervalTime = DateTime.Parse(orderRange["lastInterval"].ToString(), null, DateTimeStyles.RoundtripKind);
        if (lastIntervalTime != DateTime.UtcNow.Date)
            return null;
        return orderRange["purchaseList"].AsObject();
    }
    public static async Task<JsonObject> GetFufillmentCounts()
    {
        var attributes = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        return attributes["in_app_purchases"]["fufillmentCounts"].AsObject();
    }

    public static async Task<string> GetSACCode()
    {
        var attributes = (await GetProfile(FnProfiles.Common))["profileChanges"][0]["profile"]["stats"]["attributes"];
        var lastSetTime = DateTime.Parse(attributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        bool isExpired = (DateTime.UtcNow - lastSetTime).Days > 13;
        return attributes["mtx_affiliate"] + (isExpired ? " (Expired)" : "");
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

    public static async Task<JsonObject> GetProfile(string profileId, bool forceRefresh = false)
    {
        if (forceRefresh && validProfiles.Contains(profileId))
            validProfiles.Remove(profileId);

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
            //if this profile has been accessed between sending the query and getting past the sephamore, return the profile
            if (operation == "QueryProfile" && profileCache.ContainsKey(profileId) && profileCache[profileId] is not null && validProfiles.Contains(profileId))
                return profileCache[profileId];

            if (profileId == FnProfiles.AccountItems)
                currentFortStats = null;//TODO: this is messy

            if (!await LoginRequests.WaitForLogin())
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

            return profileCache[profileId];
        }
        finally
        {
            profileOperationSephamore.Release();
        }
    }

    const int operationsPer10ms = 50;
    static async Task<JsonObject> GenerateChangelog(JsonObject oldProfile, JsonObject newProfile)
    {
        var oldItems = oldProfile["profileChanges"][0]["profile"]["items"].AsObject();
        var newItems = newProfile["profileChanges"][0]["profile"]["items"].AsObject();

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

    public static event Action<string, string> OnItemAdded;
    public static event Action<string, string> OnItemUpdated;
    public static event Action<string, string> OnItemRemoved;
    static void HandleChanges(JsonArray multiUpdate)
    {
        foreach (var profileUpdate in multiUpdate)
        {
            string profileId = profileUpdate["profileId"].ToString();
            foreach (var change in profileUpdate["profileChanges"].AsArray())
            {
                string changeType = change["changeType"].ToString();
                string itemId = change["itemId"]?.ToString();
                switch (changeType)
                {
                    case "itemAdded":
                        GD.Print("ADDED: " + itemId);
                        profileCache[profileId]["profileChanges"][0]["profile"]["items"][itemId] = change["item"].Reserialise();
                        OnItemAdded?.Invoke(profileId, itemId);
                        break;
                    case "itemRemoved":
                        GD.Print("REMOVED: " + itemId);
                        profileCache[profileId]["profileChanges"][0]["profile"]["items"].AsObject().Remove(itemId);
                        OnItemRemoved?.Invoke(profileId, itemId);
                        break;
                    case "statChanged":
                        break;
                    case "itemQuantityChanged":
                        profileCache[profileId]["profileChanges"][0]["profile"]["items"][itemId]["quantity"] = change["quantity"].Reserialise();
                        GD.Print("CHANGED (quantity): " + itemId);
                        OnItemUpdated?.Invoke(profileId, itemId);
                        break;
                    case "itemAttrChanged":
                        //TODO: apply change to profile
                        GD.Print("CHANGED (attribute): " + itemId + "[" + change["attributeName"] +"]");
                        profileCache[profileId]["profileChanges"][0]["profile"]["items"][itemId][change["attributeName"].ToString()] = change["attributeValue"].Reserialise();
                        OnItemUpdated?.Invoke(profileId, itemId);
                        break;
                    case "itemFullyChanged"://custom one for handling generated changelogs
                        profileCache[profileId]["profileChanges"][0]["profile"]["items"][itemId] = change["item"].Reserialise();
                        GD.Print("CHANGED (full): " + itemId);
                        OnItemUpdated?.Invoke(profileId, itemId);
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
    public event Action<ProfileItemHandle> OnRemoved; // only sends UUID
    public readonly string profile;
    public readonly Func<KeyValuePair<string, JsonObject>,bool> predicate;
    readonly List<ProfileItemHandle> items = new();

    public static async Task<ProfileListener> CreateListener(string profile, string type)
    {
        Func<KeyValuePair<string, JsonObject>, bool> predicate = kvp => kvp.Value["templateId"].ToString().StartsWith(type);
        return new(profile, predicate, await ProfileRequests.GetProfileItems(profile, predicate));
    }

    public static async Task<ProfileListener> CreateListener(string profile, Func<KeyValuePair<string, JsonObject>, bool> predicate)
    {
        return new(profile, predicate, await ProfileRequests.GetProfileItems(profile, predicate));
    }

    ProfileListener(string profile, Func<KeyValuePair<string, JsonObject>, bool> predicate, JsonObject existingItems)
    {
        this.profile = profile;
        this.predicate = predicate;
        ProfileRequests.OnItemAdded += ItemAddedDetector;
        ProfileRequests.OnItemRemoved += ItemRemovalDetector;

        foreach (var existingItem in existingItems)
        {
            items.Add(ProfileItemHandle.CreateHandleUnsafe(profile, existingItem.Key));
        }
    }

    public ProfileItemHandle[] Items => items.ToArray();

    void ItemAddedDetector(string profile, string uuid)
    {
        var addedItem = ProfileRequests.GetCachedProfileItemInstance(profile, uuid);
        addedItem.GetTemplate();
        //GD.Print($"checking if item ({uuid}) is a match...");
        if (predicate(new(uuid, addedItem)))
        {
            //GD.Print($"item ({uuid}) is valid");
            var handle = ProfileItemHandle.CreateHandleUnsafe(profile, uuid);
            items.Add(handle);
            OnAdded?.Invoke(handle);
        }
    }

    void ItemRemovalDetector(string profile, string uuid)
    {
        var handle = items.FirstOrDefault(i=>i.IsMatch(uuid));
        if (handle is not null)
        {
            OnRemoved?.Invoke(handle);
            items.Remove(handle);
        }
    }
}

public class ProfileItemHandle
{
    public event Action<ProfileItemHandle> OnChanged;
    public event Action<ProfileItemHandle> OnRemoved;
    public string profile { get; private set; }
    public string uuid { get; private set; }
    bool isValid = true;

    public static async Task<ProfileItemHandle> CreateHandle(string profile, string uuid)
    {
        if((await ProfileRequests.GetProfileItemInstance(profile, uuid)) is not null)
            return new ProfileItemHandle(profile, uuid);
        return new ProfileItemHandle();
    }
    public static ProfileItemHandle CreateHandleUnsafe(string profile, string uuid) =>
        new(profile, uuid);

    ProfileItemHandle()
    {
        isValid = false;
    }

    ProfileItemHandle(string profile, string uuid)
    {
        this.profile = profile;
        this.uuid = uuid;
        ProfileRequests.OnItemUpdated += ItemChangeDetector;
        ProfileRequests.OnItemRemoved += ItemRemovalDetector;
    }

    ~ProfileItemHandle()
    {
        ProfileRequests.OnItemUpdated -= ItemChangeDetector;
        ProfileRequests.OnItemRemoved -= ItemRemovalDetector;
    }

    public async Task ReplaceWith(string profile, string uuid)
    {
        if (this.profile == profile && this.uuid == uuid)
            return;
        if ((await ProfileRequests.GetProfileItemInstance(profile, uuid)) is not null)
        {
            this.profile = profile;
            this.uuid = uuid;
            isValid = true;
            OnChanged?.Invoke(this);
        }
    }

    public bool IsMatch(string uuid)
    {
        return !isValid || this.uuid == uuid;
    }

    void ItemChangeDetector(string profile, string uuid)
    {
        if (this.profile==profile && this.uuid==uuid)
            OnChanged?.Invoke(this);
    }

    void ItemRemovalDetector(string profile, string uuid)
    {
        if (this.profile == profile && this.uuid == uuid)
        {
            isValid = false;
            OnRemoved?.Invoke(this);
        }
    }

    public async Task<JsonObject> GetItem() => isValid ? await ProfileRequests.GetProfileItemInstance(profile, uuid) : null;
    public JsonObject GetItemUnsafe() => isValid ? ProfileRequests.GetCachedProfileItemInstance(profile, uuid) : null;
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
