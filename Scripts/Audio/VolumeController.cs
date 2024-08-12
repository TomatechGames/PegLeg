using Godot;
using System;

public partial class VolumeController : Node
{
    public override void _Ready()
    {
        RefreshVolumeLevels();
    }

    public static void RefreshVolumeLevels()
    {
        for (int i = 0; i < AudioServer.BusCount; i++)
        {
            string busName = AudioServer.GetBusName(i);
            AudioServer.SetBusVolumeDb(i, GetBusMuted(busName) ? -80 : GetBusVolume(busName));
        }
    }

    public static float GetBusVolume(string busName)=>
        AppConfig.Get("audio", $"{busName}_volume", AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(busName))).AsSingle();

    public static bool GetBusMuted(string busName) =>
        AppConfig.Get("audio", $"{busName}_muted", false).AsBool();

    public static void SetBusVolume(string busName, float newValue)
    {
        if (!GetBusMuted(busName))
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(busName), newValue);
        AppConfig.Set("audio", $"{busName}_volume", newValue);
    }

    public static void SetBusMuted(string busName, bool newValue)
    {
        int busIdx = AudioServer.GetBusIndex(busName);
        AudioServer.SetBusVolumeDb(busIdx, newValue ? -80 : GetBusVolume(busName));
        AppConfig.Set("audio", $"{busName}_muted", newValue);
    }
    public static void ToggleBusMuted(string busName) =>
        SetBusMuted(busName, !GetBusMuted(busName));
}
