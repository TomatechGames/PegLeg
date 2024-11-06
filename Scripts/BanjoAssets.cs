using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Godot.RenderingDevice;

public static class BanjoAssets
{
    public const string banjoFolderPath = "res://External/Banjo";

    public static readonly Texture2D defaultIcon = ResourceLoader.Load<Texture2D>("res://Images/InterfaceIcons/T-Icon-Unknown-128.png");
    public static readonly BanjoSuppliments supplimentaryData = ResourceLoader.Load<BanjoSuppliments>("res://banjo_suppliments.tres");
    static readonly Dictionary<string, WeakRef> iconCache = new();

    public static T Reserialise<T>(this T toReserialise) where T : JsonNode =>
        toReserialise is not null ? (T)JsonNode.Parse(toReserialise.ToJsonString()) : null;

    public static async Task GenerateAssets(string gamePath, Func<Task> waitTask = null)
    {
        if (!DirAccess.DirExistsAbsolute(gamePath + "/Content/Paks"))
            return;

        string programFolder = "res://External/BanjoGenerator/Release/net8.0";

        JsonObject appSettingsObj = null;
        using (var appSettingsFile = FileAccess.Open(programFolder + "/appsettings.json", FileAccess.ModeFlags.Read))
        {
            appSettingsObj = JsonNode.Parse(appSettingsFile.GetAsText()).AsObject();
        }

        JsonArray gameDirArray = appSettingsObj["GameFileOptions"]["GameDirectories"].AsArray();
        gameDirArray.Clear();
        gameDirArray.Add(gamePath + "/Content/Paks");

        using (var appSettingsFile = FileAccess.Open(programFolder + "/appsettings.json", FileAccess.ModeFlags.Write))
        {
            appSettingsFile.StoreString(appSettingsObj.ToString());
        }

        //TODO: copy existing json files into a backup folder

        string realProgramFolder = Helpers.ProperlyGlobalisePath(programFolder);

        string generatorProgramPath = realProgramFolder + "/BanjoBotAssets.exe";
        var banjoProcess = Process.Start(new ProcessStartInfo()
        {
            FileName = generatorProgramPath,
            UseShellExecute = true,
            WorkingDirectory = realProgramFolder
        });

        if (waitTask == null)
        {
            await banjoProcess.WaitForExitAsync();
        }
        else
        {
            while (!banjoProcess.HasExited)
            {
                await waitTask();
            }
        }
        PreloadSourcesParalell();
    }

    public static JsonObject CreateTemplate(string name, string description = null, string type = "Custom:item", string iconPath = null)
    {
        var splitType = type.Split(':');
        JsonObject toReturn = new()
        {
            ["DisplayName"] = name,
            ["Type"] = splitType[0],
            ["Name"] = splitType[1],
        };
        if (description is not null)
            toReturn["Description"] = description;
        if (iconPath is not null)
            toReturn["ImagePaths"] = new JsonObject() { ["LargePreview"] = iconPath };
        return toReturn;
    }

    public static bool TryGetTemplate(this JsonNode possibleItemId, out JsonObject itemTemplate)
    {
        itemTemplate = null;
        if (possibleItemId is JsonValue itemIdJson)
        {
            if (TryGetTemplate(itemIdJson.ToString(), out itemTemplate))
            {
                return true;
            }
            //GD.Print("Itemisation failed on id: "+itemIdJson);
        }
        //GD.PushWarning("Itemisation failed on possible id: " + possibleItemId);
        return false;
    }

    public static JsonObject TryGetTemplate(string itemID) => TryGetTemplate(itemID, out var result) ? result : null;
    public static bool TryGetTemplate(string templateId, out JsonObject itemTemplate)
    {
        if (templateId.StartsWith("STWAccoladeReward"))
            templateId = templateId.Replace("STWAccoladeReward:stwaccolade_", "Accolades:accoladeid_stw_");
        var splitItemId = templateId.Split(':');
        if (templateId.Contains(':') && TryGetSource(splitItemId[0], out var source))
        {
            templateId = splitItemId[0] + ":" + splitItemId[1].ToLower();
            itemTemplate = source[templateId]?.AsObject();
            return itemTemplate is not null;
        }
        itemTemplate = null;
        return false;
    }

    static Dictionary<string, Dictionary<string, GameItemTemplate>> newTemplateDict = new();
    public static GameItemTemplate TryGetNewTemplate(string templateId)
    {
        if (templateId.StartsWith("STWAccoladeReward"))
            templateId = templateId.Replace("STWAccoladeReward:stwaccolade_", "Accolades:accoladeid_stw_");

        var splitItemId = templateId.Split(':');

        if (!templateId.Contains(':'))
            return null;

        if (newTemplateDict.ContainsKey(splitItemId[0]) && newTemplateDict[splitItemId[0]].ContainsKey(splitItemId[1]))
            return newTemplateDict[splitItemId[0]][splitItemId[1]];

        templateId = splitItemId[0] + ":" + splitItemId[1].ToLower();
        if (!TryGetSource(splitItemId[0], out var source))
            return null;

        GameItemTemplate newTemplate = source[templateId] is JsonObject templateObj ? new(templateObj) : null;

        if (newTemplate is not null)
        {
            newTemplateDict[splitItemId[0]] ??= new();
            newTemplateDict[splitItemId[0]].Add(splitItemId[1], newTemplate);
        }

        return newTemplate;
    }

