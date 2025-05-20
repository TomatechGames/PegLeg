using Godot;

public partial class MusicController : Node
{
    static MusicController instance;

    [Export]
    AudioStreamPlayer musicA;
    [Export]
    AudioStreamPlayer musicB;

    [Export]
    float transitionDelayMin = 5;

    [Export]
    float transitionDelayMax = 15;

    [Export]
    float transitionTime = 15;

    [Export]
    float muteTime = 1;

    public override void _Ready()
    {
        instance = this;
        musicA.Finished += PlayMusic;
        ThemeController.OnThemeChanged += OnThemeUpdated;
        OnThemeUpdated();
    }

    bool hasPlayedIntro = false;
    bool isStopping = true;
    bool isStopped = true;
    AppTheme.MusicPlaylist currentPlaylist;
    AppTheme.MusicTrack currentTrack;
    AppTheme.MusicFile currentLayer;

    Tween currentTransition;

    async void OnThemeUpdated()
    {
        if (!isStopping)
        {
            StopMusic();
            await Helpers.WaitForTimer(muteTime);
            ChangePlaylist();
            ResumeMusic(true);
        }
    }

    void ChangePlaylist()
    {
        currentPlaylist = ThemeController.activeTheme?.PickPlaylist();
    }
    
    void PlayMusic() => PlayMusic(0);
    void PlayMusic(float resumeTime)
    {
        if (isStopped || currentPlaylist is null)
            return;

        bool switchTracks = GD.Randf() <= currentPlaylist.trackSwitchChance;
        bool switchLayers = GD.Randf() <= currentPlaylist.layerSwitchChance && !isStopping;

        if (!(switchTracks || switchLayers || currentTrack is null))
        {
            musicA.Play();
            return;
        }

        if (switchTracks || currentTrack is null)
            currentTrack = currentPlaylist.PickTrack(currentTrack);

        var prevLayer = currentLayer;
        if (switchLayers || currentLayer is null)
        {
            currentLayer = currentTrack.PickLayer(currentLayer);
        }
        if (prevLayer == currentLayer)
            switchLayers = false;

        if (switchLayers && resumeTime == 0 && prevLayer is not null)
        {
            //GD.Print($"track {currentTrack + 1} out of {currentPlaylist.tracks.Length}");
            //GD.Print($"layer {currentLayer + 1} out of {currentPlaylist.layerCount} (from {prevLayer + 1})");
            var fromStream = prevLayer.File;
            var toStream = currentLayer.File;

            musicA.VolumeDb = -80;
            musicB.VolumeDb = 0;

            musicA.Stream = toStream;
            musicB.Stream = fromStream;

            musicA.Play();
            musicB.Play();

            if (currentTransition?.IsValid() ?? false)
                currentTransition.Kill();

            currentTransition = GetTree().CreateTween().SetTrans(Tween.TransitionType.Expo);

            double transitionDelay = GD.RandRange(transitionDelayMin, transitionDelayMax);
            transitionDelay = Mathf.Min(transitionDelay, toStream.GetLength() - (transitionTime + 1));

            currentTransition.TweenInterval(transitionDelay);
            currentTransition.Parallel().TweenProperty(musicA, "volume_db", 0, transitionTime).SetEase(Tween.EaseType.Out);
            currentTransition.Parallel().TweenProperty(musicB, "volume_db", -80, transitionTime).SetEase(Tween.EaseType.In);
        }
        else
        {
            //GD.Print($"track {currentTrack+1} out of {currentPlaylist.tracks.Length}");
            if(prevLayer is null)
                GD.Print($"layer {currentTrack.IndexOf(currentLayer) + 1} out of {currentTrack.Layers.Length}");
            var toStream = currentLayer.File;

            if (!hasPlayedIntro && currentPlaylist.PickIntro()?.File is AudioStream introStream)
            {
                //GD.Print($"using intro");
                toStream = introStream;
                currentTrack = null;
            }
            hasPlayedIntro = true;

            musicA.Stream = toStream;
            musicA.Play();

            if (isStopping)
                return;

            if (currentTransition?.IsValid() ?? false)
                currentTransition.Kill();

            currentTransition = GetTree().CreateTween().SetTrans(Tween.TransitionType.Expo);
            currentTransition.Parallel().TweenProperty(musicA, "volume_db", 0, resumeTime).SetEase(Tween.EaseType.Out);
        }
    }

    void StopMusicInst(float time)
    {
        if (isStopping)
            return;
        isStopping = true;
        if (currentTransition?.IsValid() ?? false)
            currentTransition.Kill();
        currentTransition = GetTree().CreateTween().SetTrans(Tween.TransitionType.Expo).SetParallel();
        currentTransition.TweenProperty(musicA, "volume_db", -80, time).SetEase(Tween.EaseType.In);
        currentTransition.TweenProperty(musicB, "volume_db", -80, time).SetEase(Tween.EaseType.In);
        currentTransition.Finished += () =>
        {
            isStopped = true;
            musicA.Stop();
            musicB.Stop();
        };
    }

    public static void StopMusic() => StopMusic(instance.muteTime);
    public static void StopMusic(float time) => instance.StopMusicInst(time);
    public static void ResumeMusic(bool playIntro = false) => ResumeMusic(instance.muteTime, playIntro);
    public static void ResumeMusic(float time, bool playIntro = false)
    {
        if (playIntro)
            instance.hasPlayedIntro = false;
        instance.ChangePlaylist();
        instance.currentLayer = null;
        instance.currentTrack = null;
        instance.isStopping = false;
        instance.isStopped = false;
        instance.PlayMusic(time);
    }
}
