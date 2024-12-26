using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Godot;

static class MissionRequests
{
    const string missionCacheSavePath = "user://missions.json";
    static uint missionSample;
    static DateTime missionReset;
    static FnMission[] missions = null;
    const int MissionVersion = 4;

    public static async Task<bool> CheckForMissionChanges()
    {
        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return false;

        JsonNode fullMissions = await Helpers.MakeRequest(
                HttpMethod.Get,
                FnEndpoints.gameEndpoint,
                "fortnite/api/game/v2/world/info",
                "",
                account.AuthHeader
            );

        var missionHash = fullMissions["missionAlerts"].ToString().Hash();
        if (missionSample == 0)
        {
            missionSample = missionHash;
            return false;
        }
        if (missionSample != missionHash)
        {
            GD.Print(missionSample + " >> " + missionHash);
            missionSample = missionHash;
            missions = null;
            return true;
        }
        return false;
    }

    public static bool MissionsEmptyOrOutdated()
    {
        if (missions is null)
            return true;
        return DateTime.UtcNow.CompareTo(missionReset) >= 0;
    }

    static SemaphoreSlim missionSephamore = new(1);
    static bool forceQueued = true;
    static bool isBeingForced = true;
    public static async Task<FnMission[]> GetMissions(bool forceRefresh = false)
    {
        if (isBeingForced)
            forceRefresh = false;
        else if (forceRefresh)
        {
            forceQueued = true;
            forceRefresh = false;
        }
        await missionSephamore.WaitAsync();
        try
        {
            if (forceQueued)
                forceRefresh = true;
            JsonObject missionData = null;
            if (forceRefresh)
            {
                GD.Print("forcing refresh");
                missions = null;
            }
            else if (missions is not null && DateTime.UtcNow.CompareTo(missionReset) >= 0)
            {
                GD.Print("missions expired");
                missions = null;
            }
            else if (missions is null && FileAccess.FileExists(missionCacheSavePath))
            {
                //load from file
                using FileAccess missionFile = FileAccess.Open(missionCacheSavePath, FileAccess.ModeFlags.Read);
                missionData = JsonNode.Parse(missionFile.GetAsText()).AsObject();
                if(missionData["version"]?.GetValue<int>()== MissionVersion)
                {
                    missionSample = missionData["sample"]?.AsValue().TryGetValue(out uint sampleVal) ?? false ? sampleVal : 0;
                    missionReset = DateTime.Parse(missionData["expiryDate"]?.ToString(), CultureInfo.InvariantCulture);
                    Debug.WriteLine("mission file loaded");
                }
                else
                {
                    missionData = null;
                    Debug.WriteLine("mission file version mismatch");
                }
            }

            if(missions is not null)
                return missions;

            if (missionData is null)
            {
                GD.Print("requesting missions");
                missionData = await RequestMissions();
            }

            //GD.Print("max rewards: " + missionsCache.SelectMany(kvp => kvp.Value.AsArray().Select(m => m["missionRewards"].AsArray().Count)).Max());
            //GD.Print("max alert rewards: " + missionsCache.SelectMany(kvp => kvp.Value.AsArray().Select(m => m["missionAlert"]?["rewards"].AsArray().Count ?? 0)).Max());
            return missions = GenerateMissions(missionData);
        }
        finally
        {
            forceQueued = false;
            missionSephamore.Release();
        }
    }

    static async Task<JsonObject> RequestMissions()
    {
        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return null;

        GD.Print("retrieving missions from epic...");
        JsonNode missionData = await Helpers.MakeRequest(
                HttpMethod.Get,
                FnEndpoints.gameEndpoint,
                "fortnite/api/game/v2/world/info",
                "",
                account.AuthHeader
            );
        if(missionData["errorMessage"] is not null)
        {
            GD.Print("Error: "+missionData.ToString());
            return null;
        }
        var alerts = missionData["missionAlerts"];
        missionData["version"] = MissionVersion;
        missionData["expiryDate"] = alerts[0]["nextRefresh"].ToString()[..^1]; //the Z messes with daylight savings time
        missionData["sample"] = missionSample = alerts.ToString().Hash();
        //GD.Print(fullMissions.ToString()[..350] + "...");
        //GD.Print(missionsCache.ToString()[..350] + "...");
        //save to file

        using FileAccess missionFile = FileAccess.Open(missionCacheSavePath, FileAccess.ModeFlags.Write);
        missionFile.StoreString(missionData.ToString());
        missionFile.Flush();
        return missionData.AsObject();
    }


