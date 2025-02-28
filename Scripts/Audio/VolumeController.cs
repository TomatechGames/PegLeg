using Godot;

public partial class VolumeController : Node
{
    static Window mainWindow;
    public override async void _Ready()
    {
        await Helpers.WaitForFrame();
        mainWindow = GetWindow();
        musicMuted = !mainWindow.HasFocus() || mainWindow.Mode == Window.ModeEnum.Minimized;
        musicVolumeScalar = musicMuted ? 0 : 1;
        RefreshVolumeLevels();
        mainWindow.FocusEntered += RefreshMusicMuteState;
        mainWindow.FocusExited += RefreshMusicMuteState;
    }

    static bool musicMuted = false;
    static float musicVolumeScalar = 1;
    float MusicVolumeScalar
    {
        get => musicVolumeScalar;
        set
        {
            musicVolumeScalar = value;
            RefreshMusicVolume();
        }
    }
    Tween musicMuteTween = null;

    void RefreshMusicMuteState()
    {
        bool newState = !mainWindow.HasFocus() || mainWindow.Mode == Window.ModeEnum.Minimized;
        if (newState == musicMuted)
            return;
        musicMuted = newState;
        if (musicMuteTween?.IsRunning() ?? false)
            musicMuteTween.Kill();
        musicMuteTween = GetTree().CreateTween();
        musicMuteTween.TweenProperty(this, "MusicVolumeScalar", musicMuted ? 0 : 1, 0.5f);
        musicMuteTween.Play();
    }

    static void RefreshMusicVolume()
    {
        var idx = AudioServer.GetBusIndex("Music");
        AudioServer.SetBusVolumeDb(idx, GetBusMuted("Music") ? -80 : GetBusVolume("Music"));
    }

    public static void RefreshVolumeLevels()
    {
        for (int i = 0; i < AudioServer.BusCount; i++)
        {
            string busName = AudioServer.GetBusName(i);
            AudioServer.SetBusVolumeDb(i, GetBusMuted(busName) ? -80 : GetBusVolume(busName));
        }
    }

    public static float GetBusVolume(string busName)
    {
        var baseValue = AppConfig.Get("audio", $"{busName}_volume", busName == "Master" ? -20 : 0);
        var invScalar = 1 - musicVolumeScalar;
        if (busName == "Music")
            return Mathf.Lerp(-80, baseValue, 1 - (invScalar * invScalar));
        return baseValue;
    }

    public static bool GetBusMuted(string busName)
    {
        return AppConfig.Get("audio", $"{busName}_muted", false);
    }

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