    public static GameItemTemplate GetOrCreateNewTemplate(string templateId, Func<GameItemTemplate> constructor)
    {
        if(TryGetNewTemplate(templateId) is GameItemTemplate foundTemplate)
            return foundTemplate;

        var splitItemId = templateId.Split(':');

        if (!templateId.Contains(':'))
            return null;

        GameItemTemplate newTemplate = constructor();

        if (newTemplate is not null)
        {
            newTemplateDict[splitItemId[0]] ??= new();
            newTemplateDict[splitItemId[0]].Add(splitItemId[1], newTemplate);
        }

        return newTemplate;
    }

    public static JsonObject GetTemplate(this JsonNode itemInstance, out JsonObject template) => template = itemInstance.GetTemplate();
    public static JsonObject GetTemplate(this JsonNode itemInstance, bool print = false)
    {
        if (print)
            GD.Print(itemInstance.ToString());
        if (itemInstance is JsonValue val && TryGetTemplate(val, out var varTemplate))
            return varTemplate;

        if (itemInstance is JsonArray)
            return null;

        if (itemInstance is null)
            return null;

        if (itemInstance["template"] is JsonObject cachedTemplate)
            return cachedTemplate;

        if (itemInstance["template"]?.ToString() is string templateID)
            itemInstance["templateId"] = templateID;

        if (itemInstance["itemType"]?.ToString() is string itemType)
            itemInstance["templateId"] = itemType;

        if (itemInstance["templateId"]?.TryGetTemplate(out var template) ?? false)
        {
            itemInstance["template"] = template.Reserialise();
            return itemInstance["template"].AsObject();
        }
        return null;
    }
    public static JsonObject GenerateItemSearchTags(this JsonNode itemInstance, bool force = false)
    {
        if (force)
            itemInstance["searchTags"] = null;
        itemInstance["searchTags"] ??= GenerateItemTemplateSearchTags(itemInstance.GetTemplate());
        return itemInstance.AsObject();
    }
    public static JsonArray GenerateItemTemplateSearchTags(string itemID, bool assumeUncommon = true) =>
        GenerateItemTemplateSearchTags(TryGetTemplate(itemID), assumeUncommon);
    public static JsonArray GenerateItemTemplateSearchTags(JsonNode template, bool assumeUncommon = true)
    {
        List<string> tags = new()
        {
            template["DisplayName"]?.ToString(),
            template["Rarity"]?.ToString() ?? (assumeUncommon ? "Uncommon" : null),
            template["Type"]?.ToString(),
            template["SubType"]?.ToString(),
            template["Category"]?.ToString(),
            template["Personality"]?.ToString()[2..]
        };
        template["RarityLv"] = template.AsObject().GetItemRarity();
        if (tags.Contains("Worker"))
            tags.Add("Survivor");
        var result = new JsonArray(tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => (JsonNode)t).ToArray());
        return result;
    }

    static readonly Dictionary<string, JsonObject> dataSources = new();

    static string[] requiredSources = new string[]
    {
        "Hero",
        "CardPack",
        "Schematic",
        "Defender",
        "Worker",
        "AccountResource",
    };

    public static bool PreloadSourcesParalell()
    {
        string path = Helpers.ProperlyGlobalisePath(banjoFolderPath);
        if (!DirAccess.DirExistsAbsolute(path))
            return false;
        GD.Print(path);
        //return TryLoadJsonFile(path+"/assets.json", out banjoFile);
        DirAccess banjoDir = DirAccess.Open(path);
        var allFiles = banjoDir.GetFiles();
        foreach (var requiredSource in requiredSources)
        {
            if (!allFiles.Any(f => f==$"{requiredSource}.json"))
            {
                GD.Print($"Missing {requiredSource}");
                return false;
            }
        }
        Parallel.ForEach(allFiles, file =>
        {
            file = file.Split("/")[^1][..^5];
            if (TryLoadJsonFile(file, out var json))
            {
                lock (dataSources)
                {
                    dataSources[file] = json;
                }
            }
        });
        return true;
    }

    public static bool TryGetSource(string namedDataSource, out JsonObject source)
    {
        //if (banjoFile.ContainsKey(namedDataSource))
        //    source = banjoFile[namedDataSource].AsObject();
        //else
        //    source = banjoFile["NamedItems"].AsObject();
        //return banjoFile is not null;

        bool exists = dataSources.ContainsKey(namedDataSource);
        if (!exists && TryLoadJsonFile(namedDataSource, out var json))
        {
            dataSources[namedDataSource] = source = json;
            return true;
        }
        source = exists ? dataSources[namedDataSource] : null;
        return exists;
    }

    public static JsonObject[] GetTemplatesFromSource(string namedDataSource, Func<JsonObject, bool> filter = null)
    {
        if (!TryGetSource(namedDataSource, out var source))
            return null;
        filter ??= item => true;
        return source.Select(kvp => kvp.Value.AsObject()).Where(val => filter(val)).ToArray();
    }

    public static string GetIDFromTemplate(this JsonObject template) => template["Type"].ToString() + ":" + template["Name"].ToString().ToLower();

    public static Texture2D GetItemTexture(this JsonObject itemTemplate, ItemTextureType textureType = ItemTextureType.Preview) =>
        itemTemplate.GetItemTexture(defaultIcon, textureType);
    public static Texture2D GetItemTexture(this JsonObject itemTemplate, Texture2D fallbackIcon, ItemTextureType textureType = ItemTextureType.Preview)
    {
        if (itemTemplate is null)
            return fallbackIcon;

        if (textureType == ItemTextureType.Preview && (itemTemplate["attributes"]?["portrait"]?.TryGetTemplate(out var portraitTemplate) ?? false))
        {
            var portraitTexture = portraitTemplate.GetItemTexture(fallbackIcon);
            if (portraitTexture is not null)
                return portraitTexture;
        }

        if (!itemTemplate.ContainsKey("Type"))
            itemTemplate = itemTemplate.GetTemplate();

        if (itemTemplate is null)
            return fallbackIcon;

        if (itemTemplate["Type"]?.ToString() == "Worker" && (itemTemplate["ImagePaths"]?["SmallPreview"]?.ToString().Contains("GenericWorker") ?? false))
        {
            if (itemTemplate.ContainsKey("SubType"))
                return GetSubtypeTexture(itemTemplate["SubType"].ToString(), fallbackIcon);
            else
                return GetSubtypeTexture("Survivor", fallbackIcon);
        }

        if (itemTemplate.TryGetTexturePathFromTemplate(textureType, out var texturePath))
        {
            var tex = GetReservedTexture(texturePath);
            if(tex is null)
            {
                GD.PushWarning($"Null texture in: {itemTemplate["Type"]}:{itemTemplate["Name"].ToString().ToLower()}");
                return fallbackIcon;
            }
            return tex;
        }
        else
            return fallbackIcon;
    }

    static readonly Dictionary<string, string> itemTypeTextureMap = new()
    {
        //main types
        ["Survivor"] = "Icon-Worker-L",
        ["Lead Survivor"] = "T-Icon-Survivor-Leader-CARD",
        ["Trap"] = "T-Icon-Traps-CARD",
        ["Defender"] = "T-Icon-Defenders-CARD",

        //hero sybtypes
        ["Soldier"] = "T-Icon-Hero-Soldier-CARD",
        ["Constructor"] = "T-Icon-Hero-Constructor-CARD",
        ["Ninja"] = "T-Icon-Hero-Ninja-CARD",
        ["Outlander"] = "T-Icon-Hero-Outlander-CARD",

        //ranged subtypes
        ["Assault"] = "T-Icon-Assault-CARD",
        ["SMG"] = "T-Icon-SMG-CARD",
        ["Pistol"] = "T-Icon-Pistol-CARD",
        ["Shotgun"] = "T-Shotgun-TITLE",
        ["Explosive"] = "T-Icon-Explosive-CARD",
        ["Sniper"] = "T-Icon-Sniper-CARD",

        //melee subtypes
        ["Axe"] = "T-Icon-Axe-CARD",
        ["Hardware"] = "T-Icon-Tool-CARD",
        ["Scythe"] = "T-Icon-Scythe-CARD",
        ["Spear"] = "T-Icon-Spear-CARD",
        ["Sword"] = "T-Icon-Sword-CARD",
        ["Club"] = "T-Icon-Blunt-CARD", //is this the right icon??

        //lead survivor subtypes
        ["Doctor"] = "T-Icon-Leader-Doctor-CARD",
        ["Engineer"] = "T-Icon-Leader-Engineer-CARD",
        ["Explorer"] = "T-Icon-Leader-Explorer-CARD",
        ["Gadgeteer"] = "T-Icon-Leader-Gadgeteer-CARD",
        ["Inventor"] = "T-Icon-Leader-Inventor-CARD",
        ["Martial Artist"] = "T-Icon-Leader-MartialArtist-CARD",
        ["Marksman"] = "T-Icon-Leader-Soldier-CARD",
        ["Trainer"] = "T-Icon-Leader-Trainer-CARD",

        //trap subtypes
        ["Wall"] = "T-Trap-Wall-TITLE",
        ["Ceiling"] = "T-Trap-Ceiling-TITLE",
        ["Floor"] = "T-Trap-Floor-TITLE",

        //defender subtypes
        ["Assault Defender"] = "T-Icon-Survivor-Assault-CARD",
        ["Shotgun Defender"] = "T-Icon-Survivor-Shotgun-CARD",
        ["Melee Defender"] = "T-Icon-Survivor-Melee-CARD",
        ["Pistol Defender"] = "T-Icon-Survivor-Pistol-CARD",
        ["Sniper Defender"] = "T-Icon-Survivor-Sniper-CARD",
    };

    public static Texture2D GetSubtypeTexture(string key, Texture2D fallbackIcon = null)
    {
        key ??= "";
        if (itemTypeTextureMap.ContainsKey(key))
        {
            return GetReservedTexture($"ExportedImages/{itemTypeTextureMap[key]}.png");
        }
        return fallbackIcon;
    }

    public static Texture2D GetItemSubtypeTexture(this JsonObject itemTemplate, Texture2D fallbackIcon = null)
    {
        switch (itemTemplate["Type"].ToString())
        {
            case "Schematic":
                if (itemTemplate["Category"].ToString() == "Trap")
                    return GetSubtypeTexture("Trap", fallbackIcon);
                else
                    return GetSubtypeTexture(itemTemplate["SubType"]?.ToString() ?? "", fallbackIcon);
            case "Worker":
                if (itemTemplate["ImagePaths"]?["SmallPreview"]?.ToString().Contains("GenericWorker") ?? false)
                    return null;
                else
                {
                    if (itemTemplate.ContainsKey("SubType"))
                        return GetSubtypeTexture(itemTemplate["SubType"].ToString(), fallbackIcon);
                    else
                        return GetSubtypeTexture("Survivor", fallbackIcon);
                }
            default:
                return GetSubtypeTexture(itemTemplate["SubType"]?.ToString() ?? "", fallbackIcon);
        }
    }

    public static Texture2D GetItemAmmoTexture(this JsonObject itemTemplate, Texture2D fallbackIcon = null)
    {
        if (itemTemplate["Type"].ToString() != "Schematic")
            return fallbackIcon;

        if (itemTemplate["Category"]?.ToString() == "Trap")
            return GetSubtypeTexture(itemTemplate["SubType"]?.ToString() ?? "", fallbackIcon);

        if (itemTemplate["RangedWeaponStats"]?["AmmoType"]?.ToString() is string ammoType && supplimentaryData.AmmoIcons.ContainsKey(ammoType))
            return supplimentaryData.AmmoIcons[ammoType];

        return fallbackIcon;
    }

    public static Texture2D GetSurvivorPersonalityTexture(this JsonObject itemInstance, Texture2D fallbackIcon = null)
    {
        var template = itemInstance.GetTemplate();

        //GD.Print(template["Type"].ToString());

        if (template["Type"].ToString() != "Worker")
            return fallbackIcon;

        //GD.Print("is "+(template.ContainsKey("Personality") ? "Mythic" : "Regular"));

        var personalityId = template.ContainsKey("Personality") ?
            template["Personality"]?.ToString() :
            itemInstance["attributes"]?["personality"]?.ToString()?.Split(".")?[^1];

        //GD.Print("personality: " + personalityId);

        if (personalityId is not null && supplimentaryData.PersonalityIcons.ContainsKey(personalityId))
            return supplimentaryData.PersonalityIcons[personalityId];

        return fallbackIcon;
    }

    public static Texture2D GetSurvivorSetTexture(this JsonObject itemInstance, Texture2D fallbackIcon = null)
    {
        var template = itemInstance.GetTemplate();

        if (template["Type"].ToString() != "Worker")
            return fallbackIcon;

        if (template.ContainsKey("SubType"))
        {
            if (template["SubType"]?.ToString()?.Replace("Martial Artist", "MartialArtist") is string subType && supplimentaryData.SquadIcons.ContainsKey(subType))
                return supplimentaryData.SquadIcons[subType];
        }
        else
        {
            string setBonus = itemInstance["attributes"]?["set_bonus"]?.ToString()?.Split(".")?[^1];
            if (setBonus is not null && supplimentaryData.SetBonusIcons.ContainsKey(setBonus))
                return supplimentaryData.SetBonusIcons[setBonus];
        }

        return fallbackIcon;
    }

    public static Texture2D GetReservedTexture(string texturePath)
    {
        if (texturePath is null)
            return null;
        if (iconCache.ContainsKey(texturePath) && iconCache[texturePath].GetRef().Obj is Texture2D cachedTexture)
            return cachedTexture;

        string fullPath = banjoFolderPath + "/" + texturePath;
        if(!FileAccess.FileExists(fullPath))
        {
            GD.PushWarning($"Missing Image file: {Helpers.ProperlyGlobalisePath(fullPath)}");
            return null;
        }
        Texture2D loadedTexture = ImageTexture.CreateFromImage(Image.LoadFromFile(fullPath));
        //Texture2D loadedTexture = ResourceLoader.Load<Texture2D>(fullPath);
        iconCache[texturePath] = GodotObject.WeakRef(loadedTexture);

        return loadedTexture;
    }

    public static int GetItemRarity(this JsonObject itemTemplate) =>
        (itemTemplate["Rarity"]?.ToString() ?? "") switch
        {
            "Common" => 1,
            "Uncommon" => 2,
            "Rare" => 3,
            "Epic" => 4,
            "Legendary" => 5,
            "Mythic" => 6,
            _ => 2
        };

    static string[] rarityIds = new string[]
    {
        null,
        "C",
        "UC",
        "R",
        "VR",
        "SR",
        "UR"
    };

    static string[] tierIds = new string[]
    {
        "T00",
        "T01",
        "T02",
        "T03",
        "T04",
        "T05",
    };

    public static JsonObject TryUpgradeTemplateRarity(this JsonObject itemTemplate)
    {
        int rarityIndex = itemTemplate.GetItemRarity();
        if (rarityIndex >= 6)
            return null;
        string fromRarity = $"_{rarityIds[rarityIndex]}_";
        string toRarity = $"_{rarityIds[rarityIndex+1]}_";
        string oldName = itemTemplate["Name"].ToString().ToLower();
        string newName = oldName.Replace(fromRarity.ToLower(), toRarity.ToLower());
        if (TryGetTemplate($"{itemTemplate["Type"]}:{newName}") is JsonObject newTemplate && newTemplate["Name"].ToString().ToLower() != oldName)
            return newTemplate;
        return null;
    }

    static string GetCompactRarityAndTier(this JsonObject itemTemplate, int givenTier = 0)
    {
        var rarityId = rarityIds[itemTemplate.GetItemRarity()];
        var tierId = givenTier <= 0 ? tierIds[itemTemplate["Tier"]?.GetValue<int>() ?? 0] : tierIds[givenTier];
        return rarityId + "_" + tierId;
    }

    public static float GetItemRating(this JsonObject itemInstance, string overrideSurviverSquad = null)
    {
        if (TryGetSource("ItemRatings", out var ratings))
        {
            var survivorSquad = overrideSurviverSquad;

            var template = itemInstance.GetTemplate();
            if (template?["Tier"] is null)
                return 0;
            var level = itemInstance["attributes"]?["level"]?.GetValue<int>() ?? ((template["Tier"].GetValue<int>() * 10) - 10);
            level = Mathf.Max(level, 1);
            string ratingCategory = "Default";

            if (template["Type"].ToString() == "Worker")
            {
                if (template.ContainsKey("SubType"))
                    ratingCategory = "LeadSurvivor";
                else
                    ratingCategory = "Survivor";
            }
            //GD.Print("ratingCat: " + ratingCategory);
            var ratingKey = template.GetCompactRarityAndTier();
            //GD.Print($"ratingKey: {ratingKey}");
            if (ratingCategory == "LeadSurvivor")
                ratingKey = ratingKey.Replace("UR_", "SR_");

            var ratingSet = ratings[ratingCategory]["Tiers"][ratingKey];
            if (ratingSet is null)
            {
                GD.Print($"no rating set {ratingCategory}:{ratingKey}");
                return 0;
            }
            int subLevel = level - ratingSet["FirstLevel"].GetValue<int>();
            var rating = ratingSet["Ratings"][subLevel].GetValue<float>();

            if (survivorSquad is null && itemInstance["attributes"]?["squad_id"]?.ToString() is string squadId)
                survivorSquad = squadId;
            if (template["Type"].ToString() == "Worker" && survivorSquad is not null)
            {
                if (template["SubType"]?.ToString() is string leadType)
                {
                    //check for synergy match
                    var matchedSquadID = supplimentaryData.SynergyToSquadId[leadType.Replace(" ", "")];
                    if (matchedSquadID == survivorSquad)
                    {
                        //GD.Print("lead synergy boost");
                        rating *= 2;
                    }
                    //else
                    //    GD.Print($"no lead synergy boost ({matchedSquadID} != {squadId})");
                }
                else
                {
                    var leadSurvivor = ProfileRequests.GetCachedProfileItems(FnProfileTypes.AccountItems, kvp =>
                        kvp.Value?["attributes"]?["squad_id"]?.ToString() == survivorSquad &&
                        kvp.Value["attributes"]["squad_slot_idx"].GetValue<int>() == 0
                    ).FirstOrDefault().Value?.AsObject();

                    string leaderRarity = leadSurvivor?.GetTemplate()["Rarity"].ToString() ?? "";
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

                    string targetPersonality = leadSurvivor?["attributes"]["personality"].ToString().Split(".")[^1] ?? "";
                    string currentPersonality = itemInstance["attributes"]["personality"].ToString().Split(".")[^1];

                    if (currentPersonality == targetPersonality)
                    {
                        //    GD.Print($"personality boost: {rarityBoost}");
                        rating += rarityBoost;
                    }
                    else
                    {
                        //GD.Print($"no personality boost ({targetPersonality} != {currentPersonality})");
                        //if (rarityPenalty > 0)
                        //    GD.Print($"personality penalty: {rarityPenalty}");
                        rating -= rarityPenalty;
                    }

                    rating = Mathf.Max(rating, 1);
                }
            }
            return rating;
        }
        return 0;
    }

    static readonly Color[] rarityColours = new Color[]
    {
        Colors.Transparent,
        Color.FromString("#bfbfbf", Colors.White),
        Color.FromString("#83db00", Colors.White),
        Color.FromString("#008bf1", Colors.White),
        Color.FromString("#a952ff", Colors.White),
        Color.FromString("#ff7b3d", Colors.White),
        Color.FromString("#ffff40", Colors.White),
    };
    public static Color GetItemRarityColor(this JsonObject itemTemplate) => rarityColours[itemTemplate.GetItemRarity()];
    public static Color GetRarityColor(int itemRarity) => rarityColours[itemRarity];

    public static JsonObject GetHeroAbilities(this JsonObject itemTemplate)
    {
        if (itemTemplate.ContainsKey("HeroItems"))
            return itemTemplate["HeroItems"].AsObject();
        if (itemTemplate.ContainsKey("HeroPerk"))
        {
            JsonObject heroItems = new()
            {
                ["HeroPerk"] = TryGetTemplate("Ability:" + itemTemplate["HeroPerkName"],out var heroPerk) ? heroPerk.Reserialise() : null,
                ["CommanderPerk"] = TryGetTemplate("Ability:" + itemTemplate["CommanderPerkName"], out var commanderPerk) ? commanderPerk.Reserialise() : null,
                ["HeroAbilities"] = new JsonArray()
                {
                    itemTemplate["HeroAbilities"][0].TryGetTemplate(out var heroAbility0) ? heroAbility0.Reserialise() : null,
                    itemTemplate["HeroAbilities"][1].TryGetTemplate(out var heroAbility1) ? heroAbility1.Reserialise() : null,
                    itemTemplate["HeroAbilities"][2].TryGetTemplate(out var heroAbility2) ? heroAbility2.Reserialise() : null
                }
            };
            itemTemplate["HeroItems"] = heroItems;
            return heroItems;
        }
        return null;
    }

    public static float GetHeroStat(this JsonObject heroInstance, string stat, int givenLevel = 0, int givenTier = 0)
    {
        if (TryGetSource("HeroStats", out var stats))
        {
            JsonObject template = heroInstance.GetTemplate();
            if (givenLevel <= 0)
            {
                givenLevel = heroInstance["attributes"]?["level"]?.GetValue<int>() ?? 1;
                givenTier = (int)template["Tier"];
            }
            string heroSubType = template["SubType"].ToString();
            string heroStatType = template["HeroStatType"].ToString();
            string heroRarityAndTier = template.GetCompactRarityAndTier(givenTier);
            var statLookup = stats["Types"]?[$"{heroSubType}_{heroStatType}"]?[heroRarityAndTier]?[stat]?.AsObject();
            if (statLookup is null)
                return 0;
            int statKey = Mathf.Clamp(givenLevel - (int)statLookup["FirstLevel"], 0, statLookup["Values"].AsArray().Count - 1);
            return (float)statLookup["Values"][statKey];
        }
        return 0;
    }
    
    public static JsonObject CreateInstanceOfItem(this JsonObject itemTemplate, int quantity = 1, JsonObject attributes = null, string overrideItem = null)
    {
        
        JsonObject toReturn = new()
        {
            ["templateId"] = itemTemplate["Type"].ToString()+":"+ itemTemplate["Name"].ToString(),
            ["template"] = itemTemplate.Reserialise(),
            ["quantity"] = quantity
        };
        attributes ??= new JsonObject();
        attributes["generated_by_pegleg"] = true;
        if(overrideItem is not null)
            attributes["overrideItem"] = overrideItem;
        toReturn["attributes"] = attributes;
        return toReturn;
    }

    public static bool TryGetTexturePathFromTemplate(this JsonObject itemTemplate, ItemTextureType textureType, out string foundPath)
    {
        foundPath = null;
        JsonObject imagePaths = itemTemplate["ImagePaths"]?.AsObject();
        if (imagePaths is null)
            return false;

        if (textureType == ItemTextureType.Preview)
        {
            foundPath = (imagePaths["LargePreview"] ?? imagePaths["SmallPreview"])?.ToString();
            if (!FileAccess.FileExists(banjoFolderPath + "/" + foundPath))
            {
                GD.Print($"Large Image not found: {banjoFolderPath + "/" + foundPath} ({itemTemplate["Name"]})");
                foundPath = imagePaths["SmallPreview"]?.ToString();
            }
        }
        else
            foundPath = imagePaths[textureType.ToString()]?.ToString();

        if (string.IsNullOrWhiteSpace(foundPath) || !foundPath.StartsWith("ExportedImages"))
            return false;
        return true;
    }

    static bool TryLoadJsonFile(string fileName, out JsonObject json)
    {
        json = null;
        string filePath = banjoFolderPath + "/" + fileName + ".json";
        if (!FileAccess.FileExists(filePath))
            return false;
        using FileAccess fileAccessor = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        json = JsonNode.Parse(fileAccessor.GetAsText()).AsObject();
        //GD.Print(fileName + " file loaded");
        return true;
    }
}

