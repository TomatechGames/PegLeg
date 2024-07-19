using Godot;
using System;

public static class AppConfig
{
    public static event Action<string, string, Variant> OnConfigChanged;

    static ConfigFile configFile;

    public static Variant Get(string section, string key, Variant @fallback = default)
    {
        if(configFile is null)
        {
            configFile = new();
            var error = configFile.Load("appConfig");
            if(error!=Error.Ok)
                GD.Print(error);
        }
        return configFile.GetValue(section, key, @fallback);
    }

    public static void Set(string section, string key, Variant value)
    {
        if (configFile is null)
        {
            configFile = new();
            var error = configFile.Load("appConfig");
            if (error != Error.Ok)
                GD.Print(error);
        }
        configFile.SetValue(section, key, value);
        OnConfigChanged?.Invoke(section, key, value);
        configFile.Save("appConfig");
    }
}
