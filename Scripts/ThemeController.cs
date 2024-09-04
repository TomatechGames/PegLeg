using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Nodes;
using static ThemeController;

public partial class ThemeController : Node
{
	static readonly DateTime referenceStartDate = new(2024, 1, 25);
	static readonly int[] seasonLengths = new int[]
	{
		10,
		11,
		11,
		11,
		9
	};
    static readonly int weeksInSeasonalYear = seasonLengths.Sum();
	static readonly string[] seasonThemes = new string[]
	{
        "Flannel Falls",
        "Scurvy Shoals",
        "Blasted Badlands",
        "Hexsylvania",
        "Frozen Fjords",
    };

    static string seasonTheme;
    const string builtInThemePath = "res://External/Themes/BuiltIn";
    const string customThemePath = "res://External/Themes/Custom";

    [Export]
    AudioStreamPlayer samplePlayer;

    public override void _Ready()
	{
        seasonTheme = GetSeasonTheme();
        GenerateThemeData();
        SetActiveTheme(AppConfig.Get("theme", "current", ""));
        RefreshTimerController.OnDayChanged += CheckForNewSeason;
    }

    static void CheckForNewSeason()
    {
        var newSeasonTheme = GetSeasonTheme();
        if (newSeasonTheme != seasonTheme)
        {
            seasonTheme = newSeasonTheme;
            if (currentThemeName == "")
                SetActiveTheme(AppConfig.Get("theme", "current", ""));
        }
    }

    static string GetSeasonTheme()
    {
        //TODO: timeline-related logic should be moved to a timeline class
        int weekCount = ((RefreshTimerController.RightNow.Date - referenceStartDate).Days / 7) % weeksInSeasonalYear;
        int seasonIndex = -1;
        for (int i = 0; i < seasonLengths.Length; i++)
        {
            if (weekCount < seasonLengths[i])
            {
                seasonIndex = i;
                break;
            }
            weekCount -= seasonLengths[i];
        }
        return seasonThemes[seasonIndex];
    }

    public static string currentThemeName { get; private set; }
    public static string activeThemeName { get; private set; }
    public static ThemeData activeTheme { get; private set; }

    public static event Action OnThemeUpdated;

    public static void SetActiveTheme(string newTheme)
    {
        newTheme = themeDataSet.ContainsKey(newTheme) ? newTheme : "";
        AppConfig.Set("theme", "current", newTheme);
        currentThemeName = newTheme;
        activeThemeName = themeDataSet.ContainsKey(currentThemeName) ? currentThemeName : seasonTheme;
        if (activeTheme != themeDataSet[activeThemeName])
        {
            activeTheme?.Unload();
            activeTheme = themeDataSet[activeThemeName];
            activeTheme.Load();
        }
        OnThemeUpdated?.Invoke();
    }

    static Dictionary<string, ThemeData> themeDataSet;

    static void GenerateThemeData()
    {
        if (themeDataSet is not null)
            return;
        themeDataSet = new();

        string absoluteCustomThemePath = Helpers.ProperlyGlobalisePath(customThemePath);
        if (DirAccess.DirExistsAbsolute(absoluteCustomThemePath))
        {
            using DirAccess customThemes = DirAccess.Open(absoluteCustomThemePath);
            foreach (var themeDir in customThemes.GetDirectories())
            {
                var themeFullDir = customThemes.GetCurrentDir() + "/" + themeDir;
                if (!FileAccess.FileExists(themeFullDir + "/theme.json"))
                    continue;
                using var themeFile = FileAccess.Open(themeFullDir + "/theme.json", FileAccess.ModeFlags.Read);
                themeDataSet.Add(themeDir, new(JsonNode.Parse(themeFile.GetAsText()), themeDir));
            }
        }

        string absoluteBuiltInThemePath = Helpers.ProperlyGlobalisePath(builtInThemePath);
        if (!DirAccess.DirExistsAbsolute(absoluteCustomThemePath))
            DirAccess.MakeDirAbsolute(absoluteCustomThemePath);
        using DirAccess builtInThemes = DirAccess.Open(absoluteBuiltInThemePath);
        foreach (var themeName in builtInThemes.GetDirectories())
        {
            if (themeDataSet.ContainsKey(themeName))
                continue;
            var themeFullDir = builtInThemes.GetCurrentDir() + "/" + themeName;
            if (!FileAccess.FileExists(themeFullDir + "/theme.json"))
                continue;
            using var themeFile = FileAccess.Open(themeFullDir + "/theme.json", FileAccess.ModeFlags.Read);
            themeDataSet.Add(themeName, new(JsonNode.Parse(themeFile.GetAsText()), themeName));
        }
    }