public static class HeroStats
{
    public const string MaxHealth = "FortHealthSet.MaxHealth";
    public const string MaxShields = "FortHealthSet.Shield";
    public const string HealthRegenRate = "FortRegenHealthSet.HealthRegenRate";
    public const string ShieldRegenRate = "FortRegenHealthSet.ShieldRegenRate";
    public const string AbilityDamage = "FortDamageSet.OutgoingBaseAbilityDamageMultiplier";
    public const string HealingModifier = "FortHealthSet.HealingSourceBaseMultiplier";
}

public static class SurvivorBonus
{
    public const string MaxHealth = "IsFortitudeLow";
    public const string MaxShields = "IsResistanceLow";
    public const string ShieldRegenRate = "IsShieldRegenLow";

    public const string RangedDamage = "IsRangedDamageLow";
    public const string MeleeDamage = "IsMeleeDamageLow";
    public const string AbilityDamage = "IsAbilityDamageLow";
    public const string TrapDamage = "IsTrapDamageLow";

    public const string TrapDurability = "IsTrapDurabilityHigh";
}

public class GameItemTemplate
{
    public GameItemTemplate(JsonObject rawData)
    {
        isReal = true;
        this.rawData = rawData;
    }

    public GameItemTemplate(string templateId = "Custom:item", string displayName = "Custom Item", string description = null, string iconPath = null, JsonObject extraData = null)
    {
        extraData ??= new();
        var splitTemplateId = templateId.Split(":");
        extraData["Type"] = splitTemplateId[0];
        extraData["Name"] = splitTemplateId[1];
        extraData["DisplayName"] = displayName;
        extraData["Description"] = description;
        if (iconPath is not null)
            extraData["ImagePaths"] = new JsonObject() { ["LargePreview"] = iconPath };
        rawData = extraData;
    }

