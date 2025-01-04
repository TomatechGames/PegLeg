using Godot;
using System;
using System.Threading.Tasks;

public partial class AccountSelector : ModalWindow
{
    [Export]
    Control selectorButtonLabel;
    [Export]
    Control selectorButtonLabelTarget;
    [Export]
    Control selectorButtonIcons;
    [Export]
    Foldout foldout;
    [Export]
    Button foldoutBtn;

    protected override string OpenSound => "WipeAppear";
    protected override string CloseSound => "WipeDisappear";

    protected override void CancelOpenImmediate()
    {
        base.CancelOpenImmediate();
    }

    protected override void BuildTween(ref Tween tween, bool openState)
    {
        if (openState)
        {
            selectorButtonIcons.Modulate = Colors.White;
            selectorButtonLabel.Modulate = Colors.Transparent;
            foldout.SetFoldoutStateImmediate(false);
        }
        else
        {
            foldout.SetFoldoutState(false);
        }
        foldoutBtn.Disabled = true;

        tween.SetParallel(false);
        tween.TweenInterval(openState ? 0 : 0.2f);
        tween.TweenInterval(0.01);
        tween.SetParallel();
        base.BuildTween(ref tween, openState);

        tween.TweenProperty(selectorButtonIcons, "modulate", openState ? Colors.Transparent : Colors.White, TweenTime);
        tween.TweenProperty(selectorButtonLabel, "modulate", openState ? Colors.White : Colors.Transparent, TweenTime);

        tween.TweenProperty(selectorButtonLabel, "custom_minimum_size:x", openState ? selectorButtonLabelTarget.GetCombinedMinimumSize().X : 0, TweenTime);

        //tween.SetParallel(false);
        //tween.TweenInterval(0.1f);
    }

    protected override void OnTweenFinished(bool openState)
    {
        base.OnTweenFinished(openState);
        if (openState)
        {
            foldoutBtn.Disabled = false;
            foldout.SetFoldoutState(true);
        }
    }
}
