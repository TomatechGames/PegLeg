using Godot;
using System;

public partial class ModalWindow : Control
{
    [Signal]
    public delegate void WindowOpenedEventHandler();
    [Signal]
    public delegate void WindowClosedEventHandler();

    [Export]
    Control mouseBlockPanel;
    [Export]
    Control backgroundPanel;
    [Export]
    CanvasGroup windowCanvas;
    [Export]
    Control windowControl;

    [Export]
    float tweenTime = 0.1f;
    [Export]
    float shrunkScale = 0.5f;
    [Export]
    bool startOpen;
    [Export]
    bool useSounds = true;

    public override void _Ready()
    {
        backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
        mouseBlockPanel.MouseFilter = MouseFilterEnum.Ignore;
        backgroundPanel.SelfModulate = Colors.Transparent;

        if (windowCanvas is not null)
        {
            windowCanvas.SelfModulate = Colors.Transparent;
            windowCanvas.Scale = Vector2.One * shrunkScale;
        }
        if (windowControl is not null)
        {
            windowControl.Modulate = Colors.Transparent;
            windowControl.Scale = Vector2.One * shrunkScale;
        }

        if (startOpen)
            SetWindowOpen(true);
    }

    public override void _Process(double delta)
    {
        openedThisFrame = false;
        if (windowControl is not null)
        {
            windowControl.PivotOffset = windowControl.Size * 0.5f;
        }
    }
    public bool IsOpen { get; private set; }
    void CloseWindow() => SetWindowOpen(false);

    Tween currentTween;
    bool openedThisFrame = false;
    public virtual void SetWindowOpen(bool openState)
    {
        if (currentTween is not null && currentTween.IsRunning())
            currentTween.Kill();
        if (openedThisFrame && !openState)
        {
            openedThisFrame = false;
            backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
            mouseBlockPanel.MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;
            UISounds.StopSound("PanelAppear");
            ProcessMode = ProcessModeEnum.Disabled;
            return;
        }
        if (openState)
            openedThisFrame = true;
        currentTween = GetTree().CreateTween().SetParallel();
        IsOpen = openState;
        if (openState)
        {
            if (useSounds)
                UISounds.PlaySound("PanelAppear");
            backgroundPanel.MouseFilter = MouseFilterEnum.Stop;
            mouseBlockPanel.MouseFilter = MouseFilterEnum.Stop;
            Visible = true;
            ProcessMode = ProcessModeEnum.Inherit;
            if (windowControl is not null)
                windowControl.PivotOffset = windowControl.Size * 0.5f;
            EmitSignal(SignalName.WindowOpened);
        }
        else
        {
            if (useSounds)
                UISounds.PlaySound("PanelDisappear");
            EmitSignal(SignalName.WindowClosed);
        }

        var newSize = openState ? 1 : shrunkScale;
        var newColour = openState ? Colors.White : Colors.Transparent;
        currentTween.TweenProperty(backgroundPanel, "self_modulate", newColour, tweenTime);

        if(windowCanvas is not null)
        {
            currentTween.TweenProperty(windowCanvas, "self_modulate", newColour, tweenTime);
            currentTween.TweenProperty(windowCanvas, "scale", Vector2.One * newSize, tweenTime);
        }

        if(windowControl is not null)
        {
            currentTween.TweenProperty(windowControl, "modulate", newColour, tweenTime);
            currentTween.TweenProperty(windowControl, "scale", Vector2.One * newSize, tweenTime);
        }

        currentTween.Finished += () =>
        {
            if (!openState)
            {
                backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
                mouseBlockPanel.MouseFilter = MouseFilterEnum.Ignore;
                Visible = false;
                ProcessMode = ProcessModeEnum.Disabled;
            }
        };
    }
}