    public bool isReal { get; private set; }
    public JsonObject rawData { get; private set; }
    public JsonNode this[string propertyName] => rawData[propertyName];
    public bool ContainsKey(string propertyName) => rawData.ContainsKey(propertyName);
    public string TemplateId => $"{Type}:{Name.ToLower()}";

    public string Type => rawData["Type"].ToString();
    public string Name => rawData["Name"].ToString();
    public string DisplayName => rawData["DisplayName"]?.ToString();
    public string Description => rawData["Description"]?.ToString();
    public string Category => rawData["Category"]?.ToString();
    public string SubType => rawData["SubType"]?.ToString();
    public string Rarity => rawData["Rarity"]?.ToString();
    public int RarityLevel => (Rarity ?? "") switch
        {
            "Common" => 1,
            "Uncommon" => 2,
            "Rare" => 3,
            "Epic" => 4,
            "Legendary" => 5,
            "Mythic" => 6,
            _ => 2 //todo: rarity items with unspecified rarity should be exported as "Uncommon", then this can be 0
        };

    public int Tier => rawData["Tier"] is JsonValue tierVal ? (tierVal.TryGetValue<int>(out var tier) ? tier : 0) : 0;
    public string Personality => rawData["Personality"]?.ToString();

    public Texture2D GetTexture(ItemTextureType textureType = ItemTextureType.Preview, Texture2D fallbackIcon = null)
    {
        if (Type == "Worker" && (rawData["ImagePaths"]?["SmallPreview"]?.ToString().Contains("GenericWorker") ?? false))
            return BanjoAssets.GetSubtypeTexture(SubType ?? "Survivor", fallbackIcon);

        if (TryGetTexturePath(textureType, out var texturePath))
        {
            var tex = BanjoAssets.GetReservedTexture(texturePath);
            if (tex is null)
            {
                GD.PushWarning($"Null texture in: {TemplateId}");
                return fallbackIcon;
            }
            return tex;
        }
        else
            return fallbackIcon;

    }

