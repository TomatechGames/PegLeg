using Godot;
using System;
using System.Text.Json.Nodes;

public static class AppConfig
{
    public static event Action<string, string, JsonValue> OnConfigChanged;

    //static ConfigFile configFile;
    const string configPath = "user://appConfig.json";
    static JsonObject configData;

    public static T Get<T>(string section, string key, T fallback = default)
    {
        LoadConfig();
        var possibleVal = configData[section]?[key]?.AsValue();
        if (possibleVal is JsonValue val)
            return val.TryGetValue<T>(out var typedVal) ? typedVal : fallback;
        return fallback;
    }

    public static void Set(string section, string key, AdaptiveJsonValue value)
    {
        LoadConfig();
        configData[section] ??= new JsonObject();
        configData[section][key] = value.JsonValue;
        GD.Print($"Applying Change ({section}:{key} = {value.JsonValue})");
        OnConfigChanged?.Invoke(section, key, value.JsonValue);

        using var configFile = FileAccess.Open(configPath, FileAccess.ModeFlags.Write);
        configFile.StoreString(configData.ToString());
    }

    static void LoadConfig()
    {
        if (configData is not null)
            return;
        using var configFile = FileAccess.Open(configPath, FileAccess.ModeFlags.Read);
        if (configFile is not null)
            configData = JsonNode.Parse(configFile.GetAsText())?.AsObject();
        configData ??= new();
    }

    public readonly struct AdaptiveJsonValue
    {
        public JsonValue JsonValue { get; private init; }
        public AdaptiveJsonValue(JsonValue val)
        {
            JsonValue = val;
        }
        public static implicit operator AdaptiveJsonValue(bool value) => new(JsonValue.Create(value));
        public static implicit operator AdaptiveJsonValue(int value) => new(JsonValue.Create(value));
        public static implicit operator AdaptiveJsonValue(float value) => new(JsonValue.Create(value));
        public static implicit operator AdaptiveJsonValue(double value) => new(JsonValue.Create(value));
        public static implicit operator AdaptiveJsonValue(string value) => new(JsonValue.Create(value));
    }
}
