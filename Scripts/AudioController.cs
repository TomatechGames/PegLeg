using Godot;
using System;

public partial class AudioController : Node
{
    [Signal]
    public delegate void UpdateVolumeStateEventHandler(float volume);

    [Export]
    TextureRect muteTextureTarget;

    [Export]
	string targetBusName;
    int targetBusIndex;
    bool isUpdatingSelf = false;

    static readonly Texture2D soundOnTexture = ResourceLoader.Load<Texture2D>("res://Images/InterfaceIcons/T-Icon-Sound-On-L.png");
    static readonly Texture2D soundOffTexture = ResourceLoader.Load<Texture2D>("res://Images/InterfaceIcons/T-Icon-Sound-Off-L.png");

    public override void _Ready()
    {
        AppConfig.OnConfigChanged += CheckConfigUpdate;
        targetBusIndex = AudioServer.GetBusIndex(targetBusName);

        float volume = AppConfig.Get("audio", $"{targetBusName}_volume", AudioServer.GetBusVolumeDb(targetBusIndex)).AsSingle();
        EmitSignal(SignalName.UpdateVolumeState, volume);
        AudioServer.SetBusVolumeDb(targetBusIndex, volume);

        bool currentMuteState = AppConfig.Get("audio", $"{targetBusName}_muted", false).AsBool();
        SetMuteVisuals(currentMuteState);
        AudioServer.SetBusMute(targetBusIndex, currentMuteState);
    }

    void CheckConfigUpdate(string section, string key, Variant value)
    {
        if (isUpdatingSelf || section != "audio")
            return;
        if (key == $"{targetBusName}_muted")
            SetMuteVisuals(value.AsBool());
        if (key == $"{targetBusName}_volume")
            EmitSignal(SignalName.UpdateVolumeState, value);
    }

    public void SetVolume(float newValue)
    {
        isUpdatingSelf = true;

        AudioServer.SetBusVolumeDb(targetBusIndex, newValue);
        AppConfig.Set("audio", $"{targetBusName}_volume", newValue);

        isUpdatingSelf = false;
    }

    public void ToggleMute()
    {
        isUpdatingSelf = true;

        bool currentMuteState = AppConfig.Get("audio", $"{targetBusName}_muted", false).AsBool();
        SetMuteVisuals(!currentMuteState);
        AudioServer.SetBusMute(targetBusIndex, !currentMuteState);
        AppConfig.Set("audio", $"{targetBusName}_muted", !currentMuteState);

        isUpdatingSelf = false;
    }

    void SetMuteVisuals(bool value)
    {
        if(muteTextureTarget is not null)
        {
            muteTextureTarget.Texture = value ? soundOffTexture : soundOnTexture;
        }
    }
}