    public bool TryGetTexturePath(out string foundPath)
    {
        bool result = TryGetTexturePath(ItemTextureType.Preview, out var previewPath);
        foundPath = previewPath;
        return result;
    }

    public bool TryGetTexturePath(ItemTextureType textureType, out string foundPath)
    {
        foundPath = null;
        JsonObject imagePaths = rawData["ImagePaths"]?.AsObject();
        if (imagePaths is null)
            return false;

        if (textureType == ItemTextureType.Preview)
        {
            foundPath = (imagePaths["LargePreview"] ?? imagePaths["SmallPreview"])?.ToString();
            if (!FileAccess.FileExists(BanjoAssets.banjoFolderPath + "/" + foundPath))
            {
                GD.Print($"Large Image not found: {BanjoAssets.banjoFolderPath + "/" + foundPath} ({rawData["Name"]})");
                foundPath = imagePaths["SmallPreview"]?.ToString();
            }
        }
        else
            foundPath = imagePaths[textureType.ToString()]?.ToString();

        if (string.IsNullOrWhiteSpace(foundPath) || !foundPath.StartsWith("ExportedImages"))
            return false;
        return true;
    }

    public Texture2D GetSubtypeTexture(Texture2D fallbackIcon = null)
    {

        switch (Type)
        {
            case "Schematic":
                if (Category == "Trap")
                    return BanjoAssets.GetSubtypeTexture("Trap", fallbackIcon);
                else
                    return BanjoAssets.GetSubtypeTexture(SubType, fallbackIcon);
            case "Worker":
                if (rawData["ImagePaths"]?["SmallPreview"]?.ToString().Contains("GenericWorker") ?? false)
                    return null;
                else
                    return BanjoAssets.GetSubtypeTexture(SubType ?? "Survivor", fallbackIcon);
            default:
                return BanjoAssets.GetSubtypeTexture(SubType, fallbackIcon);
        }
    }

