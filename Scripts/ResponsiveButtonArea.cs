using Godot;
using System;

public partial class ResponsiveButtonArea : BaseButton
{
    [Signal]
    public delegate void HoverChangedEventHandler(bool hoverState);
    [Signal]
    public delegate void HoldChangedEventHandler(bool holdState);
    [Signal]
    public delegate void HoldPressedEventHandler();
    [Signal]
    public delegate void HoldReleasedEventHandler();

    [Export]
    bool autoAttach = true;
    [Export]
    bool circleMode;
    [Export]
    ShaderHook outlineObject;
    [Export]
    float outlinePadding = 0;
    [Export]
    float resetTime = 0.1f;
    [Export]
    float hoverTime = 0.1f;
    [Export]
    float holdTime = 0.1f;
    [Export]
    bool cancelHoldOnMouseExit = true;
    [Export]
    bool alwaysSendHoldReleasedWhenPressed = false;
    [Export]
    Control target;

    [ExportGroup("Anchor Offsets")]
    [Export]
    bool useOffset = true;
    [Export]
    int baseOffset = 10;
    [Export]
    int hoverOffset = 0;
    [Export]
    int holdOffset = -2;

    [ExportGroup("Colour")]
    [Export]
    bool useColor = true;
    [Export]
    Color baseColor = Colors.Transparent;
    [Export]
    Color hoverColor = Colors.White;
    [Export]
    Color holdColor = Colors.White;

    [ExportGroup("Line Size")]
    [Export]
    bool useLineSize = true;
    [Export]
    int baseLineSize = 0;
    [Export]
    int hoverLineSize = 3;
    [Export]
    int holdLineSize = 3;
    //[ExportGroup("Anchors")]
    //[Export]
    //bool useAnchors = false;
    //[Export]
    //Vector4I fromAnchors;
    //[Export]
    //Vector4I toAnchors;

    Callable outlineFunction;
    Tween currentTween;
    Timer holdTimer;


    float LineSize
    {
        get => outlineObject?.GetShaderFloat("LineSize") ?? 0;
        set=> outlineObject?.SetShaderFloat(value, "LineSize");
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
		target ??= GetChild<Control>(0);
        holdTimer = new();
        holdTimer.Timeout += OnHoldPerformed;
        AddChild(holdTimer);
        if (useOffset)
        {
            target.OffsetTop = -baseOffset;
            target.OffsetBottom = baseOffset;
            target.OffsetLeft = -baseOffset;
            target.OffsetRight = baseOffset;
        }
        //if (useAnchors)
        //{
        //    target.AnchorTop = fromAnchors.W;
        //    target.AnchorBottom = fromAnchors.X;
        //    target.AnchorLeft = fromAnchors.Y;
        //    target.AnchorRight = fromAnchors.Z;
        //}
        if (useColor)
            target.Modulate = baseColor;

        if (useLineSize)
        {
            outlineObject?.SetShaderFloat(0, "LineSize");
        }

        if (autoAttach)
        {
            MouseEntered += () => SetHoverState(true);
            MouseExited += () => SetHoverState(false);
            VisibilityChanged += () => SetHoverState(false);
            
            ButtonDown += () => SetHeldState(true);
            ButtonUp += () => SetHeldState(false);
        }

    }

    private void OnHoldPerformed()
    {
        holdTimer.Stop();
        holdPrepped = true;
        //GD.Print("Press");
        EmitSignal(SignalName.HoldPressed);
    }
    private void OnHoldReleased()
    {
        //GD.Print("Release");
        EmitSignal(SignalName.HoldReleased);
    }

    public override void _Process(double delta)
    {
        outlineObject?.SetShaderVector(circleMode ? Vector2.Zero : (Size-new Vector2(outlinePadding, outlinePadding)), "TargetSize");
    }

    public void SetHoverState(bool newHovered)
    {
        //prevents accidental highlights when using spinbox, in which case hover events wouldnt change anyway
        if (Input.MouseMode == Input.MouseModeEnum.Captured)
            return;
        if (hovered != newHovered)
            EmitSignal(SignalName.HoverChanged, newHovered);

        if (cancelHoldOnMouseExit && !newHovered && held)
        {
            holdTimer.Stop();
            held = false;
            if (alwaysSendHoldReleasedWhenPressed && holdPrepped)
                OnHoldReleased();
            holdPrepped = false;
            EmitSignal(SignalName.HoldChanged, false);
        }

        hovered = newHovered;
        UpdateTweenState();
    }

    public void SetHeldState(bool newHeld)
    {
        if (newHeld && holdTimer.IsStopped())
        {
            holdTimer.WaitTime = holdTime;
            holdTimer.Start();
        }
        if (!newHeld && !holdTimer.IsStopped())
            holdTimer.Stop();
        if (!newHeld && holdPrepped)
        {
            if(held || alwaysSendHoldReleasedWhenPressed)
                OnHoldReleased();
            holdPrepped = false;
        }
        if (held != newHeld)
            EmitSignal(SignalName.HoldChanged, newHeld);
        held = newHeld;
        UpdateTweenState();
    }

    bool hovered = false;
    bool held = false;
    bool holdPrepped;
    public T StateCheck<T>(T onHold, T onHover, T onBase)=> held ? onHold : (hovered ? onHover : onBase);
    public void UpdateTweenState()
    {

        float offsetTarget = StateCheck(holdOffset, hoverOffset, baseOffset);
        Color colorTarget = StateCheck(holdColor, hoverColor, baseColor);
        float lineSizeTarget = StateCheck(holdLineSize, hoverLineSize, baseLineSize);
        float duration = StateCheck(holdTime - 0.05f, hoverTime, resetTime);
        PerformTween(offsetTarget, colorTarget, lineSizeTarget, duration);
    }

    public void PerformTween(float offsetTarget, Color colorTarget, float lineSizeTarget, float duration)
    {
        if (currentTween?.IsRunning() ?? false)
            currentTween.Kill();
        currentTween = GetTree().CreateTween().SetParallel();

        //if (useAnchors)
        //{
        //    Vector4I newAnchors = hovered ? toAnchors : fromAnchors;
        //    currentHoverTween.TweenProperty(target, "anchor_top", newAnchors.W, hoverTime);
        //    currentHoverTween.TweenProperty(target, "anchor_bottom", newAnchors.X, hoverTime);
        //    currentHoverTween.TweenProperty(target, "anchor_left", newAnchors.Y, hoverTime);
        //    currentHoverTween.TweenProperty(target, "anchor_right", newAnchors.Z, hoverTime);
        //}

        if (useOffset)
        {
            currentTween.TweenProperty(target, "offset_top", -offsetTarget, duration);
            currentTween.TweenProperty(target, "offset_bottom", offsetTarget, duration);
            currentTween.TweenProperty(target, "offset_left", -offsetTarget, duration);
            currentTween.TweenProperty(target, "offset_right", offsetTarget, duration);
        }

        if (useColor)
        {
            currentTween.TweenProperty(target, "modulate", colorTarget, duration);
        }

        if (useLineSize)
        {
            currentTween.TweenProperty(this, "LineSize", lineSizeTarget, duration);
        }
    }

}
