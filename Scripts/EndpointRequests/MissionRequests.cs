using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using static FNAssetData;

static class MissionRequests
{
    const string missionCacheSavePath = "user://missions.json";
    static JsonObject missionsCache;
    static JsonObject difficultyTable;

    const string difficultyGrowthTablePath = "res://External/DataTables/GameDifficultyGrowthBounds.json";

    public static bool MissionsRequireUpdate()
    {
        if (missionsCache is null)
            return true;
        var refreshTime = DateTime.Parse(missionsCache["availableUntil"].ToString(), null, DateTimeStyles.RoundtripKind);
        return DateTime.UtcNow.CompareTo(refreshTime) >= 0;
    }

    static Task<JsonObject> activeMissionRequest = null;
    public static async Task<JsonObject> GetMissions(bool forceRefresh = false)
    {
        if (activeMissionRequest is not null && activeMissionRequest.IsCompleted)
            activeMissionRequest = null;

        if (forceRefresh)
        {
            GD.Print("forcing refresh");
            missionsCache = null;
        }
        else if (missionsCache is null && FileAccess.FileExists(missionCacheSavePath))
        {
            //load from file
            using FileAccess missionFile = FileAccess.Open(missionCacheSavePath, FileAccess.ModeFlags.Read);
            missionsCache = JsonNode.Parse(missionFile.GetAsText()).AsObject();
            Debug.WriteLine("mission file loaded");
        }

        if (missionsCache is not null)
        {
            var refreshTime = DateTime.Parse(missionsCache["availableUntil"].ToString(), null, DateTimeStyles.RoundtripKind);
            if (DateTime.UtcNow.CompareTo(refreshTime) >= 0)
                missionsCache = null;
        }

        if (missionsCache is null)
        {
            GD.Print("requesting missions");
            activeMissionRequest ??= RequestMissions();
            await Task.WhenAny(activeMissionRequest);
        }

        //filter
        //JsonObject filtered = FilterMissions(missionsCache, searchTerm, zoneFilter, requireAllSearchTerms);
        return missionsCache;
    }

    static async Task<JsonObject> RequestMissions()
    {
        if (!await LoginRequests.WaitForLogin())
            return null;
        Debug.WriteLine("retrieving missions from epic...");
        JsonNode fullMissions = await Helpers.MakeRequest(
                HttpMethod.Get,
                FNEndpoints.gameEndpoint,
                "fortnite/api/game/v2/world/info",
                "",
                LoginRequests.AccountAuthHeader
            );
        missionsCache = SimplifyMissions(fullMissions);
        //save to file
        using FileAccess missionFile = FileAccess.Open(missionCacheSavePath, FileAccess.ModeFlags.Write);
        missionFile.StoreString(missionsCache.ToString());
        missionFile.Flush();
        return missionsCache;
    }

    static JsonObject LoadDifficultyTable()
    {
        var difficultyFile = FileAccess.Open(difficultyGrowthTablePath, FileAccess.ModeFlags.Read);
        return JsonNode.Parse(difficultyFile.GetAsText())[0]["Rows"].AsObject();
    }

