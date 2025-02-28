using Godot;
using System;

public partial class AnimatedNotificationEntry : NotificationEntry
{
    [Export]
    Control animatedTarget;
    [Export]
    Control animatedBasis;
    [Export]
    float animationDuration = 0.5f;

    public override void _Ready()
    {
        animatedTarget.OffsetLeft = 0;
    }

    bool openFinished = false;
    public override void Open()
    {
        dismisStarted = false;
        openFinished = false;
        Visible = true;
        base.Open();

        //animate in
        animatedTarget.OffsetLeft = 0;
        var animTween = GetTree().CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
        animTween.TweenProperty(animatedTarget, "offset_left", -animatedBasis.GetCombinedMinimumSize().X, animationDuration);
        animTween.Finished += () =>
        {
            openFinished = true;
        };
    }

    public override void Press()
    {
        if (dismisStarted || !openFinished)
            return;
        base.Press();
    }

    bool dismisStarted = false;
    public override async void Dismiss()
    {
        if (dismisStarted || !openFinished)
            return;
        dismisStarted = true;

        //animate out
        animatedTarget.OffsetLeft = -animatedBasis.GetCombinedMinimumSize().X;
        var animTween = GetTree().CreateTween().SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        animTween.TweenProperty(animatedTarget, "offset_left", 0, animationDuration*0.3);
        await ToSignal(animTween, Tween.SignalName.Finished);

        base.Dismiss();
    }
}
