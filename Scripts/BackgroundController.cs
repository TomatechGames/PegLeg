using Godot;
using System;

public partial class BackgroundController : Node
{
    [Export]
    Texture2D defaultBGTex;
    [Export]
    ShaderHook bgMain;
    [Export]
    ShaderHook bgOverlay;
    [Export]
    float fadeTime = 0.5f;
    [Export]
    bool forceNoCorners;

    public override void _Ready()
    {
        ThemeController.OnThemeUpdated += OnThemeUpdated;
        currentBG = ((Texture2D)ThemeController.activeTheme?.GetBackground()) ?? defaultBGTex;
        bgMain.Texture = currentBG;
        bgOverlay.Modulate = Colors.Transparent;
        SetCorners(true);
    }

    Texture2D currentBG;
    Tween currentTransition;

    private void OnThemeUpdated()
    {
        bgOverlay.Texture = currentBG;
        currentBG = ((Texture2D)ThemeController.activeTheme?.GetBackground()) ?? defaultBGTex;
        bgMain.Texture = currentBG;
        bgOverlay.Modulate = Colors.White;

        if (currentTransition?.IsValid() ?? false)
            currentTransition.Kill();
        currentTransition = GetTree().CreateTween();
        currentTransition.Parallel().TweenProperty(bgOverlay, "modulate", Colors.Transparent, fadeTime);
    }

    public void SetCorners(bool corners)
    {
        corners &= !forceNoCorners;
        bgMain?.SetShaderBool(corners, "UseCorners");
        bgOverlay?.SetShaderBool(corners, "UseCorners");
    }

    public override void _ExitTree()
    {
        ThemeController.OnThemeUpdated -= OnThemeUpdated;
    }
}
