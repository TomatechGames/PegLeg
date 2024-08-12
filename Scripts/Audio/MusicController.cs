using Godot;
using System;
using System.Collections.Generic;

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
        ThemeController.OnThemeUpdated += OnThemeUpdated;
        OnThemeUpdated();
    }

    bool hasPlayedIntro = false;
    bool isStopping = true;
    bool isStopped = true;
    ThemeController.MusicPlaylist currentPlaylist;
    int currentTrack = -1;
    int currentLayer = -1;

    Tween currentTransition;

    async void OnThemeUpdated()
    {
        if (!isStopping)
        {
            StopMusic();
            await this.WaitForTimer(muteTime);
            ChangePlaylist();
            ResumeMusic(true);
        }
    }

    void ChangePlaylist()
    {
        int playlistIndex = ThemeController.activeTheme?.PickPlaylist() ?? -1;
        if (playlistIndex != -1)
        {
            GD.Print($"playlist {playlistIndex + 1} out of {ThemeController.activeTheme.music.Length}");
            currentPlaylist = ThemeController.activeTheme.music[playlistIndex];
        }
    }
    
    void PlayMusic() => PlayMusic(0);
    void PlayMusic(float resumeTime)
    {
        if (isStopped || currentPlaylist is null)
            return;

        bool switchTracks = GD.Randf() <= currentPlaylist.trackSwitchChance;
        bool switchLayers = GD.Randf() <= currentPlaylist.layerSwitchChance && !isStopping;

        if (!(switchTracks || switchLayers || currentTrack == -1))
        {
            musicA.Play();
            return;
        }

        if (switchTracks || currentTrack == -1)
            currentTrack = currentPlaylist.PickTrack(currentLayer);

        int prevLayer = currentLayer;
        if (switchLayers || currentLayer == -1)
        {
            currentLayer = currentPlaylist.PickLayer(currentTrack, currentLayer);
        }
        if (prevLayer == currentLayer)
            switchLayers = false;

        var track = currentPlaylist.tracks[currentTrack];
        if (switchLayers && resumeTime == 0 && prevLayer != -1)
        {
            GD.Print($"track {currentTrack + 1} out of {currentPlaylist.tracks.Length}");
            GD.Print($"layer {currentLayer + 1} out of {currentPlaylist.layerCount} (from {prevLayer + 1})");
            var fromStream = track.layers[prevLayer].fileData;
            var toStream = track.layers[currentLayer].fileData;

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
            GD.Print($"track {currentTrack+1} out of {currentPlaylist.tracks.Length}");
            if(prevLayer==-1)
                GD.Print($"layer {currentLayer + 1} out of {currentPlaylist.layerCount}");
            var toStream = track.layers[currentLayer].fileData;

            if (!hasPlayedIntro && currentPlaylist.GetIntro() is AudioStreamWav introStream)
            {
                GD.Print($"using intro");
                toStream = introStream;
                currentTrack = -1;
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
        instance.currentLayer = -1;
        instance.currentTrack = -1;
        instance.isStopping = false;
        instance.isStopped = false;
        instance.PlayMusic(time);
    }
}
