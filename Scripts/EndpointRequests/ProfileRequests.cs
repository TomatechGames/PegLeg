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
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Reflection.Metadata;


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

public class GameAccount
{
    const string accountDataPath = "user://accounts";
    static readonly AesContext deviceDetailEncryptor = new();

    static string GetDeviceDetailsKey()
    {
        string deviceDetailKey = System.Environment.MachineName + "custard";
        int baseLength = deviceDetailKey.Length;
        for (int i = 0; i < 32 - baseLength; i++)
        {
            deviceDetailKey += "custard"[i % 7];
        }
        return deviceDetailKey[..32];
    }

    static byte[] EncryptDeviceDetails(JsonObject fromDetails)
    {
        //stringify and add padding
        string deviceDetalsString = fromDetails.ToString();
        int remainder = deviceDetalsString.Length % 16;

        for (int i = 0; i < 16 - remainder; i++)
        {
            deviceDetalsString += "^";
        }

        string deviceDetailKey = GetDeviceDetailsKey();

        //encrypt
        deviceDetailEncryptor.Start(AesContext.Mode.EcbEncrypt, deviceDetailKey.ToUtf8Buffer());
        byte[] encryptedDetails = deviceDetailEncryptor.Update(deviceDetalsString.ToUtf8Buffer());
        deviceDetailEncryptor.Finish();
        return encryptedDetails;
    }

    static JsonObject DecryptDeviceDetails(byte[] encryptedDetails)
    {
        if (encryptedDetails is null)
            return null;
        GD.Print("DDLen: " + encryptedDetails.Length);
        GD.Print("DDLen16: " + encryptedDetails.Length % 16);
        if (encryptedDetails.Length % 16 != 0)
            return null;

        string deviceDetailKey = GetDeviceDetailsKey();
        GD.Print("DDKey: "+deviceDetailKey);
        if (deviceDetailKey.Length != 32)
            return null;

        //decrypt
        deviceDetailEncryptor.Start(AesContext.Mode.EcbDecrypt, deviceDetailKey.ToUtf8Buffer());
        byte[] decryptedDetails = deviceDetailEncryptor.Update(encryptedDetails);
        deviceDetailEncryptor.Finish();
        string deviceDetalsString = Encoding.UTF8.GetString(decryptedDetails, 0, decryptedDetails.Length);


        //remove padding and convert to json
        while (deviceDetalsString.EndsWith('^'))
        {
            deviceDetalsString = deviceDetalsString[..^1];
        }

        JsonObject resultDetails = null;
        try
        {
            resultDetails = JsonNode.Parse(deviceDetalsString).AsObject();
        }
        catch (Exception) { }
        return resultDetails;
    }

    public static GameAccount[] GetStoredAccounts()
    {
        if(!DirAccess.DirExistsAbsolute(accountDataPath))
            return Array.Empty<GameAccount>();
        using var accountDir = DirAccess.Open(accountDataPath);
        return accountDir.GetFiles().Where(f => !f.Contains('.')).Select(f => GetOrCreateAccount(f)).ToArray();
    }

    static readonly Dictionary<string, GameAccount> gameAccountCache = new();
    public static GameAccount GetOrCreateAccount(string accountId) => gameAccountCache.ContainsKey(accountId) ? gameAccountCache[accountId] : gameAccountCache[accountId] = new (accountId);
    static GameAccount _activeAccount;
    public static GameAccount activeAccount => _activeAccount ?? new(null);
    public static event Action<GameAccount> ActiveAccountChangedEarly;
    public static event Action<GameAccount> ActiveAccountChanged;

    public static async Task<bool> SetActiveAccount(string accountId)
    {
        if (!gameAccountCache.ContainsKey(accountId))
            return false;
        var account = gameAccountCache[accountId];
        if (await account?.Authenticate())
        {
            _activeAccount = account;
            ActiveAccountChangedEarly?.Invoke(account);
            ActiveAccountChanged?.Invoke(account);
            return true;
        }
        return false;
    }

    public static void LoginToAccount(JsonObject accountAuthResponse)
    {
        if (accountAuthResponse["account_id"]?.ToString() is not string accountId)
            return;
        var account = GetOrCreateAccount(accountId);
        account.SetAuthentication(accountAuthResponse);
    }

    public GameAccount(string accountId)
    {
        this.accountId = accountId;
    }

