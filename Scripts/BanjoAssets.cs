using Godot;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

public static class BanjoAssets
{
    const string banjoFolderPath = "res://External/Banjo";

    public static readonly Texture2D defaultIcon = ResourceLoader.Load<Texture2D>("res://External/Icons/Mission/T-Icon-Unknown-128.png", "Texture2D");
    static readonly Dictionary<string, WeakRef> iconCache = new();
    public enum TextureType
    {
        Preview,
        Icon,
        LoadingScreen,
        PackImage
    }

    public static T Reserialise<T>(this T toReserialise) where T : JsonNode
    { 
        return JsonNode.Parse(toReserialise.ToString()) as T; 
    }

    public static JsonObject CreateTemplate(string name, string description = null, string type = "Custom:item", string iconPath = null)
    {
        var splitType = type.Split(':');
        JsonObject toReturn = new()
        {
            ["ItemName"] = name,
            ["Type"] = splitType[0],
            ["Name"] = splitType[1],
        };
        if(description is not null)
            toReturn["ItemDescription"] = description;
        if(iconPath is not null)
            toReturn["ImagePaths"] = new JsonObject() { ["SmallPreview"] =  iconPath };
        return toReturn;
    }

    public static bool TryGetTemplate(this JsonNode possibleItemId, out JsonObject itemTemplate)
    {
        itemTemplate = null;
        if(possibleItemId is JsonValue itemIdJson)
        {
            if (TryGetTemplate(itemIdJson.ToString(), out itemTemplate))
            {
                return true;
            }
            GD.Print("Itemisation failed on id: "+itemIdJson);
        }
        GD.PushWarning("Itemisation failed on possible id: " + possibleItemId);
        return  false;
    }

    public static bool TryGetTemplate(string itemID, out JsonObject itemTemplate)
    {
        var splitItemId = itemID.Split(':');
        if (itemID.Contains(':') && TryGetSource(splitItemId[0], out var source))
        {
            itemID = splitItemId[0] + ":" + splitItemId[1].ToLower();
            itemTemplate = source[itemID]?.AsObject();
            return itemTemplate is not null;
        }
        itemTemplate = null;
        return false;
    }

    public static JsonObject GetTemplate(this JsonObject itemInstance, out JsonObject template) => template = itemInstance.GetTemplate();
    public static JsonObject GetTemplate(this JsonObject itemInstance)
    {
        if (itemInstance.ContainsKey("template"))
            return itemInstance["template"].AsObject();
        if (itemInstance.ContainsKey("itemType"))
            itemInstance["templateId"] = itemInstance["itemType"].ToString();
        if (itemInstance["templateId"].TryGetTemplate(out var template))
        {
            itemInstance["template"] = template.Reserialise();
            return itemInstance["template"].AsObject();
        }
        return null;
    }

    static readonly Dictionary<string, JsonObject> dataSources = new();

