using Godot;
using System;

public partial class CurtainOpener : Node
{
	[Export]
	ShaderHook curtain;
    [Export]
    float curtainOpenDuration = 0.25f;
    public override async void _Ready()
    {
        curtain.Visible = true;

        await Helpers.WaitForFrame();
        await Helpers.WaitForFrame();
        await Helpers.WaitForTimer(0.1f);

        var tween = GetTree().CreateTween().SetParallel();
        tween.TweenProperty(curtain, "SH_RevealScale", 1, curtainOpenDuration);
        tween.Finished += () => curtain.MouseFilter = Control.MouseFilterEnum.Ignore;
    }
}
