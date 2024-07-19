using Godot;
using System;
using System.Collections.Generic;

public partial class MusicController : Node
{
    static MusicController instance;

    [Export(PropertyHint.ArrayType)]
    AudioStream[] lightMusic = new AudioStream[3];

    [Export(PropertyHint.ArrayType)]
    AudioStream[] fullMusic = new AudioStream[3];

    [Export]
    AudioStream simpleMusic;

    [Export]
    bool simpleMode;

    [Export]
    NodePath musicAPath;
    AudioStreamPlayer musicA;

    [Export]
    NodePath musicBPath;
    AudioStreamPlayer musicB;

    [Export]
    NodePath musicCPath;
    AudioStreamPlayer musicC;

    [Export(PropertyHint.Range, "0,1")]
    float chanceToFlip = 0.25f;

    [Export(PropertyHint.Range, "0,1")]
    float chanceToSwitch = 0.5f;

    [Export]
    float transitionDelayMin = 5;

    [Export]
    float transitionDelayMax = 15;

    [Export]
    float transitionTime = 15;

    [Export]
    float muteTime = 1;

    [Export]
    int minSwitchesBetweenFlips = 2;

    [Export]
    int minLoopsBetweenSwitches = 2;

    public override async void _Ready()
    {
        instance = this;
        this.GetNodeOrNull(musicAPath, out musicA);
        this.GetNodeOrNull(musicBPath, out musicB);
        this.GetNodeOrNull(musicCPath, out musicC);

        musicA.Finished += PlayMusic;
        musicA.Stream = simpleMode ? simpleMusic : (isLight ? lightMusic[lastIndex] : fullMusic[lastIndex]);
        //musicA.VolumeDb = -80;
        //var transitionTween = GetTree().CreateTween().SetTrans(Tween.TransitionType.Expo);
        //transitionTween.TweenProperty(musicA, "volume_db", 0, 5).SetEase(Tween.EaseType.Out);
        await this.WaitForTimer(0.5);
        PlayMusic();
    }

    int lastIndex = 0;
    bool isLight = true;
    int loopsSinceLastSwitch = 5;
    int switchesSinceLastFlip = 5;
    bool isStopped = false;
    bool isOverridden = false;
    bool isFirst = true;

    AudioStreamPlayer currentOverridePlayer;
    Tween currentTransition;

    void PlayMusic() => PlayMusic(0);
    void PlayMusic(float resumeTime)
    {
        if (isStopped)
            return;
        if (isOverridden)
        {
            musicC.Play();
            return;
        }
        if (simpleMode)
        {
            musicA.Play();
            if (currentTransition?.IsValid() ?? false)
                currentTransition.Kill();
            currentTransition = GetTree().CreateTween().SetTrans(Tween.TransitionType.Expo);
            currentTransition.Parallel().TweenProperty(musicA, "volume_db", 0, transitionTime).SetEase(Tween.EaseType.Out);
            return;
        }
        loopsSinceLastSwitch++;
        bool shouldSwitch = GD.Randf() <= chanceToSwitch;
        if (!shouldSwitch || loopsSinceLastSwitch < minLoopsBetweenSwitches)
        {
            //GD.Print("repeating music");
            //musicA.Play();
            //return;
        }

        switchesSinceLastFlip++;

        int newIndex;
        int limit = Mathf.Min(lightMusic.Length, fullMusic.Length) - 1;
        newIndex = Mathf.FloorToInt(GD.RandRange(0, limit));
        //while (newIndex==lastIndex)
        //{
        //    newIndex = Mathf.FloorToInt(GD.RandRange(0, limit));
        //}
        bool shouldFlip = GD.Randf() <= chanceToFlip;
        if (isFirst)
        {
            newIndex = 1;
            shouldFlip = true;
            isFirst = false;
        }

        if (shouldFlip && switchesSinceLastFlip >= minSwitchesBetweenFlips && resumeTime==0)
        {
            //GD.Print("flipping music");
            var fromStream = isLight ? lightMusic[newIndex] : fullMusic[newIndex];
            var toStream = isLight ? fullMusic[newIndex] : lightMusic[newIndex];

            musicA.VolumeDb = -80;
            musicB.VolumeDb = 0;

            musicA.Stream = toStream;
            musicB.Stream = fromStream;

            musicA.Play();
            musicB.Play();

            isLight ^= true;
            switchesSinceLastFlip = 0;

            if (isOverridden)
                return;
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
            //GD.Print("switching music");
            var toStream = isLight ? lightMusic[newIndex] : fullMusic[newIndex];
            musicA.Stream = toStream;
            musicA.Play();

            currentTransition = GetTree().CreateTween().SetTrans(Tween.TransitionType.Expo);
            currentTransition.Parallel().TweenProperty(musicA, "volume_db", 0, resumeTime).SetEase(Tween.EaseType.Out);
        }
        loopsSinceLastSwitch = 0;
        lastIndex = newIndex;
    }

    void OverrideMusicInst(AudioStream replacementMusic)
    {
        if (currentTransition?.IsValid() ?? false)
            currentTransition.Kill();
        currentTransition = GetTree().CreateTween().SetTrans(Tween.TransitionType.Expo).SetParallel();
        if (replacementMusic == null && isOverridden)
        {
            currentTransition.TweenProperty(musicC, "volume_db", -80, muteTime).SetEase(Tween.EaseType.In);
            PlayMusic();
        }
        else
        {
            if (!isOverridden)
            {
                currentTransition.TweenProperty(musicA, "volume_db", -80, muteTime).SetEase(Tween.EaseType.In);
                currentTransition.TweenProperty(musicB, "volume_db", -80, muteTime).SetEase(Tween.EaseType.In);
            }
            currentTransition.TweenProperty(musicC, "volume_db", -80, muteTime).SetEase(Tween.EaseType.In);
            currentTransition.Chain().TweenCallback(Callable.From(() =>
            {
                musicC.Stream = replacementMusic;
                musicC.Play();
            }));
            currentTransition.Chain().TweenProperty(musicC, "volume_db", 0, muteTime).SetEase(Tween.EaseType.In);

            isOverridden = true;
        }
    }

    void StopMusicInst(float time)
    {
        isStopped = true;
        if (currentTransition?.IsValid() ?? false)
            currentTransition.Kill();
        currentTransition = GetTree().CreateTween().SetTrans(Tween.TransitionType.Expo).SetParallel();
        currentTransition.TweenProperty(musicA, "volume_db", -80, time).SetEase(Tween.EaseType.In);
        currentTransition.TweenProperty(musicB, "volume_db", -80, time).SetEase(Tween.EaseType.In);
        currentTransition.TweenProperty(musicC, "volume_db", -80, time).SetEase(Tween.EaseType.In);
    }

    public static void OverrideMusic(AudioStream replacementMusic = null) => instance.OverrideMusicInst(replacementMusic);

    public static void StopMusic() => StopMusic(instance.muteTime);
    public static void StopMusic(float time) => instance.StopMusicInst(time);
    public static void ResumeMusic() => ResumeMusic(instance.muteTime);
    public static void ResumeMusic(float time)
    {
        instance.isStopped = false;
        instance.PlayMusic(time);
    }
}