    public static string[] GetThemeList()
    {
        GenerateThemeData();
        return themeDataSet.Select(kvp => kvp.Key).ToArray();
    }

    public static ThemeData GetTheme(string key) => 
        themeDataSet.ContainsKey(key) ? themeDataSet[key] : themeDataSet[seasonTheme];

    public class ThemeData
    {
        public string themeName { get; init; }
        public Background[] backgrounds { get; init; }
        public MusicPlaylist[] music { get; init; }
        public ThemeData(JsonNode basis, string themeName)
        {
            this.themeName = themeName;
            backgrounds = basis[nameof(backgrounds)].AsFlexibleArray()
                .Select(n => new Background(n, themeName))
                .ToArray();
            music = basis[nameof(music)].AsFlexibleArray().Select(n => new MusicPlaylist(n, themeName)).ToArray();
        }

        public void Load()
        {
            Array.ForEach(backgrounds, b => b.LoadFile());
            Array.ForEach(music, m => m.Load());
        }

        public void Unload()
        {
            Array.ForEach(backgrounds, b => b.UnloadFile());
            Array.ForEach(music, m => m.Unload());
        }

        public ImageTexture GetBackground() =>
            backgrounds[PickBackground()].fileData;

        public int PickBackground() =>
            Helpers.RandomIndexFromWeights(backgrounds.Select(b=>b.RealWeight).ToArray());

        public ImageTexture LoadSampleBackground() =>
            backgrounds[GD.RandRange(0, backgrounds.Length - 1)].LoadFileTemparary();

        public int PickPlaylist() =>
            Helpers.RandomIndexFromWeights(music.Select(m => m.playlistWeight).ToArray());

        public AudioStreamWav LoadSampleMusic() =>
            music[GD.RandRange(0, music.Length - 1)].LoadSample();
    }

    public class MusicPlaylist
    {
        public MusicTrack[] tracks { get; init; }
        public MusicLayer[] intros { get; init; }
        public int layerCount { get; init; }
        public float playlistWeight { get; init; } = 1;
        public float layerSwitchChance { get; init; } = 0.15f;
        public float trackSwitchChance { get; init; } = 0.5f;
        public float trackSwitchCooldown { get; init; } = 50;

        public MusicPlaylist(JsonNode playlistNode, string themeName)
        {
            JsonObject playlistObj = playlistNode.AsFlexibleObject(nameof(tracks));

            tracks = playlistObj[nameof(tracks)].AsFlexibleArray().Select(n => new MusicTrack(n, themeName)).ToArray();
            layerCount = tracks.Select(t => t.layers.Length).Max();

            playlistWeight = playlistObj[nameof(playlistWeight)]?.GetValue<float>() ?? playlistWeight;
            layerSwitchChance = playlistObj[nameof(layerSwitchChance)]?.GetValue<float>() ?? layerSwitchChance;
            trackSwitchChance = playlistObj[nameof(trackSwitchChance)]?.GetValue<float>() ?? trackSwitchChance;
            trackSwitchCooldown = playlistObj[nameof(trackSwitchCooldown)]?.GetValue<float>() ?? trackSwitchCooldown;
            intros = playlistObj[nameof(intros)]?.AsFlexibleArray().Select(n => new MusicLayer(n, themeName)).ToArray();
        }

        public void Load()
        {
            Array.ForEach(tracks, t => t.Load());
            if(intros is not null)
                Array.ForEach(intros, t => t.LoadFile());
        }

        public void Unload()
        {
            Array.ForEach(tracks, t => t.Unload());
            if (intros is not null)
                Array.ForEach(intros, t => t.UnloadFile());
        }

        public AudioStreamWav GetIntro() =>
            intros is null ? null : intros[PickIntro()].fileData;

