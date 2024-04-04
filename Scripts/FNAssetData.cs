using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

[Serializable]
public static class FNAssetData
{
    const string databasePath = "res://External/fnAssetData.json";
    static JsonNode dataInstance;

    static void EnsureDatabase()
    {
        if (dataInstance != null)
            return;

        using var missionFile = FileAccess.Open(databasePath, FileAccess.ModeFlags.Read);
        dataInstance = JsonNode.Parse(missionFile.GetAsText());
    }

    public static DisplayData GetDisplayDataForMissionGenerator(string generator)
    {
        EnsureDatabase();
        if (!dataInstance["missionGens"].AsObject().ContainsKey(generator.ToLower()[..^2]))
        {
            Debug.WriteLine(generator.ToLower()[..^2]);
            return default;
        }
        string displayInfoKey = dataInstance["missionGens"][generator.ToLower()[..^2]].ToString();
        return new(dataInstance["displayData"][displayInfoKey]);
    }

    public static (DisplayData, int) GetDataForReward(string reward)
    {
        EnsureDatabase();
        if (!dataInstance["rewards"].AsObject().ContainsKey(reward.ToLower()))
        {
            Debug.WriteLine(reward.ToLower());
            return default;
        }
        var rewardNode = dataInstance["rewards"][reward.ToLower()];
        var displayData = new DisplayData(dataInstance["displayData"][rewardNode["displayDataKey"].ToString()]);
        return (displayData, rewardNode["scalingTier"].GetValue<int>());
    }

    public static (DisplayData, int) GetDataForResource(string resource){
        EnsureDatabase();
        if (!dataInstance["resources"].AsObject().ContainsKey(resource.ToLower()))
        {
            Debug.WriteLine(resource.ToLower());
            return default;
        }
        var resourceNode = dataInstance["resources"][resource.ToLower()];
        var displayData = new DisplayData(dataInstance["displayData"][resourceNode["displayDataKey"].ToString()]);
        return (displayData, resourceNode["rarity"].GetValue<int>());
    }

    public static (DisplayData,JsonObject,JsonObject) GetDataForHero(string hero)
    {
        EnsureDatabase();
        if (!dataInstance["heros"].AsObject().ContainsKey(hero.ToLower()))
        {
            Debug.WriteLine(hero.ToLower());
            return default;
        }
        var heroNode = dataInstance["heros"][hero.ToLower()].AsObject();
        var heroDataNode = dataInstance["heroData"][heroNode["heroDataKey"].ToString()].AsObject();
        heroDataNode["heroCategoryName"] = heroDataNode["heroCategory"].GetValue<int>() switch
        {
            1 => "Soldier",
            2 => "Constructor",
            3 => "Ninja",
            4 => "Outlander",
            _ => "?",

        };
        var displayData = new DisplayData(dataInstance["displayData"][heroNode["displayDataKey"].ToString()]);
        return (displayData,heroNode,heroDataNode);
    }

    public static DisplayData GetDataForModifier(string modifier)
    {
        EnsureDatabase();
        if (!dataInstance["modifiers"].AsObject().ContainsKey(modifier.ToLower()))
        {
            Debug.WriteLine(modifier.ToLower());
            return default;
        }
        var modifierNode = dataInstance["modifiers"][modifier.ToLower()].ToString();
        var displayData = new DisplayData(dataInstance["displayData"][modifierNode]);
        return displayData;
    }

    public static readonly Texture2D defaultIcon = ResourceLoader.Load<Texture2D>("res://External/Icons/Mission/T-Icon-Unknown-128.png", "Texture2D");
    static readonly Dictionary<string, Texture2D> iconCache = new();

    public struct DisplayData
    {
        public string displayName;
        public readonly bool HasName => displayName != "?";
        public string description;
        public readonly bool HasDescription => description != "?";
        public string displayNameTranslationKey;
        public readonly bool HasLocalisedName => displayName != "?";
        public string descriptionTranslationKey;
        public readonly bool HasLocalisedDescription => description != "?";
        public string iconPath;