    static JsonObject SimplifyMissions(JsonNode rootNode)
    {
        difficultyTable ??= LoadDifficultyTable();

        //Theatres
        List<string> allowedTheatreIDs = new();

        //base game theatres
        allowedTheatreIDs.AddRange(new string[]{
            "33A2311D4AE64B361CCE27BC9F313C8B",
            "D477605B4FA48648107B649CE97FCF27",
            "E6ECBD064B153234656CB4BDE6743870",
            "D9A801C5444D1C74D1B7DAB5C7C12C5B"
        });

        //ventures theatre
        var venturesTheatre = rootNode["theaters"]
            .AsArray()
            .FirstOrDefault(t => t["displayName"]?["en"]?.ToString().Contains("Venture") == true, null);
        if (venturesTheatre != null)
            allowedTheatreIDs.Add(venturesTheatre["uniqueId"].ToString());

        string refreshDateTime = null;
        List<JsonNode> missionResultList = new();
        foreach (var theatreID in allowedTheatreIDs)
        {
            var theatre = rootNode["theaters"].AsArray().First(t => t["uniqueId"].ToString() == theatreID);
            if (theatre == null)
                continue;

            string theatreName = theatre["displayName"]["en"].ToString();
            char theatreCategory = theatreName switch
            {
                "Stonewood" => 's',
                "Plankerton" => 'p',
                "Canny Valley" => 'c',
                "Twine Peaks" => 't',
                _ => 'v'
            };

            //Missions
            JsonArray missionArray = rootNode["missions"].AsArray().FirstOrDefault(t => t["theaterId"].ToString() == theatreID)["availableMissions"].AsArray();

            //Mission Alerts (indexed by Tile Index, as that is the common factor between missions and mission alerts)
            Dictionary<int, JsonNode> missionAlertDict = rootNode["missionAlerts"].AsArray().FirstOrDefault(t => t["theaterId"].ToString() == theatreID)["availableMissionAlerts"].AsArray().ToDictionary(n => n["tileIndex"].GetValue<int>());

            //resolve power level ints to sort them later
            Dictionary<int, TreeItem> powerLevelFilters = new();
            foreach (var missionObj in missionArray)
            {
                //get refresh time if we havent already
                refreshDateTime ??= missionObj["availableUntil"]?.ToString();

                //establish mission details
                string missionGenName = missionObj["missionGenerator"].ToString().Split(".")[^1];
                var missionData = GetDisplayDataForMissionGenerator(missionGenName);

                string powerLevelRow = missionObj["missionDifficultyInfo"]["rowName"].ToString();
                int powerLevel = difficultyTable[powerLevelRow]["RecommendedRating"].GetValue<int>();

                int tileIndex = missionObj["tileIndex"].GetValue<int>();

                //skip Homebase and Story missions
                if (missionData.iconPath.EndsWith("T-Icon-Story-128.png") || missionData.iconPath.EndsWith("T-Icon-Outpost-128.png"))
                    continue;

                //debug: only show invalid missions
                // if(missionData.IsValid)
                // 	continue;
                JsonObject missionResult = new()
                {
                    ["generatorID"] = missionGenName,
                    ["powerLevel"] = powerLevel,
                    ["theatreName"] = theatreName,
                    ["theatreCat"] = theatreCategory,
                    ["zoneTheme"] = theatre["tiles"][tileIndex]["zoneTheme"].ToString().Split(".")[^1]
                };


                JsonObject missionRewardsObject = new();
                foreach (JsonNode rewardNode in missionObj["missionRewards"]["items"].AsArray())
                {
                    int nodeValue = rewardNode["quantity"].GetValue<int>();
                    if (missionRewardsObject.ContainsKey(rewardNode["itemType"].ToString()))
                        nodeValue += missionRewardsObject[rewardNode["itemType"].ToString()].GetValue<int>();
                    missionRewardsObject[rewardNode["itemType"].ToString()] = nodeValue;
                }
                missionResult["rewards"] = missionRewardsObject;

                if (missionAlertDict.ContainsKey(tileIndex))
                {
                    //parse mission alert
                    JsonObject alertObj = missionAlertDict[tileIndex].AsObject();
                    JsonObject alertRewardsObject = new();
                    foreach (JsonNode rewardNode in alertObj["missionAlertRewards"]["items"].AsArray())
                    {
                        alertRewardsObject[rewardNode["itemType"].ToString()] = rewardNode["quantity"].GetValue<int>();
                    }
                    JsonArray modifierArray = new();
                    if (alertObj.ContainsKey("missionAlertModifiers"))
                        foreach (JsonNode modifierNode in alertObj["missionAlertModifiers"]["items"].AsArray())
                        {
                            modifierArray.Add(modifierNode["itemType"].ToString());
                        }
                    missionResult["missionAlert"] = new JsonObject
                    {
                        ["rewards"] = alertRewardsObject,
                        ["modifiers"] = modifierArray
                    };
                }

                missionResultList.Add(missionResult);
            }
        }

        var orderedMissions = missionResultList.OrderBy(n => n["powerLevel"].GetValue<int>());
        JsonObject simplifiedRoot = new()
        {
            ["missions"] = JsonNode.Parse(JsonSerializer.Serialize(orderedMissions)),
            ["availableUntil"] = refreshDateTime
        };

        return simplifiedRoot;
    }

    static JsonObject FilterMissions(JsonNode rootNode, string searchText, string zoneFilterText, bool requireAllSearchTerms)
    {
        if(string.IsNullOrWhiteSpace(searchText) && string.IsNullOrWhiteSpace(zoneFilterText))
            return rootNode.AsObject();

        List<JsonNode> filteredMissions = new();
        string[] searchTerms = searchText.ToLower().Split(' ');
        char[] zoneFilter = zoneFilterText.ToCharArray();

        Func<string, bool> anySearchMethod = e => searchTerms.Any(s => e.ToLower().Contains(e));
        Func<string, bool> allSearchMethod = e => searchTerms.All(s => e.ToLower().Contains(e));

        foreach (var missionNode in rootNode["missions"].AsArray())
        {
            if (zoneFilter.Length != 0 && !zoneFilter.Contains(missionNode["theatreCat"].GetValue<char>()))
                continue;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                filteredMissions.Add(missionNode);
                continue;
            }

            bool searchMatchFound = false;
            bool searchExcluderFound = false;

            if (missionNode["rewards"].AsObject().Any(e => requireAllSearchTerms ? allSearchMethod(e.ToString()) : anySearchMethod(e.ToString())))
                searchMatchFound = true;
            else
                searchExcluderFound = true;

            if (missionNode.AsObject().ContainsKey("missionAlert"))
            {
                if (missionNode["missionAlert"]["rewards"].AsObject().Any(e => requireAllSearchTerms ? allSearchMethod(e.ToString()) : anySearchMethod(e.ToString())))
                    searchMatchFound = true;
                else
                    searchExcluderFound = true;

                if (missionNode["missionAlert"]["modifiers"].AsArray().Any(e => requireAllSearchTerms ? allSearchMethod(e.ToString()) : anySearchMethod(e.ToString())))
                    searchMatchFound = true;
                else
                    searchExcluderFound = true;
            }

            bool includeResult = searchMatchFound;

            if (includeResult)
                filteredMissions.Add(missionNode);
        }

        JsonObject filteredRoot = new()
        {
            ["missions"] = JsonNode.Parse(JsonSerializer.Serialize(filteredMissions)),
            ["availableUntil"] = rootNode["availableUntil"].ToString()
        };

        return filteredRoot;
    }
}