    public Texture2D GetAmmoTexture(Texture2D fallbackIcon = null)
    {
        if (Type != "Schematic")
            return fallbackIcon;

        if (Category == "Trap")
            return BanjoAssets.GetSubtypeTexture(SubType, fallbackIcon);

        if (rawData["RangedWeaponStats"]?["AmmoType"]?.ToString() is string ammoType && BanjoAssets.supplimentaryData.AmmoIcons.ContainsKey(ammoType))
            return BanjoAssets.supplimentaryData.AmmoIcons[ammoType];

        return fallbackIcon;
    }

    public void GenerateSearchTags(bool assumeUncommon = true)
    {
        List<string> tags = new()
        {
            DisplayName,
            Rarity ?? (assumeUncommon ? "Uncommon" : null),
            Type,
            SubType,
            Rarity,
            Personality?[2..]
        };
        rawData["RarityLv"] = rawData.AsObject().GetItemRarity();
        if (tags.Contains("Worker"))
            tags.Add("Survivor");
        rawData["searchTags"] = new JsonArray(tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => (JsonNode)t).ToArray());
    }

    public GameItem CreateInstance(int quantity = 1, JsonObject attributes = null, ProfileItemId? profileItemPointer=null)
    {
        attributes ??= new JsonObject();
        attributes["generated_by_pegleg"] = true;
        return new(this, quantity, attributes, profileItemPointer);
    }
}