    static FnMission[] GenerateMissions(JsonNode rootNode)
    {
        //Theaters
        List<string> allowedTheaterIDs = new()
        {
            "33A2311D4AE64B361CCE27BC9F313C8B",
            "D477605B4FA48648107B649CE97FCF27",
            "E6ECBD064B153234656CB4BDE6743870",
            "D9A801C5444D1C74D1B7DAB5C7C12C5B"
        };

        //ventures theater
        var venturesTheater = rootNode["theaters"]
            .AsArray()
            .FirstOrDefault(t => t["missionRewardNamedWeightsRowName"]?.ToString() == "Theater.Phoenix");
        if (venturesTheater is not null)
            allowedTheaterIDs.Add(venturesTheater["uniqueId"].ToString());

        BanjoAssets.TryGetSource("ZoneTheme", out var zoneThemeLookup);

        JsonArray allMissions = rootNode["missions"].AsArray();
        JsonArray allMissionAlerts = rootNode["missionAlerts"].AsArray();

        List<FnMission> missionList = new();

        foreach (var theaterID in allowedTheaterIDs)
        {
            var theater = rootNode["theaters"].AsArray().First(t => t["uniqueId"].ToString() == theaterID);
            if (theater is null)
                continue;

            string theaterName = theater["displayName"]["en"].ToString();
            string theaterCat = theaterName switch
            {
                "Stonewood" => "s",
                "Plankerton" => "p",
                "Canny Valley" => "c",
                "Twine Peaks" => "t",
                _ => "v"
            };
            bool isVentures = theaterCat == "v";

            //Missions
            JsonArray theaterMissions = allMissions.FirstOrDefault(t => t["theaterId"].ToString() == theaterID)["availableMissions"].AsArray();

            //Mission Alerts (indexed by Tile Index, as that is the common factor between missions and mission alerts)
            var missionAlertDict = allMissionAlerts
                .FirstOrDefault(t => t["theaterId"].ToString() == theaterID)
                ["availableMissionAlerts"]
                .AsArray()
                .ToDictionary(n => n["tileIndex"].GetValue<int>());

            var missionTiles = theater["tiles"].AsArray();
            List<JsonObject> missionsToDetach = new();

            foreach (var missionObj in theaterMissions)
            {
                missionsToDetach.Add(missionObj.AsObject());

                string missionGen = missionObj["missionGenerator"].ToString();

                int tileIndex = missionObj["tileIndex"].GetValue<int>();
                var tileData = missionTiles[tileIndex].AsObject();
                missionTiles.Remove(tileIndex);

                JsonObject alertObj = null;
                if (missionAlertDict.ContainsKey(tileIndex))
                {
                    alertObj = missionAlertDict[tileIndex].AsObject();
                    missionAlertDict.Remove(tileIndex);
                }

                //skip Homebase and quest exclusive missions
                if (
                    missionGen.Contains("_TheOutpost_") ||
                    tileData["requirements"]["activeQuestDefinitions"].AsArray().Count > 0 //||
                    //tileData["requirements"]["eventFlag"].ToString() != ""
                    )
                    continue;

                missionObj["theaterCat"] = theaterCat;

                missionList.Add(new(missionObj.AsObject(), alertObj, tileData));
            }

            //detach from original json
            foreach (var mission in missionsToDetach)
            {
                theaterMissions.Remove(mission);
            }
        }
        return missionList.ToArray();
    }
}

public class FnMission
{
    public int powerLevel { get; private set; }
    public int theaterIdx { get; private set; }
    public string theaterCat { get; private set; }

    public JsonObject missionData { get; private set; }
    public JsonObject alertData { get; private set; }
    public JsonObject tileData { get; private set; }
    public JsonObject difficultyInfo { get; private set; }

    public GameItem missionGenerator { get; private set; }
    public GameItem zoneTheme { get; private set; }
    public Texture2D backgroundTexture { get; private set; }

    public GameItem[] rewardItems { get; private set; }
    public GameItem[] alertModifiers { get; private set; }
    public GameItem[] alertRewardItems { get; private set; }

    public IEnumerable<GameItem> allItems => alertRewardItems?.Union(rewardItems) ?? rewardItems;

    public FnMission(JsonObject missionData, JsonObject alertData, JsonObject tileData)
    {
        this.missionData = missionData;
        this.alertData = alertData;
        this.tileData = tileData;
        difficultyInfo = BanjoAssets.Lookup("DifficultyInfo", missionData["missionDifficultyInfo"]["rowName"].ToString());

        missionGenerator = GameItemTemplate.Lookup("MissionGen", missionData["missionGenerator"].ToString()).CreateInstance();
        zoneTheme = GameItemTemplate.Lookup("ZoneTheme", tileData["zoneTheme"].ToString()).CreateInstance();

        powerLevel = difficultyInfo?["ReccomendedRating"]?.GetValue<int>() ?? 0;
        if (powerLevel == 0)
            GD.Print(missionData["missionDifficultyInfo"]["rowName"].ToString() + " :: " + difficultyInfo);
        theaterCat = missionData["theaterCat"].ToString();
        theaterIdx = theaterCat switch
        {
            "s" => 0,
            "p" => 1,
            "c" => 2,
            "t" => 3,
            "v" => 4,
            _ => 0
        };

        missionGenerator.GetTexture(FnItemTextureType.Icon);
        backgroundTexture = missionGenerator.GetTexture(FnItemTextureType.LoadingScreen) ?? zoneTheme.GetTexture(FnItemTextureType.LoadingScreen);

        List<GameItem> rewardItemList = new();
        foreach (var itemData in missionData["missionRewards"]["items"].AsArray())
        {
            GameItem item = new(null, null, itemData.AsObject());
            item.SetSeenLocal();
            item.GetTexture();
            item.GenerateSearchTags();
            rewardItemList.Add(item);
        }
        rewardItems = rewardItemList.ToArray();

        if (alertData is not null)
        {
            List<GameItem> alertModifierList = new();
            if (alertData["missionAlertModifiers"]?["items"]?.AsArray() is JsonArray modifierData)
            {
                foreach (var itemData in modifierData)
                {
                    GameItem modifier = new(null, null, itemData.AsObject());
                    modifier.SetSeenLocal();
                    modifier.GetTexture();
                    modifier.GenerateSearchTags();
                    alertModifierList.Add(modifier);
                }
            }
            alertModifiers = alertModifierList.ToArray();

            List<GameItem> alertRewardItemList = new();
            if (alertData["missionAlertRewards"]?["items"]?.AsArray() is JsonArray rewardData)
            {
                foreach (var itemData in rewardData)
                {
                    GameItem item = new(null, null, itemData.AsObject());
                    item.SetSeenLocal();
                    item.GetTexture();
                    item.GenerateSearchTags();
                    alertRewardItemList.Add(item);
                }
            }
            alertRewardItems = alertRewardItemList.ToArray();
        }
    }
}
