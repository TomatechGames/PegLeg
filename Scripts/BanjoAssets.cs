using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public static class BanjoAssets
{
    public const string banjoFolderPath = "res://External/Banjo";

    public static readonly Texture2D defaultIcon = ResourceLoader.Load<Texture2D>("res://Images/InterfaceIcons/T-Icon-Unknown-128.png");
    public static readonly BanjoSuppliments supplimentaryData = ResourceLoader.Load<BanjoSuppliments>("res://banjo_suppliments.tres");

    static readonly ConcurrentDictionary<string, WeakRef> iconCache = new();
    static readonly ConcurrentDictionary<string, JsonObject> dataSources = new();

    static readonly string[] preloadTemplates = new string[] { "Hero", "Schematic", "Worker", "WorkerPortrait", "Ingredient", "Defender", "Weapon", "Trap", "Quest", "Ability"};
    public static async Task<bool> ReadAllSources()
    {
        string path = Helpers.ProperlyGlobalisePath(banjoFolderPath);
        if (!DirAccess.DirExistsAbsolute(path))
            return false;
        GD.Print(path);
        //return TryLoadJsonFile(path+"/assets.json", out banjoFile);
        using (DirAccess banjoDir = DirAccess.Open(path))
        {
            var allFiles = banjoDir.GetFiles();
            Parallel.ForEach(allFiles, file =>
            {
                file = file.Split("/")[^1][..^5];
                if (TryLoadJsonFile(file, out var json))
                    dataSources[file] = json;
            });
        }
        List<Task> templateTasks=new();
        foreach (var templateType in preloadTemplates)
        {
            if(TryGetSource(templateType, out var items))
            {
                var templateTask = Parallel.ForEachAsync(items, (item, ct) =>
                {
                    GameItemTemplate.TryAdd(item.Value.AsObject());
                    return ValueTask.CompletedTask;
                });
                templateTasks.Add(templateTask);
            }
        }
        await Task.WhenAll(templateTasks);

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

    public static Texture2D GetReservedTexture(string texturePath)
    {
        if (texturePath is null)
            return null;
        if (iconCache.ContainsKey(texturePath) && iconCache[texturePath].GetRef().Obj is Texture2D cachedTexture)
            return cachedTexture;

        string fullPath = banjoFolderPath + "/" + texturePath;
        if(!FileAccess.FileExists(fullPath))
        {
            //GD.PushWarning($"Missing Image file: {Helpers.ProperlyGlobalisePath(fullPath)}");
            return null;
        }
        Texture2D loadedTexture = ImageTexture.CreateFromImage(Image.LoadFromFile(fullPath));
        //Texture2D loadedTexture = ResourceLoader.Load<Texture2D>(fullPath);
        iconCache[texturePath] = GodotObject.WeakRef(loadedTexture);

        return loadedTexture;
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

    public static JsonObject Lookup(string source, string name)
    {
        if (TryGetSource(source, out var lookupTarget))
            return lookupTarget[name]?.AsObject();
        return null;
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
    #region Static Values

    static Texture2D goldLlama = ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataGold.png", "Texture2D");

    public static string[] rarityIds = new string[]
    {
        null,
        "C",
        "UC",
        "R",
        "VR",
        "SR",
        "UR"
    };

    public static string[] tierIds = new string[]
    {
        "T00",
        "T01",
        "T02",
        "T03",
        "T04",
        "T05",
    };

    public static readonly Color[] rarityColours = new Color[]
    {
        Colors.Transparent,
        Color.FromString("#bfbfbf", Colors.White),
        Color.FromString("#83db00", Colors.White),
        Color.FromString("#008bf1", Colors.White),
        Color.FromString("#a952ff", Colors.White),
        Color.FromString("#ff7b3d", Colors.White),
        Color.FromString("#ffff40", Colors.White),
    };

    static readonly string[] cardPackFromRarity = new string[]
    {
        "CardPack:cardpack_choice_all_r",
        "CardPack:cardpack_choice_all_r",
        "CardPack:cardpack_choice_all_r",
        "CardPack:cardpack_choice_all_r",
        "CardPack:cardpack_choice_all_vr",
        "CardPack:cardpack_choice_all_sr",
    };

    #endregion

    #region Static Methods

    static ConcurrentDictionary<string, ConcurrentDictionary<string, GameItemTemplate>> templateDict = new();
    public static void TryAdd(JsonObject template)
    {
        if (template["Type"]?.ToString() is not string templateType || template["Name"]?.ToString().ToLower() is not string templateName)
            return;
        templateDict.TryAdd(templateType, new());
        if(templateDict[templateType].ContainsKey(templateName))
            return;
        templateDict[templateType].TryAdd(templateName, new(template));
    }

    public static GameItemTemplate Get(string templateId, JsonObject source = null)
    {
        if (templateId is null)
            return null;

        if (templateId.StartsWith("STWAccoladeReward"))
            templateId = templateId.Replace("STWAccoladeReward:stwaccolade_", "Accolades:accoladeid_stw_");

        if (templateId == "AccountResource:currency_mtxswap")
            templateId = "AccountResource:currency_hybrid_mtx_xrayllama";

        if (!templateId.Contains(':'))
            return null;

        var splitItemId = templateId.Split(':');
        splitItemId[1] = splitItemId[1].ToLower();

        if (templateDict.TryGetValue(splitItemId[0], out var templateSource) && templateSource.TryGetValue(splitItemId[1], out var template))
            return template;

        templateId = splitItemId[0] + ":" + splitItemId[1].ToLower();
        if (source is null && !BanjoAssets.TryGetSource(splitItemId[0], out source))
            return null;

        GameItemTemplate newTemplate = source[templateId] is JsonObject templateObj ? new(templateObj) : null;

        bool exists = false;
        if (newTemplate is not null)
        {
            templateDict.TryAdd(splitItemId[0], new());
            exists = !templateDict[splitItemId[0]].TryAdd(splitItemId[1], newTemplate);
        }

        return exists ? templateDict[splitItemId[0]][splitItemId[1]] : newTemplate;
    }

    public static GameItemTemplate Lookup(string sourceName, string key)
    {
        if (sourceName is null || key is null)
            return null;
        
        if (templateDict.TryGetValue(sourceName, out var sourceDict) && sourceDict.TryGetValue(key, out var template))
            return template;

        if (!BanjoAssets.TryGetSource(sourceName, out var source))
            return null;

        GameItemTemplate newTemplate = source[key] is JsonObject templateObj ? new(templateObj) : null;

        bool exists = false;
        if (newTemplate is not null)
        {
            templateDict.TryAdd(sourceName, new());
            exists = !templateDict[sourceName].TryAdd(key, newTemplate);
        }

        return exists ? templateDict[sourceName][key] : newTemplate;
    }

    public static GameItemTemplate GetOrCreate(string templateId, Func<GameItemTemplate> constructor)
    {
        if (Get(templateId) is GameItemTemplate foundTemplate)
            return foundTemplate;

        var splitItemId = templateId.Split(':');

        if (!templateId.Contains(':'))
            return null;

        GameItemTemplate newTemplate = constructor();

        bool exists = false;
        if (newTemplate is not null)
        {
            templateDict.TryAdd(splitItemId[0], new());
            exists = !templateDict[splitItemId[0]].TryAdd(splitItemId[1], newTemplate);
        }

        return exists ? templateDict[splitItemId[0]][splitItemId[1]] : newTemplate;
    }

    public static GameItemTemplate[] GetTemplatesFromSource(string namedDataSource, Func<GameItemTemplate, bool> filter = null)
    {
        if (!BanjoAssets.TryGetSource(namedDataSource, out var source))
            return null;
        filter ??= item => true;
        return source.Select(kvp => Get(kvp.Key, source)).Where(val => filter(val)).ToArray();
    }

    public static Texture2D GetSubtypeTexture(string key, Texture2D fallbackIcon = null)
    {
        key ??= "";
        var dict = BanjoAssets.supplimentaryData.ItemTypeAndSubtypeIcons;
        if (dict.ContainsKey(key))
            return dict[key];
        return fallbackIcon;
    }

    #endregion

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
        if (displayName is not null)
            extraData["DisplayName"] = displayName;
        if (description is not null)
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
    public bool VBucksOrXRayTickets => Type == "AccountResource" && Name.ToLower() is string lowername && (
            lowername == "currency_hybrid_mtx_xrayllama" ||
            lowername == "currency_mtxswap" ||
            lowername == "currency_xrayllama"
        );

    public string Type => rawData["Type"].ToString();
    public bool IsCollectable => Type switch
    {
        "Hero" or "Worker" or "Defender" or "Schematic" => true,
        _ => false
    };
    public bool CanBeLeveled => Tier > 0 && Type switch
    {
        "Hero" or "Worker" or "Weapon" or "Trap" => true,
        "Defender" or "Schematic" => !IsPermenant,
        _ => false
    };
    public bool CanBeUnseen=> Type switch
    {
        "Hero" or "Worker" or "Defender" or "Schematic" or "Quest" or "AccountResource" or "ConsumableAccountItem" or "CardPack" => true,
        _ => false
    };

    public string CollectionProfile => Type == "Schematic" ? FnProfileTypes.SchematicCollection : FnProfileTypes.PeopleCollection;
    public string Name => rawData["Name"].ToString();
    public string DisplayName => rawData["DisplayName"]?.ToString();
    public string SortingDisplayName => DisplayName.StartsWith("The ") ? DisplayName[4..] : DisplayName;
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
    public Color RarityColor => Name.StartsWith("ZCP_") ? Colors.Transparent : rarityColours[RarityLevel];

    public int Tier => rawData["Tier"]?.GetValue<int>() ?? 0;
    //public int Tier => rawData["Tier"] is JsonValue tierVal ? (tierVal.TryGetValue<int>(out var tier) ? tier : 0) : 0;
    public string Personality => rawData["Personality"]?.ToString();

    public bool IsPermenant => (Type == "Defender" || Type == "Schematic" || Type == "Hero") && rawData["RecycleRecipe"] is null;

    public JsonArray AlterationSlots => rawData?["AlterationSlots"]?.AsArray();

    public Texture2D GetTexture(FnItemTextureType textureType = FnItemTextureType.Preview) => GetTexture(textureType, BanjoAssets.defaultIcon);
    public Texture2D GetTexture(Texture2D fallbackIcon) => GetTexture(FnItemTextureType.Preview, fallbackIcon);

    public Texture2D GetTexture(FnItemTextureType textureType, Texture2D fallbackIcon)
    {
        if ((Type == "TeamPerk" || Type == "Ability") && textureType == FnItemTextureType.Preview)
            textureType = FnItemTextureType.Icon;

        if (Type == "Worker" && (rawData["ImagePaths"]?["SmallPreview"]?.ToString().Contains("GenericWorker") ?? false))
            return GetSubtypeTexture(SubType ?? "Survivor", fallbackIcon);

        if (Type == "CardPack")
        {
            if (textureType == FnItemTextureType.Preview && DisplayName.Contains("Legendary") && DisplayName.Contains("Llama") && !Name.StartsWith("ZCP_"))
                return goldLlama;
        }

        if (TryGetTexturePath(textureType, out var texturePath))
        {
            var tex = BanjoAssets.GetReservedTexture(texturePath);
            if (tex is null)
            {
                //GD.PushWarning($"Null texture in: {TemplateId}");
                return fallbackIcon;
            }
            return tex;
        }
        else
            return fallbackIcon;
    }

    public bool TryGetTexturePath(out string foundPath)
    {
        bool result = TryGetTexturePath(FnItemTextureType.Preview, out var previewPath);
        foundPath = previewPath;
        return result;
    }

    public bool TryGetTexturePath(FnItemTextureType textureType, out string foundPath)
    {
        foundPath = null;
        JsonObject imagePaths = rawData["ImagePaths"]?.AsObject();
        if (imagePaths is null)
            return false;

        if (textureType == FnItemTextureType.Preview)
        {
            foundPath = (imagePaths["LargePreview"] ?? imagePaths["SmallPreview"])?.ToString();
            if (!FileAccess.FileExists(BanjoAssets.banjoFolderPath + "/" + foundPath))
            {
                //GD.Print($"Large Image not found: {BanjoAssets.banjoFolderPath + "/" + foundPath} ({rawData["Name"]})");
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
                    return GetSubtypeTexture("Trap", fallbackIcon);
                else
                    return GetSubtypeTexture(SubType, fallbackIcon);
            case "Worker":
                if (rawData["ImagePaths"]?["SmallPreview"]?.ToString().Contains("GenericWorker") ?? false)
                    return null;
                else
                    return GetSubtypeTexture(SubType ?? "Survivor", fallbackIcon);
            case "Trap":
                return GetSubtypeTexture("Trap", fallbackIcon);
            default:
                return GetSubtypeTexture(SubType, fallbackIcon);
        }
    }

    public GameItemTemplate TryGetNextRarity()
    {
        if (rawData["RarityUpRecipe"]?["Result"]?.ToString() is string rarityUpResult)
            return Get(rarityUpResult);
        return null;
    }

    public GameItemTemplate TryGetNextTier()
    {
        if (rawData["TierUpRecipe"]?["Result"]?.ToString() is string rarityUpResult)
            return Get(rarityUpResult);
        return null;
    }

    public Texture2D GetAmmoTexture(Texture2D fallbackIcon = null)
    {
        if (Type != "Schematic" && Type != "Weapon" && Type != "Trap")
            return fallbackIcon;

        if (Category == "Trap" || Type == "Trap")
            return GetSubtypeTexture(SubType, fallbackIcon);

        if (
            rawData["RangedWeaponStats"]?["AmmoType"]?.ToString() is string ammoType && 
            BanjoAssets.supplimentaryData.AmmoIcons.ContainsKey(ammoType)
            )
            return BanjoAssets.supplimentaryData.AmmoIcons[ammoType];

        return fallbackIcon;
    }

    public string GetCompactRarityAndTier(int givenTier = 0)
    {
        var rarityId = rarityIds[RarityLevel];
        var tierId = givenTier <= 0 ? tierIds[Tier] : tierIds[givenTier];
        return rarityId + "_" + tierId;
    }

    GameItemTemplate[] heroAbilities;
    public GameItemTemplate[] GetHeroAbilities()
    {
        if (Type != "Hero")
            return null;
        return heroAbilities ??= new GameItemTemplate[]
        {
            Get(rawData["HeroPerkTemplate"]?.ToString()),
            Get(rawData["CommanderPerkTemplate"]?.ToString()),
            Get(rawData["HeroAbilities"]?[0].ToString()),
            Get(rawData["HeroAbilities"]?[1].ToString()),
            Get(rawData["HeroAbilities"]?[2].ToString()),
        };
    }

    GameItemTemplate teamPerk;
    public GameItemTemplate GetTeamPerk()
    {
        if (Type != "Hero")
            return null;
        return teamPerk ??= Get(rawData["UnlocksTeamPerk"]?.ToString());
    }

    GameItem[] questRewards;
    GameItem[] visibleQuestRewards;
    GameItem[] hiddenQuestRewards;
    public GameItem[] GetQuestRewards()
    {
        if (Type != "Quest")
            return null;
        return questRewards ??= GetVisibleQuestRewards().Union(GetHiddenQuestRewards()).ToArray();
    }

    public GameItem[] GetVisibleQuestRewards()
    {
        if (Type != "Quest")
            return null;
        return visibleQuestRewards ??= GenerateQuestRewards(false);
    }

    public GameItem[] GetHiddenQuestRewards()
    {
        if (Type != "Quest")
            return null;
        return hiddenQuestRewards ??= GenerateQuestRewards(true);
    }

    GameItem[] GenerateQuestRewards(bool hidden)
    {
        var allRewards = rawData["Rewards"]
            .AsArray()
            .Where(r => r["Hidden"].GetValue<bool>() == hidden);

        var rewards = allRewards
            .Where(r => !r["Selectable"].GetValue<bool>())
            .Select(r => Get(r["Item"].ToString())?.CreateInstance(r["Quantity"].GetValue<int>()))
            .Where(r => r is not null)
            .ToList();

        var dynamicRewards = allRewards
            .Where(r => r["Selectable"].GetValue<bool>());

        if (dynamicRewards.Any())
        {
            //fake a cardpack to show a choice reward
            var cardpackID = cardPackFromRarity[dynamicRewards.Select(q => Get(q["Item"]?.ToString())?.RarityLevel ?? 0).Max()];
            JsonObject attributes = new()
            {
                ["options"] = new JsonArray(dynamicRewards.Select(r => new JsonObject()
                {
                    ["itemType"] = r["Item"].ToString(),
                    ["attributes"] = new JsonObject(),
                    ["quantity"] = r["Quantity"].GetValue<int>()
                }).ToArray())
            };
            var choiceReward = Get(cardpackID).CreateInstance(1, attributes);
            choiceReward.attributes["quest_selectable"] = true;
            rewards.Insert(0, choiceReward);
        }
        return rewards.ToArray();
    }

    public JsonArray GenerateSearchTags(bool assumeUncommon = true)
    {
        if(rawData["searchTags"] is JsonArray existingSearchTags)
            return existingSearchTags.Reserialise();

        List<string> tags = new()
        {
            DisplayName,
            Rarity ?? (assumeUncommon ? "Uncommon" : null),
            Type,
            SubType,
            Category,
            Personality?[2..]
        };

        if(GetHeroAbilities() is GameItemTemplate[] abilities)
        {
            foreach (var ability in abilities)
            {
                if (!ability?.DisplayName?.EndsWith("+") ?? false)
                    tags.Add(ability.DisplayName);
            }
        }
        if(GetTeamPerk() is GameItemTemplate teamPerk)
            tags.Add(teamPerk.DisplayName);

        if (tags.Contains("Worker"))
            tags.Add("Survivor");
        if (rawData["RecycleRecipe"] is null)
            tags.Add("Permenant");
        var searchTags = new JsonArray(tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => (JsonNode)t).ToArray());
        lock (rawData)
        {
            rawData["RarityLv"] = RarityLevel;
            rawData["searchTags"] = searchTags;
        }
        return searchTags.Reserialise();
    }

    public GameItem CreateInstance(int quantity = 1, JsonObject attributes = null, GameItem inspectorOverride = null, JsonObject customData = null)
    {
        customData ??= new();
        customData["generated_by_pegleg"] = true;
        return new(this, quantity, attributes, inspectorOverride, customData);
    }
}

public enum FnItemTextureType
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