public enum ItemTextureType
{
    Preview,
    Icon,
    LoadingScreen,
    PackImage,

    Personality,
    SetBonus
}

class DataTable
{
    readonly Dictionary<string, DataTableCurve> curves = new();

    public DataTable(string filepath)
    {
        if (!FileAccess.FileExists(filepath))
            return;
        using FileAccess dataTableFile = FileAccess.Open(filepath, FileAccess.ModeFlags.Read);
        var curveJsonMap = JsonNode.Parse(dataTableFile.GetAsText())[0]["Rows"].AsObject();

        foreach (var curveKvp in curveJsonMap)
        {
            //GD.Print(survivorCurveKvp.Key);
            curves[curveKvp.Key] = new(curveKvp.Value.AsObject());
        }
    }
    public bool ContainsKey(string key) => curves.ContainsKey(key);
    public DataTableCurve this[string key] => curves[key];
}

class DataTableCurve
{
    readonly List<float> times = new();
    readonly List<float> values = new();
    readonly float minTime = 0;
    readonly float maxTime = 0;

    public DataTableCurve(string filepath, string curveKey)
    {
        if (!FileAccess.FileExists(filepath))
            return;
        using FileAccess dataTableFile = FileAccess.Open(filepath, FileAccess.ModeFlags.Read);
        var curveJsonMap = JsonNode.Parse(dataTableFile.GetAsText())[0]["Rows"].AsObject();

        if (!curveJsonMap.ContainsKey(curveKey))
            return;

        var dataTableCurveJson = curveJsonMap[curveKey].AsObject();

        var keysArray = dataTableCurveJson["Keys"].AsArray();

        minTime = keysArray[0]["Time"].GetValue<float>();
        maxTime = keysArray[^1]["Time"].GetValue<float>();

        foreach (var curvePointKey in keysArray)
        {
            times.Add(curvePointKey["Time"].GetValue<float>());
            values.Add(curvePointKey["Value"].GetValue<float>());
        }
    }