    public bool isValid => !string.IsNullOrWhiteSpace(accountId);
    public bool isOwned => !AuthTokenExpired || GetLocalData("DeviceDetails") is not null;
    public string accountId { get; private set; }

    Dictionary<string, GameProfile> profiles = new();

    public GameProfile this[string profileId] => GetProfile(profileId);
    public GameProfile GetProfile(string profileId) => profiles.ContainsKey(profileId ?? "") ? profiles[profileId ?? ""] : profiles[profileId ?? ""] = new(this, profileId ?? "");
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

    string authToken;
    int authExpiresAt = -999;
    string refreshToken;
    int refreshExpiresAt = -999;
    AuthenticationHeaderValue accountAuthHeader;
    //fails 60 seconds before it would actualy expire
    public bool AuthTokenExpired => authExpiresAt <= (Time.GetTicksMsec() * 0.001) - 60;
    bool RefreshTokenExpired => refreshExpiresAt <= (Time.GetTicksMsec() * 0.001) - 10;
    public AuthenticationHeaderValue AuthHeader => accountAuthHeader;
    public async Task<bool> Authenticate(bool loadingOverlay = false)
    {
        if (!AuthTokenExpired)
            return true;

        try
        {
            if (loadingOverlay)
                LoadingOverlay.AddLoadingKey("Authenticate");

            if (!RefreshTokenExpired)
            {
                var refreshAuth = await GameClient.LoginWithRefreshToken(refreshToken);

                if (refreshAuth is not null && refreshAuth["errorMessage"] is null)
                {
                    SetAuthentication(refreshAuth);
                    return true;
                }

                GD.Print(refreshAuth?["errorMessage"].ToString());
            }

            var dd = GetLocalData("DeviceDetails")?.AsArray().Select(n => n.GetValue<byte>()).ToArray();
            JsonNode deviceAuth = await GameClient.LoginWithDeviceAuth(DecryptDeviceDetails(dd));

            if (deviceAuth is not null && deviceAuth["errorMessage"] is null)
            {
                SetAuthentication(deviceAuth);
                return true;
            }
            GD.Print(deviceAuth?["errorMessage"].ToString());

            return false;
        }
        finally
        {
            if (loadingOverlay)
                LoadingOverlay.RemoveLoadingKey("Authenticate");
        }
    }

    public SemaphoreSlim profileOperationSephamore { get; private set; } = new(1);

    public async Task<JsonArray> PurchaseOffer(GameOffer offer, int purchaseQuantity = 1)
    {
        JsonObject shopRequestBody = new()
        {
            ["offerId"] = offer.OfferId,
            ["purchaseQuantity"] = purchaseQuantity,
            ["currency"] = offer["prices"][0]["currencyType"].ToString(),
            ["currencySubType"] = offer["prices"][0]["currencySubType"].ToString(),
            ["expectedTotalPrice"] = (await offer.GetPersonalPrice(true, true)).quantity * purchaseQuantity,
            ["gameContext"] = "Pegleg",
        };
        return await GetProfile(FnProfileTypes.Common).PerformOperation("PurchaseCatalogEntry", shopRequestBody.ToString());
    }

    public async Task<bool> SetAsActiveAccount() => await SetActiveAccount(accountId);

    void SetAuthentication(JsonNode accountAuthResponse)
    {
        if (accountId is null)
            return;
        authToken = accountAuthResponse["access_token"].ToString();
        accountAuthHeader = new("Bearer", authToken);
        authExpiresAt = Mathf.FloorToInt(Time.GetTicksMsec() * 0.001) + accountAuthResponse["expires_in"].GetValue<int>();
        if (accountAuthResponse["refresh_expires"]?.GetValue<int>() is int refreshExpires)
        {
            refreshExpiresAt = Mathf.FloorToInt(Time.GetTicksMsec() * 0.001) + refreshExpires;
            refreshToken = accountAuthResponse["refresh_token"].ToString();
        }
    }

    public async Task SaveDeviceDetails()
    {
        if (!await Authenticate() || GetLocalData("DeviceDetails") is not null)
            return;

        //generate device details
        JsonObject deviceDetails = (await Helpers.MakeRequest(
            HttpMethod.Post,
            FnEndpoints.loginEndpoint,
            $"account/api/public/account/{accountId}/deviceAuth",
            "",
            AuthHeader,
            ""
        ))?.AsObject();

        SetLocalData("DeviceDetails", new JsonArray(EncryptDeviceDetails(deviceDetails).Select(b => (JsonNode)b).ToArray()));
    }

