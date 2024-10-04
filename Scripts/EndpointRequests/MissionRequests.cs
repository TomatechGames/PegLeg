using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;

static class MissionRequests
{
    const string missionCacheSavePath = "user://missions.json";
    static JsonObject missionsCache;
    const int MissionVersion = 3;

    public static bool MissionsRequireUpdate()
    {
        if (missionsCache is null)
            return true;
        if ((missionsCache["version"]?.GetValue<int>() ?? 0) < MissionVersion)
            return true;
        var refreshTime = DateTime.Parse(missionsCache["expiryDate"]?.ToString() ?? "1987-07-22T06:00:00.000Z", CultureInfo.InvariantCulture);
        GD.Print($"MissionRefresh = {refreshTime} ({DateTime.UtcNow})");
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

        if (MissionsRequireUpdate())
        {
            GD.Print("missions out of date or bad version");
            missionsCache = null;
        }

        if (missionsCache is null)
        {
            GD.Print("requesting missions");
            if(activeMissionRequest is null)
            {
                activeMissionRequest ??= RequestMissions();
                await activeMissionRequest;
            }
            else
            {
                await activeMissionRequest.WaitAsync(TimeSpan.FromMinutes(1));
            }
        }
        activeMissionRequest = null;

        //GD.Print("max rewards: " + missionsCache.SelectMany(kvp => kvp.Value.AsArray().Select(m => m["missionRewards"].AsArray().Count)).Max());
        //GD.Print("max alert rewards: " + missionsCache.SelectMany(kvp => kvp.Value.AsArray().Select(m => m["missionAlert"]?["rewards"].AsArray().Count ?? 0)).Max());
        return missionsCache;
    }

    static async Task<JsonObject> RequestMissions()
    {
        if (!await LoginRequests.TryLogin())
            return null;
        GD.Print("retrieving missions from epic...");
        JsonNode fullMissions = await Helpers.MakeRequest(
                HttpMethod.Get,
                FNEndpoints.gameEndpoint,
                "fortnite/api/game/v2/world/info",
                "",
                LoginRequests.AccountAuthHeader
            );
        try
        {
            missionsCache = SimplifyMissions(fullMissions);
        }
        catch (InvalidOperationException invalidEx)
        {
            GD.PushWarning(invalidEx.Message);
            var innerEx = invalidEx.InnerException;
            GD.PushWarning(invalidEx.StackTrace);
            while (innerEx is not null)
            {
                GD.PushWarning("Caused By: "+ innerEx.Message);
                innerEx = innerEx.InnerException;
            }
        }
        GD.Print(fullMissions.ToString()[..350] + "...");
        GD.Print(missionsCache.ToString()[..350] + "...");
        //save to file
        using FileAccess missionFile = FileAccess.Open(missionCacheSavePath, FileAccess.ModeFlags.Write);
        missionFile.StoreString(missionsCache.ToString());
        missionFile.Flush();
        return missionsCache;
    }

    static Dictionary<string, string> rewardPackAssociations = new()
    {
        ["CardPack:zcp_reagent_c_t04_\\w*"] = "AccountResource:reagent_c_t04",
        ["CardPack:zcp_reagent_c_t03_\\w*"] = "AccountResource:reagent_c_t03",
        ["CardPack:zcp_reagent_c_t02_\\w*"] = "AccountResource:reagent_c_t02",
        ["CardPack:zcp_reagent_c_t01_\\w*"] = "AccountResource:reagent_c_t01",

        ["CardPack:zcp_reagent_alteration_upgrade_sr_\\w*"] = "AccountResource:reagent_alteration_upgrade_sr",
        ["CardPack:zcp_reagent_alteration_upgrade_vr_\\w*"] = "AccountResource:reagent_alteration_upgrade_vr",
        ["CardPack:zcp_reagent_alteration_upgrade_r_\\w*"] = "AccountResource:reagent_alteration_upgrade_r",
        ["CardPack:zcp_reagent_alteration_upgrade_uc_\\w*"] = "AccountResource:reagent_alteration_upgrade_uc",
        ["CardPack:zcp_reagent_alteration_generic_\\w*"] = "AccountResource:reagent_alteration_generic",

        ["CardPack:zcp_phoenixxp_t\\d\\d"] = "AccountResource:phoenixxp",
        ["CardPack:zcp_personnelxp_t\\d\\d"] = "AccountResource:personnelxp",
        ["CardPack:zcp_heroxp_t\\d\\d"] = "AccountResource:heroxp",

        ["CardPack:zcp_ore_copper_\\w*"] = "Ingredient:ingredient_ore_copper",
        ["CardPack:zcp_ore_silver_\\w*"] = "Ingredient:ingredient_ore_silver",
        ["CardPack:zcp_ore_malachite_\\w*"] = "Ingredient:ingredient_ore_malachite",
        ["CardPack:zcp_ore_obsidian_\\w*"] = "Ingredient:ingredient_ore_obsidian",
        ["CardPack:zcp_ore_brightcore_\\w*"] = "Ingredient:ingredient_ore_brightcore",

        ["CardPack:zcp_crystal_quartz_\\w*"] = "Ingredient:ingredient_crystal_quartz",
        ["CardPack:zcp_crystal_shadowshard_\\w*"] = "Ingredient:ingredient_crystal_shadowshard",
        ["CardPack:zcp_crystal_sunbeam_\\w*"] = "Ingredient:ingredient_crystal_sunbeam",
    };

