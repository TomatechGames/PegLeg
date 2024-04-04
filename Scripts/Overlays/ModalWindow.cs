using Godot;
using System;

public partial class ModalWindow : Control
{
    [Export]
    NodePath mouseBlockPanelPath;
    Control mouseBlockPanel;

    [Export]
    NodePath backgroundPanelPath;
    Control backgroundPanel;

    [Export]
    NodePath windowContentsPath;
    CanvasGroup windowContents;

    [Export]
    float tweenTime = 0.1f;
    [Export]
    float shrunkScale = 0.5f;
    [Export]
    bool startOpen;

    public override void _Ready()
    {
        this.GetNodeOrNull(mouseBlockPanelPath, out mouseBlockPanel);
        this.GetNodeOrNull(backgroundPanelPath, out backgroundPanel);
        this.GetNodeOrNull(windowContentsPath, out windowContents);

        backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
        mouseBlockPanel.MouseFilter = MouseFilterEnum.Ignore;
        backgroundPanel.SelfModulate = Colors.Transparent;
        windowContents.SelfModulate = Colors.Transparent;
        windowContents.Scale = Vector2.One * shrunkScale;
        windowContents.Visible = false;
        if (startOpen)
            SetWindowOpen(true);
        Visible = true;
    }

    Tween currentTween;
    public void SetWindowOpen(bool openState)
    {
        if (currentTween is not null && currentTween.IsRunning())
            currentTween.Kill();
        currentTween = GetTree().CreateTween().SetParallel();

        if (openState)
        {
            backgroundPanel.MouseFilter = MouseFilterEnum.Stop;
            mouseBlockPanel.MouseFilter = MouseFilterEnum.Stop;
            windowContents.Visible = true;
        }

        var newSize = openState ? 1 : shrunkScale;
        var newColour = openState ? Colors.White : Colors.Transparent;
        currentTween.TweenProperty(backgroundPanel, "self_modulate", newColour, tweenTime);
        currentTween.TweenProperty(windowContents, "self_modulate", newColour, tweenTime);
        currentTween.TweenProperty(windowContents, "scale", Vector2.One*newSize, tweenTime);
        currentTween.Finished += () =>
        {
            if (!openState)
            {
                backgroundPanel.MouseFilter = MouseFilterEnum.Ignore;
                mouseBlockPanel.MouseFilter = MouseFilterEnum.Ignore;
                windowContents.Visible = false;
            }
        };
    }
}
