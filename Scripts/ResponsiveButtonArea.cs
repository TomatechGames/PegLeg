using Godot;
using System;

public partial class ResponsiveButtonArea : BaseButton
{
    [Signal]
    public delegate void HoverChangedEventHandler(bool hoverState);
    [Signal]
    public delegate void CustomTweenEventHandler(float tweenScale);
    [Export]
    bool autoAttach = true;
    [Export]
    float hoverTime = 0.1f;
    Control target;
    [ExportGroup("Anchor Offsets")]
    [Export]
    bool useOffset = true;
    [Export]
    int fromOffset = 10;
    [Export]
    int toOffset = 0;
    [ExportGroup("Colour")]
    [Export]
    bool useColor = true;
    [Export]
    Color fromColor = Colors.Transparent;
    [Export]
    Color toColor = Colors.White;
    [ExportGroup("Anchors")]
    [Export]
    bool useAnchors = false;
    [Export]
    Vector4I fromAnchors;
    [Export]
    Vector4I toAnchors;
    [ExportGroup("Custom Tween")]
    [Export]
    bool useCustom = false;
    [Export]
    float fromCustomValue = 0;
    [Export]
    float toCustomValue = 1;

    Callable customTweenFunction;
    Tween currentHoverTween;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
		target = GetChild<Control>(0);
        if (useOffset)
        {
            target.OffsetTop = -fromOffset;
            target.OffsetBottom = fromOffset;
            target.OffsetLeft = -fromOffset;
            target.OffsetRight = fromOffset;
        }
        if (useAnchors)
        {
            target.AnchorTop = fromAnchors.W;
            target.AnchorBottom = fromAnchors.X;
            target.AnchorLeft = fromAnchors.Y;
            target.AnchorRight = fromAnchors.Z;
        }
        if (useColor)
            target.Modulate = fromColor;

        if (useCustom)
        {
            EmitSignal(SignalName.CustomTween, fromCustomValue);
            customTweenFunction = Callable.From<float>(newValue => EmitSignal(SignalName.CustomTween, newValue));
        }

        if (autoAttach)
        {
            MouseEntered += () => SetState(true);
            MouseExited += () => SetState(false);
            VisibilityChanged += () => SetState(false);
        }

    }

    public void SetState(bool hovered)
    {
        //prevents accidental highlights when using spinbox, in which case hover events wouldnt change anyway
        if (Input.MouseMode == Input.MouseModeEnum.Captured)
            return;
        if (currentHoverTween is not null && currentHoverTween.IsRunning())
            currentHoverTween.Kill();
        currentHoverTween = GetTree().CreateTween().SetParallel();
        EmitSignal(SignalName.HoverChanged, hovered);

        if (useAnchors)
        {
            Vector4I newAnchors = hovered ? toAnchors : fromAnchors;
            currentHoverTween.TweenProperty(target, "anchor_top", newAnchors.W, hoverTime);
            currentHoverTween.TweenProperty(target, "anchor_bottom", newAnchors.X, hoverTime);
            currentHoverTween.TweenProperty(target, "anchor_left", newAnchors.Y, hoverTime);
            currentHoverTween.TweenProperty(target, "anchor_right", newAnchors.Z, hoverTime);
        }

        if (useOffset)
        {
            int newOffset = hovered ? toOffset : fromOffset;
            currentHoverTween.TweenProperty(target, "offset_top", -newOffset, hoverTime);
            currentHoverTween.TweenProperty(target, "offset_bottom", newOffset, hoverTime);
            currentHoverTween.TweenProperty(target, "offset_left", -newOffset, hoverTime);
            currentHoverTween.TweenProperty(target, "offset_right", newOffset, hoverTime);
        }
        if (useColor)
        {
            currentHoverTween.TweenProperty(target, "modulate", hovered ? toColor : fromColor, hoverTime);
        }
        if (useCustom)
        {
            float from = hovered ? fromCustomValue : toCustomValue;
            float to = hovered ? toCustomValue : fromCustomValue;
            currentHoverTween.TweenMethod(customTweenFunction, from, to, hoverTime);
        }
    }

}