    static JsonObject SimplifyMissions(JsonNode rootNode)
    {
        //Theatres
        List<string> allowedTheaterIDs = new()
        {
            "33A2311D4AE64B361CCE27BC9F313C8B",
            "D477605B4FA48648107B649CE97FCF27",
            "E6ECBD064B153234656CB4BDE6743870",
            "D9A801C5444D1C74D1B7DAB5C7C12C5B"
        };

        //ventures theatre
        var venturesTheater = rootNode["theaters"]
            .AsArray()
            .FirstOrDefault(t => t["missionRewardNamedWeightsRowName"]?.ToString() == "Theater.Phoenix");
        if (venturesTheater is not null)
            allowedTheaterIDs.Add(venturesTheater["uniqueId"].ToString());

        BanjoAssets.TryGetSource("MissionGen", out var missionGenLookup);
        BanjoAssets.TryGetSource("DifficultyInfo", out var difficultyInfoLookup);
        BanjoAssets.TryGetSource("ZoneTheme", out var zoneThemeLookup);

        JsonArray allMissions = rootNode["missions"].AsArray();
        JsonArray allMissionAlerts = rootNode["missionAlerts"].AsArray();

        List<JsonObject> missionList = new();

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

            foreach (var missionObj in theaterMissions)
            {
                //establish mission details
                string missionGen = missionObj["missionGenerator"].ToString();
                missionObj["missionGenerator"] = missionGenLookup[missionGen].Reserialise();

                //skip Homebase and Story missions
                string iconPath = missionObj["missionGenerator"]["ImagePaths"]["Icon"].ToString();
                if (iconPath.EndsWith("T-Icon-Story-128.png") || iconPath.EndsWith("T-Icon-Outpost-128.png"))
                    continue;

                string dificultyRow = missionObj["missionDifficultyInfo"]["rowName"].ToString();
                missionObj["missionDifficultyInfo"] = difficultyInfoLookup[dificultyRow].Reserialise();

                missionObj["theaterCat"] = theaterCat;

                missionObj["missionRewards"] = new JsonArray(
                        missionObj["missionRewards"]["items"].AsArray()
                        .GroupBy(r => r["itemType"].ToString())
                        .Select(g =>new JsonObject()
                        {
                            ["itemType"] = g.Key,
                            ["quantity"] = g.Select(r => r["quantity"].GetValue<int>()).Sum()
                        })
                        .ToArray()
                    );
                foreach (var rewardNode in missionObj["missionRewards"].AsArray())
                {
                    bool hasEquiv = false;
                    foreach (var equivelent in rewardPackAssociations)
                    {
                        if(Regex.Match(rewardNode["itemType"].ToString(), equivelent.Key).Success)
                        {
                            rewardNode["equivelent"] = equivelent.Value;
                            rewardNode["equivelentTemplate"] = BanjoAssets.TryGetTemplate(equivelent.Value).Reserialise();
                            rewardNode["searchTags"] = BanjoAssets.GenerateItemTemplateSearchTags(equivelent.Value);
                            rewardNode["searchTags"].AsArray().Add("Bundle");
                            hasEquiv = true;
                            break;
                        }
                    }
                    if (!hasEquiv)
                    {
                        rewardNode.GenerateItemSearchTags();
                    }
                }

                int tileIndex = missionObj["tileIndex"].GetValue<int>();
                missionObj["tile"] = missionTiles[tileIndex].AsObject().Reserialise();
                string zoneTheme = missionObj["tile"]["zoneTheme"].ToString();
                missionObj["tile"]["zoneTheme"] = zoneThemeLookup[zoneTheme].Reserialise();

                //search tags
                List<string> tags = new()
                {
                    theaterName,
                    missionObj["missionGenerator"]["DisplayName"].ToString(),
                    missionObj["tile"]["zoneTheme"]["DisplayName"].ToString(),
                };
                if (theaterCat == "v")
                    tags.Add("ventures");

                if (missionAlertDict.ContainsKey(tileIndex))
                {
                    //parse mission alert
                    JsonObject alertObj = missionAlertDict[tileIndex].AsObject();
                    JsonArray modifierArray = new();
                    if (alertObj.ContainsKey("missionAlertModifiers"))
                    {
                        foreach (JsonNode modifierNode in alertObj["missionAlertModifiers"]["items"].AsArray())
                        {
                            modifierArray.Add(modifierNode["itemType"].ToString());
                            tags.Add(modifierNode.GetTemplate()["DisplayName"].ToString());
                        }
                    }
                    JsonArray rewardArray = alertObj["missionAlertRewards"]["items"].AsArray().Reserialise();
                    foreach (var rewardNode in rewardArray)
                    {
                        rewardNode.GenerateItemSearchTags();
                    }
                    missionObj["missionAlert"] = new JsonObject
                    {
                        ["rewards"] = rewardArray,
                        ["modifiers"] = modifierArray
                    };
                }

                missionObj["searchTags"] = new JsonArray(tags.Select(t => (JsonNode)t).ToArray());
                missionObj["powerLevel"] = missionObj["missionDifficultyInfo"]["RecommendedRating"].GetValue<int>();

                missionList.Add(missionObj.AsObject().Reserialise());
            }
        }

        //var orderedMissions = missionList.OrderBy(n => n["missionDifficultyInfo"]["RecommendedRating"].GetValue<int>());
        JsonObject toReturn = new()
        {
            ["version"] = MissionVersion,
            ["expiryDate"] = missionList[0]["availableUntil"].ToString()[..^1], //the Z messes with daylight savings time
            ["missions"] = new JsonArray(
                    missionList
                    .OrderBy(n => n["powerLevel"].GetValue<int>())
                    .ToArray()
                )
        };
        //GD.Print(toReturn.ToString()[..150]);
        return toReturn;
    }

    /* Old filtering system
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
    */
}