        public DisplayData(JsonNode dataNode)
        {
            displayName = dataNode["displayName"].ToString();
            description = dataNode["description"].ToString();
            displayNameTranslationKey = dataNode["displayNameTranslationKey"].ToString();
            descriptionTranslationKey = dataNode["descriptionTranslationKey"].ToString();
            iconPath = dataNode["iconPath"].ToString();
        }

        public string GetOptimalName(string fallback = "?")
        {
            //TODO: localisation
            if (displayName is not null && displayName != "?")
                return displayName;
            return fallback;
        }

        public string GetOptimalDescription(string fallback = "?")
        {
            //TODO: localisation
            if (description is not null && description != "?")
                return description;
            return fallback;
        }

        public Texture2D GetIcon(Texture2D fallback = null)
        {
            if (iconPath is not null)
            {
                if (iconCache.ContainsKey(iconPath))
                {
                    return iconCache[iconPath];
                }
                string fullPath = "res://External/" + iconPath;
                if (ResourceLoader.Exists(fullPath))
                {
                    return ResourceLoader.Load<Texture2D>(fullPath, "Texture2D");
                    // Image image = ResourceLoader.Load<Texture2D>(fullPath, "Texture2D").GetImage();
                    // image.Resize(32, 32);
                    // Texture2D icon = ImageTexture.CreateFromImage(image);
                    // iconCache[iconPath] = icon;
                    // return icon;
                }
            }
            if (fallback != null)
                return fallback;
            return defaultIcon;
        }
    }

    [Serializable]
    public struct MissionOld
    {
        public readonly bool IsValid => !string.IsNullOrWhiteSpace(pegLegIdentifier);
        public readonly bool IsStoryMission => standardIcon == "Story";

        public string pegLegIdentifier { get; set; }
        public string displayName { get; set; }
        public string[] generatorAliases { get; set; }
        public string standardIcon { get; set; }
        public string fourPlayerIcon { get; set; }


        const string iconFolder = "res://External/Icons/Mission";
        static Dictionary<string, Texture2D> loadedIcons;
        static Texture2D defaultIcon;
        static void PreloadIcons()
        {
            if (loadedIcons is not null)
                return;
            var iconPaths = DirAccess.Open(iconFolder).GetFiles().Where(img => img.EndsWith(".png"));
            loadedIcons = new();
            Dictionary<string, string> processedKeys = new();

            foreach (var path in iconPaths)
            {
                string[] splitName = path.Split("-_".ToCharArray());
                string key = splitName[2..(splitName.Length - 1)].Join("-");

                if (processedKeys.ContainsKey(key))
                {
                    int currentResolution = int.Parse(splitName.Last().Split(".")[0]);
                    int existingResolution = int.Parse(processedKeys[key].Split("-_".ToCharArray()).Last().Split(".")[0]);
                    if (currentResolution < existingResolution)
                    {
                        Debug.WriteLine($"better match for {key} ({currentResolution}<{existingResolution}): {path}");
                        processedKeys.Remove(key);
                    }
                    else
                        continue;
                }
                else
                {
                    Debug.WriteLine($"match for {key} : {path}");
                }

                processedKeys.Add(key, path);
            }

            foreach (var item in processedKeys)
            {
                var loadedImage = ResourceLoader.Load<Texture2D>(iconFolder + "/" + item.Value, "Texture2D").GetImage();
                loadedImage.Resize(32, 32);
                loadedIcons.Add(item.Key, ImageTexture.CreateFromImage(loadedImage));
            }

            //since epic were lasy and used a quest image for Rescue The Survivors, we need to add it via hardcode
            loadedIcons.Add("RescueSurvivors", ResourceLoader.Load<Texture2D>("res://External/Icons/Quest/T-Icon-Rescue-Survivors-32.png", "Image"));

            defaultIcon = ResourceLoader.Load<Texture2D>(iconFolder + "/T-Icon-Unknown-32.png", "Texture2D");
        }

        public Texture2D GetIcon(bool isFourPlayer)
        {
            PreloadIcons();
            string key = isFourPlayer ? ("4Player-" + fourPlayerIcon) : standardIcon;
            if (IsValid && loadedIcons.ContainsKey(key))
                return loadedIcons[key];
            else
                return defaultIcon;
        }
    }
}