        int PickIntro() =>
            Helpers.RandomIndexFromWeights(intros.Select(i=>i.RealWeight).ToArray());

        public int PickTrack(int withLayer, int fromTrack = -1) =>
            Helpers.RandomIndexFromWeights(tracks.Select(t => t.trackWeight * (withLayer == -1 || t.layers[withLayer].RealWeight > 0 ? 1 : 0)).ToArray(), fromTrack);

        public int PickLayer(int inTrack, int fromLayer = -1)
        {
            var usableWeights = new float[layerCount];

            for (int i = 0; i < tracks[inTrack].layers.Length; i++)
            {
                usableWeights[i] = tracks[inTrack].layers[i].RealWeight;
            }
            for (int i = tracks[inTrack].layers.Length; i < layerCount; i++)
            {
                usableWeights[i] = 0;
            }

            return Helpers.RandomIndexFromWeights(usableWeights, fromLayer);
        }

        public AudioStreamWav LoadSample() =>
            tracks[GD.RandRange(0, tracks.Length - 1)].PickSample();
    }

    public class MusicTrack
    {
        public MusicLayer[] layers { get; init; }
        public float trackWeight { get; init; } = 1;

        public MusicTrack(JsonNode trackNode, string themeName)
        {
            JsonObject trackObj = trackNode.AsFlexibleObject(nameof(layers));
            layers = trackObj[nameof(layers)].AsFlexibleArray()
                .Select(n => new MusicLayer(n, themeName))
                .ToArray();
            trackWeight = trackObj[nameof(trackWeight)]?.GetValue<float>() ?? trackWeight;
        }

        public void Load() =>
            Array.ForEach(layers, l => l.LoadFile());

        public void Unload() =>
            Array.ForEach(layers, l => l.UnloadFile());

        public AudioStreamWav PickSample() =>
            layers[GD.RandRange(0, layers.Length - 1)].LoadFileTemparary();
    }

    public class MusicLayer : ThemeFile<AudioStreamWav>
    {
        public MusicLayer(JsonNode fileNode, string themeName) : base(fileNode, themeName) { }

        protected override AudioStreamWav LoadFileFromPath(string fullPath)
        {
            using var layerFile = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
            var layerStream = new AudioStreamWav()
            {
                Data = layerFile.GetBuffer((long)layerFile.GetLength()),
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = 96000,
            };
            return layerStream;
        }
    }

    public class Background : ThemeFile<ImageTexture>
    {
        public Background(JsonNode fileNode, string themeName) : base(fileNode, themeName) { }

        protected override ImageTexture LoadFileFromPath(string fullPath)
        {
            Image resourceImage = new();
            if (resourceImage.Load(fullPath) != Error.Ok)
                return null;
            var imageTex = ImageTexture.CreateFromImage(resourceImage);
            return imageTex;
        }
    }

    public abstract class ThemeFile<T> where T : class
    {
        public string sourceTheme { get; init; }
        public string path { get; init; }
        public float weight { get; init; } = 1;
        public T fileData { get; private set; }

        public ThemeFile(JsonNode fileNode, string themeName)
        {
            var fileObj = fileNode.AsFlexibleObject(nameof(path));
            path = fileObj[nameof(path)].ToString();
            if (path.Contains(":"))
            {
                var splitPath = path.Split(':');
                sourceTheme = splitPath[0];
                path = splitPath[1..].Join(":");
            }
            else
                sourceTheme = themeName;
            weight = fileObj[nameof(weight)]?.GetValue<float>() ?? weight;
        }

        public float RealWeight => fileData is null ? 0 : weight;

        public T LoadFileTemparary()
        {
            string possiblePath = customThemePath + "/" + sourceTheme + "/" + path;
            if (!FileAccess.FileExists(possiblePath))
                possiblePath = builtInThemePath + "/" + sourceTheme + "/" + path;
            if (!FileAccess.FileExists(possiblePath))
                return null;
            return LoadFileFromPath(possiblePath);
        }
        protected abstract T LoadFileFromPath(string path);

        public void LoadFile()
        {
            fileData ??= LoadFileTemparary();
        }

        public void UnloadFile()
        {
            fileData = null;
        }
    }
}