    public async Task RemoveDeviceDetails()
    {
        if(!await Authenticate())
        {
            GD.Print("Authentication failed, aborting device detail deletion");
            return;
        }
        var dd = GetLocalData("DeviceDetails")?.AsArray().Select(n => n.GetValue<byte>()).ToArray();
        if (DecryptDeviceDetails(dd) is JsonObject deviceDetails)
        {
            //tell epic we're not using the device any more. probably unneccecary, but its common courtesy
            await Helpers.MakeRequest(
                HttpMethod.Delete,
                FnEndpoints.loginEndpoint,
                $"account/api/public/account/{accountId}/deviceAuth/{deviceDetails["deviceId"]}",
                "",
                AuthHeader,
                ""
            );
            ClearLocalData("DeviceDetails");
        }
    }

    JsonObject localData;
    void LoadLocalData()
    {
        if (!isValid || !DirAccess.DirExistsAbsolute(accountDataPath))
        {
            GD.Print("invalid or no folder");
            localData = new();
            return;
        }
        using FileAccess localDataFile = FileAccess.Open($"{accountDataPath}/{accountId}", FileAccess.ModeFlags.Read);
        if (localDataFile is null)
        {
            GD.Print("no file");
            localData = new();
            return;
        }
        var localDataString = localDataFile.GetAsText();

        try
        {
            localData = JsonNode.Parse(localDataString).AsObject();
            return;
        }
        catch (Exception) 
        {
            GD.Print("Warning: Failed to load local data, data may be overwritten");
        }
        localData = new();
    }

    public JsonNode GetLocalData(string key)
    {
        if (localData is null)
            LoadLocalData();
        return localData[key];
    }

    public void ClearLocalData(string key) => SetLocalData(key, null);
    public void SetLocalData(string key, JsonNode value)
    {
        if (localData is null)
            LoadLocalData();
        localData[key] = value;

        if (!isValid)
            return;

        if (!DirAccess.DirExistsAbsolute(accountDataPath))
            DirAccess.MakeDirAbsolute(accountDataPath);
        using FileAccess localDataFile = FileAccess.Open($"{accountDataPath}/{accountId}", FileAccess.ModeFlags.Write);
        localDataFile?.StoreString(localData.ToString());
    }


    public event Action<GameAccount> OnFortStatsChanged;
    FORTStats? fortStats;
    bool fortStatsDirty = true;
    public FORTStats FortStats => GetFORTStats();
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

