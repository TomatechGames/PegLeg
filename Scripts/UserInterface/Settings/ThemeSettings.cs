using Godot;
using System;
using System.Linq;
using System.Reflection;

public partial class ThemeSettings : Node
{
	[Export]
	OptionButton themeOptions;
	[Export]
	ShaderHook backgroundPreview;

    public override void _Ready()
    {
		var themeList = ThemeController.GetThemeList();
        themeOptions.Clear();
        themeOptions.AddItem("Default (Current Seasonal Zone)");
        themeOptions.SetItemMetadata(0, "");
        themeOptions.AddSeparator("Themes");
        for (int i = 0; i < themeList.Length; i++)
        {
            themeOptions.AddItem(themeList[i]);
            themeOptions.SetItemMetadata(i+2, themeList[i]);
        }
        themeOptions.ItemSelected += PreviewTheme;
    }

    public void UpdateActiveTheme()
    {
        var themeName = ThemeController.currentThemeName;
        if (themeName == "")
        {
            themeOptions.Selected = 0;
            PreviewTheme(0);
        }
        else
        {
            int index = ThemeController.GetThemeList().ToList().IndexOf(themeName)+2;
            themeOptions.Selected = index;
            PreviewTheme(index);
        }
    }

    private void PreviewTheme(long index)
    {
        var themeName = (string)themeOptions.GetItemMetadata((int)index);
        backgroundPreview.Texture = ThemeController.GetTheme(themeName).LoadSampleBackground();
    }

    public void ApplyTheme()
    {
        if (themeOptions.Selected == -1)
            return;
        var themeName = (string)themeOptions.GetItemMetadata(themeOptions.Selected);
        GD.Print("Applying: " + themeName);
        ThemeController.SetActiveTheme(themeName);
    }
}
