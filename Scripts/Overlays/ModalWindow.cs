using Godot;
using System;

public partial class ModalWindow : Control
{
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

    public override void _Ready()
    {

        backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
        mouseBlockPanel.MouseFilter = MouseFilterEnum.Ignore;
        backgroundPanel.SelfModulate = Colors.Transparent;

        if (windowCanvas is not null)
        {
            windowCanvas.SelfModulate = Colors.Transparent;
            windowCanvas.Scale = Vector2.One * shrunkScale;
            windowCanvas.Visible = false;
        }
        if (windowControl is not null)
        {
            windowControl.Modulate = Colors.Transparent;
            windowControl.Scale = Vector2.One * shrunkScale;
            windowControl.Visible = false;
        }

        if (startOpen)
            SetWindowOpen(true);
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (windowControl is not null)
        {
            windowControl.PivotOffset = windowControl.Size * 0.5f;
        }
    }

    void CloseWindow() => SetWindowOpen(false);

    Tween currentTween;
    public virtual void SetWindowOpen(bool openState)
    {
        if (currentTween is not null && currentTween.IsRunning())
            currentTween.Kill();
        currentTween = GetTree().CreateTween().SetParallel();

        if (openState)
        {
            backgroundPanel.MouseFilter = MouseFilterEnum.Stop;
            mouseBlockPanel.MouseFilter = MouseFilterEnum.Stop;
            if (windowCanvas is not null)
                windowCanvas.Visible = true;
            if (windowControl is not null)
                windowControl.Visible = true;
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
                if (windowCanvas is not null)
                    windowCanvas.Visible = false;
                if (windowControl is not null)
                    windowControl.Visible = false;
            }
        };
    }
}
