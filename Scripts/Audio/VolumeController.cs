using Godot;
using System;
using System.Text.Json.Nodes;

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

    public static float GetBusVolume(string busName) =>
        AppConfig.Get("audio", $"{busName}_volume", busName == "Master" ? -20 : 0);

    public static bool GetBusMuted(string busName) =>
        AppConfig.Get("audio", $"{busName}_muted", false);

    public static void SetBusVolume(string busName, float newValue)
    {
        if (!GetBusMuted(busName))
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(busName), newValue);
        else
            GD.Print($"bus {busName} is muted");
        AppConfig.Set("audio", $"{busName}_volume", newValue);
    }

    public static void SetBusMuted(string busName, bool newValue)
    {
        int busIdx = AudioServer.GetBusIndex(busName);
        float currentVol = GetBusVolume(busName);
        GD.Print($"setting mute of {busName}({busIdx}) to {newValue} ({(newValue ? -80 : currentVol)})");
        AudioServer.SetBusVolumeDb(busIdx, newValue ? -80 : currentVol);
        AppConfig.Set("audio", $"{busName}_muted", newValue);
    }
    public static void ToggleBusMuted(string busName) =>
        SetBusMuted(busName, !GetBusMuted(busName));
}
