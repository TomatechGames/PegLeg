using Godot;
using System;

public partial class GameItemUpgrader : Control
{
    [Export]
    Slider levelSlider;

    [Export]
    ShaderHook sliderBG;

    [Export]
    Label currentLevelLabel;

    [Export]
    Label extraLevelLabel;

    [Export]
    Control costItemLayout;

    [Export]
    Control weaponPathLayout;

    [Export]
    GameItemEntry[] costItems;

    ProfileItemHandle linkedItem;
    int minLevel = 1;
    int maxLevel = 50;
    bool isShardable = false;

    public override void _Ready()
    {
        levelSlider.ValueChanged += LevelSliderChanged;
    }

    public void LinkItem(ProfileItemHandle profileItem)
    {
        if (linkedItem is not null)
            linkedItem.OnChanged -= UpdateProfileItem;

        linkedItem = profileItem;

        if (linkedItem is not null)
        {
            linkedItem.OnChanged += UpdateProfileItem;
            UpdateProfileItem(profileItem);
        }
    }

    void UpdateProfileItem(ProfileItemHandle profileItem)
    {
        var template = profileItem.GetItemUnsafe().GetTemplate();
        int level = profileItem.GetItemUnsafe()["attributes"]["level"].GetValue<int>();
        minLevel = level;
        isShardable = 
            template["Type"]?.ToString() == "Schematic" && 
            (template["SubType"]?.ToString() ?? "Explosive") != "Explosive" && 
            (template["Category"]?.ToString() ?? "Trap") != "Trap";

        //set max level based on owned homebase nodes

        currentLevelLabel.Text = $"Level {level}";
        sliderBG.SetShaderFloat(minLevel, "minVal");
        sliderBG.SetShaderFloat(maxLevel == 50 ? 100 : maxLevel, "limitVal");
        levelSlider.Value = level;
    }

    private void LevelSliderChanged(double value)
    {
        value = Mathf.Clamp(value, minLevel, maxLevel);
        levelSlider.SetValueNoSignal(value);
        extraLevelLabel.Visible = value > minLevel;
        extraLevelLabel.Text = "+"+(int)(value-minLevel);
        weaponPathLayout.Visible = isShardable && minLevel <= 40 && value > 40;
        sliderBG.SetShaderFloat((float)value, "currentVal");

        //refresh item costs
        if (costItemLayout is not null)
            costItemLayout.Visible = false;
    }
}