    public async Task<float> GetSurvivorBonus(string bonusID, int perSquadRequirement = 2, float boostBase = 5)
    {
        if(!await Authenticate())
            return 0f;
        var matchingSurvivors = (await GetProfile(FnProfileTypes.AccountItems).Query()).GetItems("Worker", gameItem =>
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

    public async Task<JsonObject> GetOrderCounts(OrderRange range)
    {
        var commonData = await GetProfile(FnProfileTypes.Common).Query();
        var orderRange = commonData.statAttributes[range.ToAttribute()];
        var lastIntervalTime = DateTime.Parse(orderRange["lastInterval"].ToString(), null, DateTimeStyles.RoundtripKind);
        if (lastIntervalTime != range.ToInterval())
            return null;
        return orderRange["purchaseList"].AsObject();
    }

    public async Task<int> GetPurchaseLimit(GameOffer offer)
    {
        int totalLimit = 999;

        if (offer.DailyLimit != -1)
        {
            int purchaseAmount = (await GetOrderCounts(OrderRange.Daily))?[offer.OfferId]?.GetValue<int>() ?? 0;
            //GD.Print($"Daily Limit: {purchaseAmount}/{dailyLimit}");
            totalLimit = Mathf.Min(totalLimit, offer.DailyLimit - purchaseAmount);
            GD.Print($"EventLimit: ({purchaseAmount}/{offer.DailyLimit})");
        }

        if (totalLimit > 0 && offer.WeeklyLimit != -1)
        {
            int purchaseAmount = (await GetOrderCounts(OrderRange.Weekly))?[offer.OfferId]?.GetValue<int>() ?? 0;
            //GD.Print($"Weekly Limit: {purchaseAmount}/{weeklyLimit}");
            totalLimit = Mathf.Min(totalLimit, offer.WeeklyLimit - purchaseAmount);
            GD.Print($"EventLimit: ({purchaseAmount}/{offer.WeeklyLimit})");
        }

        if (totalLimit > 0 && offer.MonthlyLimit != -1)
        {
            int purchaseAmount = (await GetOrderCounts(OrderRange.Monthly))?[offer.OfferId]?.GetValue<int>() ?? 0;
            //GD.Print($"Monthly Limit: {purchaseAmount}/{monthlyLimit}");
            totalLimit = Mathf.Min(totalLimit, offer.MonthlyLimit - purchaseAmount);
            GD.Print($"EventLimit: ({purchaseAmount}/{offer.MonthlyLimit})");
        }

        if (totalLimit > 0 && offer.EventLimit != -1)
        {
            var commonData = await GetProfile(FnProfileTypes.Common).Query();
            GameItem eventTracker = commonData.GetItems("EventPurchaseTracker", item =>
                    item.attributes?["event_instance_id"]?.ToString() == offer.EventId
                ).FirstOrDefault();

            int purchaseAmount = eventTracker?.attributes?["event_purchases"]?[offer.OfferId]?.GetValue<int>() ?? 0;
            //GD.Print($"Event Limit: {purchaseAmount}/{eventLimit}");
            totalLimit = Mathf.Min(totalLimit, offer.EventLimit - purchaseAmount);
            GD.Print($"EventLimit: ({purchaseAmount}/{offer.EventLimit})");
        }

        return totalLimit;
    }

    public async Task<bool> MatchesFulfillmentRequirements(GameOffer offer)
    {
        if (offer.FulfillmentDenyList.Count > 0)
        {
            var commonData = await GetProfile(FnProfileTypes.Common).Query();
            var fulfillments = commonData.statAttributes["in_app_purchases"]?["fulfillmentCounts"];
            if (offer.FulfillmentDenyList.Any(check => (fulfillments[check.Key]?.GetValue<int>() ?? 0) >= check.Value))
                return false;
        }

        if (offer.FulfillmentRequireList.Count > 0)
        {
            var commonData = await GetProfile(FnProfileTypes.Common).Query();
            var fulfillments = commonData.statAttributes["in_app_purchases"]?["fulfillmentCounts"];
            if (offer.FulfillmentRequireList.Any(check => (fulfillments[check.Key]?.GetValue<int>() ?? 0) < check.Value))
                return false;
        }

        return true;
    }

    public async Task<bool> MatchesItemRequirements(GameOffer offer)
    {
        if (offer.ItemDenyList.Count > 0)
        {
            var athenaItems = await GetProfile(FnProfileTypes.CosmeticInventory).Query();
            if (offer.ItemDenyList.Any(check => (athenaItems.GetItem(check.Key)?.quantity ?? 0) >= check.Value))
                return false;
        }

        return true;
    }

    public async Task<string> GetSACCode(bool addExpiredText = true)
    {
        var commonData = await GetProfile(FnProfileTypes.Common).Query();
        var lastSetTime = DateTime.Parse(commonData.statAttributes["mtx_affiliate_set_time"].ToString(), null, DateTimeStyles.RoundtripKind);
        bool isExpired = (DateTime.UtcNow - lastSetTime).Days > 13;
        return commonData.statAttributes["mtx_affiliate"] + (isExpired && addExpiredText ? " (Expired)" : "");
    }

    public async Task<bool> IsSACExpired() => Mathf.FloorToInt(await GetSACTime()) > 13;

    public async Task<double> GetSACTime()
    {
        var commonData = await GetProfile(FnProfileTypes.Common).Query();
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

    public async Task ClientQuestLogin()
    {
        await GetProfile(FnProfileTypes.AccountItems).PerformOperation("ClientQuestLogin", @"{""streamingAppKey"": """"}");
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

    public event Action<GameProfile> OnStatChanged;
    public event Action<GameItem> OnItemAdded;
    public event Action<GameItem> OnItemUpdated;
    public event Action<GameItem> OnItemRemoved;

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
    public GameItem[] GetTemplateItems(string templateId = null, GameItemPredicate predicate = null)
    {
        string type = templateId.Split(":")[0];
        return GetItems(type, predicate).Where(gameItem => gameItem.templateId==templateId).ToArray();
    }

    public void SendItemUpdate(GameItem item)
    {
        if (item.profile == this)
            OnItemUpdated?.Invoke(item);
    }

    public async Task<GameProfile> Query(bool force=false)
    {
        await account.profileOperationSephamore.WaitAsync();
        try
        {
            if (!hasProfile || force)
            {
                await PerformOperationUnsafe("QueryProfile");
            }
            return this;
        }
        finally
        {
            account.profileOperationSephamore.Release();
        }
    }

    public async Task<JsonArray> PerformOperation(string operation, string content = "{}")
    {
        await account.profileOperationSephamore.WaitAsync();
        try
        {
            return await PerformOperationUnsafe(operation, content);
        }
        finally
        {
            account.profileOperationSephamore.Release();
        }
    }

    async Task<JsonArray> PerformOperationUnsafe(string operation, string content = "{}")
    {
        if (!account.isOwned)
        {
            if (operation == "QueryProfile")
                operation = "QueryPublicProfile";

            if (operation != "QueryPublicProfile")
            {
                GD.Print($"cannot perform \"{operation}\" on unowned profile");
                return null;
            }

            if (profileId != FnProfileTypes.AccountItems && profileId != FnProfileTypes.Common)
            {
                GD.Print($"cannot access unowned profile of type \"{profileId}\"");
                return null;
            }
        }

        AuthenticationHeaderValue authHeader;
        if (!account.isOwned && await GameAccount.activeAccount.Authenticate())
        {
            authHeader = GameAccount.activeAccount.AuthHeader;
        }
        else if (account.isOwned && await account.Authenticate())
        {
            authHeader = account.AuthHeader;
        }
        else
        {
            GD.Print($"Authentication failed");
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
                $"fortnite/api/game/v2/profile/{account.accountId}/{(account.isOwned ? "client" : "public")}/{operation}?profileId={profileId}&rvn=-1"
            )
            {
                Content = jsonContent
            };
        request.Headers.Authorization = authHeader;

        var result = (await Helpers.MakeRequest(FnEndpoints.gameEndpoint, request)).AsObject();
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
            var from = items[itemKey].SimpleRawData.ToString();
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
                    targetItem.NotifyRemoving();
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
                    targetItem.NotifyChanged();
                    OnItemUpdated?.Invoke(targetItem);
                    break;
                case "itemAttrChanged":
                    targetItem = items[uuid];
                    targetItem.attributes[change["attributeName"].ToString()] = change["attributeValue"].Reserialise();
                    GD.Print($"CHANGED (attribute): {uuid}[{change["attributeName"]}] ({targetItem})");
                    targetItem.NotifyChanged();
                    OnItemUpdated?.Invoke(targetItem);
                    break;
                case "itemFullyChanged":
                    //custom one for handling generated changelogs
                    targetItem = items[uuid];
                    targetItem.SetRawData(change["item"].AsObject());
                    GD.Print($"CHANGED (full): {uuid} ({targetItem})");
                    targetItem.NotifyChanged();
                    OnItemUpdated?.Invoke(targetItem);
                    break;
            }
        }
    }
}

public delegate bool GameItemPredicate(GameItem gameItem);

public class GameItem
{
    #region Statics

    static Dictionary<string, string> zcpEquivelents = new()
    {
        ["CardPack:zcp_reagent_c_t04\\w*"] = "AccountResource:reagent_c_t04",
        ["CardPack:zcp_reagent_c_t03\\w*"] = "AccountResource:reagent_c_t03",
        ["CardPack:zcp_reagent_c_t02\\w*"] = "AccountResource:reagent_c_t02",
        ["CardPack:zcp_reagent_c_t01\\w*"] = "AccountResource:reagent_c_t01",

        ["CardPack:zcp_reagent_alteration_upgrade_sr\\w*"] = "AccountResource:reagent_alteration_upgrade_sr",
        ["CardPack:zcp_reagent_alteration_upgrade_vr\\w*"] = "AccountResource:reagent_alteration_upgrade_vr",
        ["CardPack:zcp_reagent_alteration_upgrade_r\\w*"] = "AccountResource:reagent_alteration_upgrade_r",
        ["CardPack:zcp_reagent_alteration_upgrade_uc\\w*"] = "AccountResource:reagent_alteration_upgrade_uc",
        ["CardPack:zcp_reagent_alteration_generic\\w*"] = "AccountResource:reagent_alteration_generic",

        ["CardPack:zcp_phoenixxp\\w*"] = "AccountResource:phoenixxp",
        ["CardPack:zcp_personnelxp\\w*"] = "AccountResource:personnelxp",
        ["CardPack:zcp_heroxp\\w*"] = "AccountResource:heroxp",
        ["CardPack:zcp_schematicxp\\w*"] = "AccountResource:schematicxp",

        ["CardPack:zcp_ore_copper\\w*"] = "Ingredient:ingredient_ore_copper",
        ["CardPack:zcp_ore_silver\\w*"] = "Ingredient:ingredient_ore_silver",
        ["CardPack:zcp_ore_malachite\\w*"] = "Ingredient:ingredient_ore_malachite",
        ["CardPack:zcp_ore_obsidian\\w*"] = "Ingredient:ingredient_ore_obsidian",
        ["CardPack:zcp_ore_brightcore\\w*"] = "Ingredient:ingredient_ore_brightcore",

        ["CardPack:zcp_crystal_quartz\\w*"] = "Ingredient:ingredient_crystal_quartz",
        ["CardPack:zcp_crystal_shadowshard\\w*"] = "Ingredient:ingredient_crystal_shadowshard",
        ["CardPack:zcp_crystal_sunbeam\\w*"] = "Ingredient:ingredient_crystal_sunbeam",

        ["CardPack:zcp_eventscaling\\w*"] = "AccountResource:eventcurrency_scaling",
    };

    static GameItem FindZcpEquivelent(string templateId)
    {
        if (!(templateId?.StartsWith("CardPack:zcp_") ?? false))
            return null;
        foreach (var equivelent in zcpEquivelents)
        {
            if (Regex.Match(templateId, equivelent.Key).Success)
            {
                GameItem equivelentItem = GameItemTemplate.Get(equivelent.Value).CreateInstance();
                equivelentItem.SetSeenLocal();
                equivelentItem.GenerateSearchTags();
                return equivelentItem;
            }
        }
        return null;
    }

    #endregion

    public event Action<GameItem> OnChanged;
    public event Action<GameItem> OnRemoving;
    public event Action<GameItem> OnRemoved;

    public GameItem(GameProfile profile, string uuid, JsonObject rawData)
    {
        this.uuid = uuid;
        this.profile = profile;
        SetRawData(rawData);
    }

    public GameItem(GameItemTemplate template, int quantity, JsonObject attributes = null, GameItem inspectorOverride = null, JsonObject customData = null)
    {
        attemptedTemplateSearch = true;
        _template = template;
        templateId = template?.TemplateId;
        this.quantity = quantity;
        this.attributes = attributes;
        this.customData = customData ?? new();
        this.inspectorOverride = inspectorOverride;
        zcpEquivelent = FindZcpEquivelent(templateId);
        isSeenLocal = true;
    }

    public GameProfile profile { get; private set; }
    public string uuid { get; private set; }

    public GameItem zcpEquivelent { get; private set; }
    public GameItem inspectorOverride { get; private set; }

    bool attemptedTemplateSearch;
    public string templateId { get; private set; }
    public GameItemTemplate template
    {
        get
        {
            if (attemptedTemplateSearch)
                return _template;
            _template ??= GameItemTemplate.Get(templateId);
            attemptedTemplateSearch = true;
            return _template;
        }
    }
    GameItemTemplate _template;

    public JsonObject attributes { get; private set; }
    public JsonObject customData { get; private set; } = new();

    public int quantity { get; private set; }
    public void SetQuantity(int newQuant)
    {
        if (profile != null)
            return;
        quantity = newQuant;
    }

    JsonObject _rawData;
    public JsonObject RawData => _rawData ?? GenerateRawData();
    public JsonObject GenerateRawData() => _rawData = new()
    {
        ["templateId"] = templateId,
        ["attributes"] = attributes.Reserialise(),
        ["quantity"] = quantity,
        ["template"] = template.rawData.Reserialise(),
        ["searchTags"] = template?["searchTags"].Reserialise(),
    };

    public JsonObject SimpleRawData => new()
    {
        ["templateId"] = templateId,
        ["attributes"] = attributes.Reserialise(),
        ["quantity"] = quantity,
    };

    public void SetRawData(JsonObject rawData)
    {
        var newTemplate = rawData["templateId"]?.ToString() ?? rawData["itemType"]?.ToString();
        if (templateId != newTemplate)
        {
            attemptedTemplateSearch = false;
            _template = null;
            templateId = newTemplate;
            zcpEquivelent = FindZcpEquivelent(templateId);
        }
        quantity = rawData["quantity"].GetValue<int>();
        attributes = rawData["attributes"]?.AsObject();
        ResetCachedData();
    }

    void ResetCachedData()
    {
        _rawData = null;
        _rating = null;
        textures.Clear();
    }

    bool isSeenLocal = false;
    public bool IsSeen => isSeenLocal || (attributes?["item_seen"]?.GetValue<bool>() ?? false);
    public GameItem SetSeenLocal(bool newVal = true)
    {
        bool update = IsSeen != newVal;
        isSeenLocal = newVal;
        if (update)
            NotifyChanged();
        return this;
    }

    public GameItem MarkItemSeen()
    {
        SetSeenLocal();
        string content = @$"{{""itemIds"": [""{uuid}""]}}";
        profile.PerformOperation("MarkItemSeen", content).StartTask();
        return this;
    }

    public async Task SetItemRewardNotification(GameAccount account)
    {
        if (profile is not null)
            return;

        if (attributes?.ContainsKey("item_seen") ?? false)
        {
            attributes.Remove("item_seen");
            _rawData = null;
        }

        if (template.Type == "Accolades" || template.Type == "CardPack" || template.Type == "Weapon")
        {
            attributes ??= new JsonObject();
            attributes["item_seen"] = true;
            _rawData = null;
            return;
        }
        if (template.Name == "Campaign_Event_Currency" || template.Name == "Currency_MtxSwap")
        {
            attributes ??= new JsonObject();
            attributes["item_seen"] = true;
            _rawData = null;
            return;
        }

        var accountItems = await account.GetProfile(FnProfileTypes.AccountItems).Query();
        bool exists = accountItems
            .GetItems(template.Type, item => 
                item.template.DisplayName == template.DisplayName && 
                item.template.RarityLevel >= template.RarityLevel)
            .Any();
        if (!exists && template.IsCollectable)
        {
            var collectionBook = await account.GetProfile(template.CollectionProfile).Query();
            exists = collectionBook
            .GetItems(template.Type, item =>
                item.template.DisplayName == template.DisplayName &&
                item.template.RarityLevel >= template.RarityLevel)
            .Any();
        }
        SetSeenLocal(exists);
    }

    public bool? isCollectedCache { get; private set; }
    public async Task<bool> IsCollected()
    {
        if (isCollectedCache is not null)
            return isCollectedCache ?? false;
        await profile.account.GetProfile(template.CollectionProfile).Query();
        return (isCollectedCache = IsCollectedUnsafe()) ?? false;
    }

    bool IsCollectedUnsafe()
    {
        var collectionBook = profile.account.GetProfile(template.CollectionProfile);
        if (template.Type == "Worker")
        {
            if (template.Name.StartsWith("workerhalloween"))
            {
                //with costume party attendees, 3 of each rarity can be collected
                return collectionBook
                    .GetItems("Worker", item => 
                        item.template.Name.StartsWith("workerhalloween") && 
                        item.template.Rarity == template.Rarity)
                    .Length < 3;
            }
            else if(template.SubType is not null)
            {
                //with mythic lead survivors, one of each unique lead can be collected
                if (template.Rarity == "Mythic")
                    return collectionBook
                        .GetItems("Worker", item => item.templateId == templateId)
                        .Any();
                //with regular lead survivors, one of each subtype-rarity combo can be collected
                else
                    return collectionBook
                        .GetItems("Worker", item => 
                            item.template.SubType == template.SubType && 
                            item.template.Rarity == template.Rarity)
                        .Any();
            }
            //with regular survivors, one of personality-rarity combo can be collected
            return collectionBook
                .GetItems("Worker", item => 
                    item.attributes["personality"].ToString() == attributes["personality"].ToString() && 
                    item.template.Rarity == template.Rarity)
                .Any();
        }
        return collectionBook
            .GetItems(template.Type, item => item.templateId == templateId)
            .Any();
    }

    public float GetHeroStat(string stat, int givenLevel = 0, int givenTier = 0)
    {
        if (!BanjoAssets.TryGetSource("HeroStats", out var stats))
            return 0;

        if (givenLevel <= 0)
        {
            givenLevel = attributes?["level"]?.GetValue<int>() ?? 1;
            givenTier = template.Tier;
        }

        string heroStatType = template["HeroStatType"].ToString();
        string heroRarityAndTier = template.GetCompactRarityAndTier(givenTier);
        var statLookup = stats["Types"]?[$"{template.SubType}_{heroStatType}"]?[heroRarityAndTier]?[stat]?.AsObject();
        if (statLookup is null)
            return 0;
        int statKey = Mathf.Clamp(givenLevel - (int)statLookup["FirstLevel"], 0, statLookup["Values"].AsArray().Count - 1);
        return (float)statLookup["Values"][statKey];
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
        if (subLevel < 0)
            return 0;
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

    Dictionary<FnItemTextureType, Texture2D> textures = new();

    public Texture2D GetTexture(FnItemTextureType textureType = FnItemTextureType.Preview) => GetTexture(textureType, BanjoAssets.defaultIcon);
    public Texture2D GetTexture(Texture2D fallbackIcon) => GetTexture(FnItemTextureType.Preview, fallbackIcon);

    const string llamaDefaultPreviewImage = "PinataStandardPack";
    public static readonly Texture2D[] llamaTierIcons = new Texture2D[]
    {
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataStandardPack.png", "Texture2D"),
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataSilver.png", "Texture2D"),
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataGold.png", "Texture2D"),
    };

    public Texture2D GetTexture(FnItemTextureType textureType, Texture2D fallbackIcon)
    {
        if (textures.ContainsKey(textureType))
            return textures[textureType];

        if (textureType == FnItemTextureType.Personality)
            return GetPersonalityTexture(fallbackIcon);

        if (textureType == FnItemTextureType.SetBonus)
            return GetSetBonusTexture(fallbackIcon);

        if (textureType == FnItemTextureType.Preview && GameItemTemplate.Get(attributes?["portrait"]?.ToString()) is GameItemTemplate portraitTemplate)
        {
            var portraitTexture = portraitTemplate.GetTexture(fallbackIcon);
            if (portraitTexture is not null)
                return textures[textureType] = portraitTexture;
        }
        if(template.Type == "CardPack")
        {
            if (textureType == FnItemTextureType.Preview && customData?["llamaTier"]?.GetValue<int>() is int llamaTier)
            {
                string llamaPinataName =
                    (template.TryGetTexturePath(FnItemTextureType.Preview, out var imagePath) ? imagePath : null)
                    ?.ToString().Split("\\")[^1];
                if (llamaPinataName?.StartsWith(llamaDefaultPreviewImage) ?? false)
                    return llamaTierIcons[llamaTier];
            }

            if (attributes?.ContainsKey("options") ?? false)
            {
                if(textureType == FnItemTextureType.Preview)
                    return llamaTierIcons[0];
                if (textureType == FnItemTextureType.PackImage)
                    textureType = FnItemTextureType.Preview;
            }
        }

        return template?.GetTexture(textureType);
    }
    
    Texture2D GetPersonalityTexture(Texture2D fallbackIcon = null)
    {
        if (template.Type != "Worker")
            return fallbackIcon;

        var personalityId = template.Personality ?? attributes?["personality"]?.ToString()?.Split(".")?[^1];

        if (personalityId is not null && BanjoAssets.supplimentaryData.PersonalityIcons.ContainsKey(personalityId))
            return textures[FnItemTextureType.Personality] = BanjoAssets.supplimentaryData.PersonalityIcons[personalityId];

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
                return textures[FnItemTextureType.SetBonus] = BanjoAssets.supplimentaryData.SquadIcons[subType];
        }
        else if(attributes?["set_bonus"]?.ToString()?.Split(".")?[^1] is string setBonus)
        {
            if (BanjoAssets.supplimentaryData.SetBonusIcons.ContainsKey(setBonus))
                return textures[FnItemTextureType.SetBonus] = BanjoAssets.supplimentaryData.SetBonusIcons[setBonus];
        }

        return fallbackIcon;
    }

    public void GenerateSearchTags(bool assumeUncommon = true) =>
        template.GenerateSearchTags(assumeUncommon);

    public override string ToString() => $"{{\n  id:{uuid}\n  template:{templateId}\n  quantity:{quantity}\n  attributes:{attributes}}}";

    public GameItem Clone() => Clone(quantity);
    public GameItem Clone(int quantity) => new(template, quantity, attributes.Reserialise(), profile is null ? inspectorOverride : this);

    public void NotifyChanged()
    {
        ResetCachedData();
        OnChanged?.Invoke(this);
    }

    public void NotifyRemoving() => OnRemoving?.Invoke(this);
    public void DisconnectFromProfile()
    {
        profile = null;
        uuid = null;
        OnRemoved?.Invoke(this);
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