    public DataTableCurve(JsonObject dataTableCurveJson)
    {
        var keysArray = dataTableCurveJson["Keys"].AsArray();

        minTime = keysArray[0]["Time"].GetValue<float>();
        maxTime = keysArray[^1]["Time"].GetValue<float>();

        foreach (var curvePointKey in keysArray)
        {
            times.Add(curvePointKey["Time"].GetValue<float>());
            values.Add(curvePointKey["Value"].GetValue<float>());
        }
    }

    public float Sample(float time)
    {
        if (time < minTime)
        {
            //handle pre-infinity
            return values[0];
        }
        if (time > maxTime)
        {
            //handle post-infinity
            return values[^1];
        }

        // higher/lower search for time range
        int GetClosestTimeIndexFloored(int fromIndex, int toIndex, float time)
        {
            if (toIndex - fromIndex < 3)
            {
                toIndex = Mathf.Clamp(toIndex, 0, times.Count);
                while (time <= times[toIndex] && toIndex>0)
                    toIndex--;
                return toIndex;
            }

            int middleIndex = Mathf.CeilToInt((toIndex - fromIndex) * 0.5f) + fromIndex;

            if (time == times[middleIndex])
                return middleIndex;
            else if (time > times[middleIndex])
                return GetClosestTimeIndexFloored(middleIndex, toIndex, time);
            else
                return GetClosestTimeIndexFloored(fromIndex, middleIndex, time);
        }

        int lowerIndex = GetClosestTimeIndexFloored(0, times.Count - 1, time);

        float lowerTime = times[lowerIndex];
        float upperTime = times[lowerIndex + 1];

        float betweenTimeBlend = (time - lowerTime) / (upperTime - lowerTime);

        float lowerValue = values[lowerIndex];
        float upperValue = values[lowerIndex + 1];

        return lowerValue + ((upperValue - lowerValue) * betweenTimeBlend);
    }
}