    public static void PreloadSourcesParalell()
    {
        DirAccess banjoDir = DirAccess.Open(banjoFolderPath);
        var allFiles = banjoDir.GetFiles();
        var result = Parallel.ForEach(allFiles, file =>
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
    }

    public static bool TryGetSource(string namedDataSource, out JsonObject source)
    {
        bool exists = dataSources.ContainsKey(namedDataSource);
        if(!exists && TryLoadJsonFile(namedDataSource, out var json))
        {
            dataSources[namedDataSource] = source = json;
            return true;
        }
        source = exists ? dataSources[namedDataSource] : null;
        return exists;
    }

    public static Texture2D GetItemTexture(this JsonObject itemData, TextureType textureType = TextureType.Preview) =>
        itemData.GetItemTexture(defaultIcon, textureType);
    public static Texture2D GetItemTexture(this JsonObject itemData, Texture2D fallbackIcon, TextureType textureType = TextureType.Preview)
    {
        if (itemData is null)
            return fallbackIcon;
        if(TryGetTexturePathFromItem(itemData, textureType, out var texturePath))
            return GetReservedTexture(texturePath);
        else 
            return fallbackIcon;
    }

    //static readonly Dictionary<string, string> subtypeTextureMap = new()
    //{
    //    [""]
    //};

    public static Texture2D GetItemSubtypeTexture(this JsonObject itemData, Texture2D fallbackIcon, TextureType textureType = TextureType.Preview)
    {
        if (itemData is null)
            return fallbackIcon;
        if (TryGetTexturePathFromItem(itemData, textureType, out var texturePath))
            return GetReservedTexture(texturePath);
        else
            return fallbackIcon;
    }

    public static Texture2D GetReservedTexture(string texturePath)
    {
        if (iconCache.ContainsKey(texturePath) && iconCache[texturePath].GetRef().Obj is Texture2D cachedTexture)
        {
            return cachedTexture;
        }
        else
        {
            string fullPath = banjoFolderPath + "/" + texturePath;
            Texture2D loadedTexture = ImageTexture.CreateFromImage(Image.LoadFromFile(fullPath));
            //Texture2D loadedTexture = ResourceLoader.Load<Texture2D>(fullPath);
            iconCache[texturePath] = GodotObject.WeakRef(loadedTexture);

            return loadedTexture;
        }
    }

    public static int GetItemRarity(this JsonObject itemData)
    {
        if (itemData is null)
            return 0;
        return (itemData["Rarity"]?.ToString() ?? "") switch
        {
            "Common" => 1,
            "Rare" => 3,
            "Epic" => 4,
            "Legendary" => 5,
            "Mythic" => 6,
            _ => 2
        };
    }

    public static async Task<JsonObject> SetItemRewardNotification(this JsonObject itemData)
    {
        bool existsInInventory = (await ProfileRequests.GetSumOfProfileItems(FnProfiles.AccountItems, itemData["templateId"].ToString())) > 0;
        itemData["attributes"] ??= new JsonObject();
        itemData["attributes"]["item_seen"] = existsInInventory;
        return itemData;
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
    public static Color GetItemRarityColor(this JsonObject itemData) => rarityColours[itemData.GetItemRarity()];
    public static Color GetRarityColor(int itemRarity) => rarityColours[itemRarity];

    public static JsonObject GetHeroAbilities(this JsonObject itemTemplate)
    {
        if (itemTemplate.ContainsKey("HeroItems"))
            return itemTemplate["HeroItems"].AsObject();
        if (itemTemplate.ContainsKey("HeroPerk"))
        {
            JsonObject heroItems = new()
            {
                ["HeroPerk"] = itemTemplate["HeroPerk"].TryGetTemplate(out var heroPerk) ? heroPerk.Reserialise() : null,
                ["CommanderPerk"] = itemTemplate["CommanderPerk"].TryGetTemplate(out var commanderPerk) ? commanderPerk.Reserialise() : null,
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


    static readonly DataTable heroStatsDataTable = new("res://External/DataTables/AttributesHeroScaling.json");
    public static float GetHeroStat(this JsonObject heroInstance, string statType)
    {
        string statRowKey = heroInstance.GetTemplate()["HeroStatsRowPrefix"].ToString() + statType;
        int sampleTime = heroInstance["attributes"]?["level"]?.GetValue<int>() ?? 1;
        return heroStatsDataTable[statRowKey].Sample(sampleTime);
    }
    

    public static JsonObject CreateInstanceOfItem(this JsonObject itemTemplate, int quantity = 1, JsonObject attributes = null)
    {
        
        JsonObject toReturn = new()
        {
            ["templateId"] = itemTemplate["Type"].ToString()+":"+ itemTemplate["Name"].ToString(),
            ["template"] = itemTemplate.Reserialise(),
            ["quantity"] = quantity
        };
        attributes ??= new JsonObject();
        attributes["generated_by_pegleg"] = true;
        toReturn["attributes"] = attributes;
        return toReturn;
    }

    static bool TryGetTexturePathFromItem(JsonObject itemTemplate, TextureType textureType, out string foundPath)
    {
        foundPath = null;
        JsonObject imagePaths = itemTemplate["ImagePaths"]?.AsObject();
        if (imagePaths is null)
            return false;

        if (textureType == TextureType.Preview)
        {
            foundPath = (imagePaths["LargePreview"] ?? imagePaths["SmallPreview"])?.ToString();
            if (!FileAccess.FileExists(banjoFolderPath + "/" + foundPath))
                foundPath = imagePaths["SmallPreview"]?.ToString();
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
    public const string AbilityDamage = "FortRegenHealthSet.ShieldRegenRate";
    public const string HealingModifier = "FortRegenHealthSet.ShieldRegenRate";
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

    public DataTableCurve(JsonObject dataTableCurveJson)
    {
        var keysArray = dataTableCurveJson["Keys"].AsArray();

        minTime = keysArray[0]["Time"].GetValue<float>();
        maxTime = keysArray[^1]["Time"].GetValue<float>();

        foreach (var curveKey in keysArray)
        {
            times.Add(curveKey["Time"].GetValue<float>());
            values.Add(curveKey["Value"].GetValue<float>());
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