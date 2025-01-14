using Godot;
using System;
using System.Threading.Tasks;

public partial class ModalWindow : Control
{
    [Signal]
    public delegate void WindowOpenedEventHandler();
    [Signal]
    public delegate void WindowClosedEventHandler();

    [Export]
    Control backgroundPanel;
    [Export]
    CanvasGroup windowCanvas;
    [Export]
    Control windowControl;

    [Export]
    float tweenTime = 0.1f;
    protected float TweenTime => tweenTime;
    [Export]
    float shrunkScale = 0.5f;
    [Export]
    bool startOpen;
    [Export]
    bool useSounds = true;
    protected bool UseSounds => useSounds;

    public override void _Ready()
    {
        backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
        MouseFilter = MouseFilterEnum.Ignore;
        backgroundPanel.Modulate = Colors.Transparent;

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
    void OpenWindow() => SetWindowOpen(true);
    void CloseWindow() => SetWindowOpen(false);

    Tween currentTween;
    bool openedThisFrame = false;
    public float Dummy = 0;
    public virtual void SetWindowOpen(bool openState)
    {
        if (openState == IsOpen || !IsInstanceValid(this))
            return;
        if (currentTween is not null && currentTween.IsRunning())
            currentTween.Kill();
        IsOpen = openState;
        if (openedThisFrame && !openState)
        {
            CancelOpenImmediate();
            return;
        }
        if (openState)
        {
            openedThisFrame = true;
            WhileOpen().StartTask();
        }
        currentTween = GetTree().CreateTween().SetParallel();
        BuildTween(ref currentTween, openState);
        currentTween.Finished += () =>
        {
            if (IsInstanceValid(this))
                OnTweenFinished(openState);
        };
        currentTween.Play();
    }

    protected virtual async Task WhileOpen()
    {
        while (IsOpen)
            await Helpers.WaitForFrame();
    }

    protected virtual string OpenSound => "PanelAppear";
    protected virtual string CloseSound => "PanelDisappear";

    protected virtual void CancelOpenImmediate()
    {
        backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        UISounds.StopSound(OpenSound);
        ProcessMode = ProcessModeEnum.Disabled;
    }

    protected virtual void BuildTween(ref Tween tween, bool openState)
    {
        if (openState)
        {
            if (useSounds)
                UISounds.PlaySound(OpenSound);
            backgroundPanel.MouseFilter = MouseFilterEnum.Stop;
            MouseFilter = MouseFilterEnum.Stop;
            Visible = true;
            ProcessMode = ProcessModeEnum.Inherit;
            if (windowControl is not null)
                windowControl.PivotOffset = windowControl.Size * 0.5f;
            EmitSignal(SignalName.WindowOpened);
        }
        else
        {
            if (useSounds)
                UISounds.PlaySound(CloseSound);
            EmitSignal(SignalName.WindowClosed);
        }

        var newSize = openState ? 1 : shrunkScale;
        var newColour = openState ? Colors.White : Colors.Transparent;
        tween.TweenProperty(backgroundPanel, "modulate", newColour, tweenTime);

        if (windowCanvas is not null)
        {
            tween.TweenProperty(windowCanvas, "self_modulate", newColour, tweenTime);
            tween.TweenProperty(windowCanvas, "scale", Vector2.One * newSize, tweenTime);
        }

        if (windowControl is not null)
        {
            tween.TweenProperty(windowControl, "modulate", newColour, tweenTime);
            tween.TweenProperty(windowControl, "scale", Vector2.One * newSize, tweenTime);
        }
    }

    protected virtual void OnTweenFinished(bool openState)
    {
        if (!openState)
        {
            backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;
            ProcessMode = ProcessModeEnum.Disabled;
        }
    }
}
