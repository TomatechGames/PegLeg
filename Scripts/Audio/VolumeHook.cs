using Godot;
using System;
using System.Text.Json.Nodes;

public partial class VolumeHook : Node
{
    [Signal]
    public delegate void UpdateBusNameEventHandler(string name);
    [Signal]
    public delegate void UpdateVolumeStateEventHandler(float volume);
    [Signal]
    public delegate void UpdateMuteIconEventHandler(Texture2D icon);

    [Export]
	string targetBusName;

    static readonly Texture2D soundOnTexture = ResourceLoader.Load<Texture2D>("res://Images/InterfaceIcons/T-Icon-Sound-On-L.png");
    static readonly Texture2D soundOffTexture = ResourceLoader.Load<Texture2D>("res://Images/InterfaceIcons/T-Icon-Sound-Off-L.png");

    public override void _Ready()
    {
        if (string.IsNullOrWhiteSpace(targetBusName))
            targetBusName = Name;
        EmitSignal(SignalName.UpdateBusName, targetBusName);
        AppConfig.OnConfigChanged += CheckConfigUpdate;

        EmitSignal(SignalName.UpdateMuteIcon, VolumeController.GetBusMuted(targetBusName) ? soundOffTexture : soundOnTexture);
        EmitSignal(SignalName.UpdateVolumeState, VolumeController.GetBusVolume(targetBusName));
    }

    void CheckConfigUpdate(string section, string key, JsonValue value)
    {
        if (section != "audio")
            return;

        if (key == $"{targetBusName}_muted")
            EmitSignal(SignalName.UpdateMuteIcon, value.GetValue<bool>() ? soundOffTexture : soundOnTexture);
        if (key == $"{targetBusName}_volume")
            EmitSignal(SignalName.UpdateVolumeState, value.GetValue<float>());
    }

    public void SetVolume(float newValue) => 
        VolumeController.SetBusVolume(targetBusName, newValue);

    public void ToggleMute() => 
        VolumeController.ToggleBusMuted(targetBusName);

    public override void _ExitTree()
    {
        AppConfig.OnConfigChanged -= CheckConfigUpdate;
    }
}
