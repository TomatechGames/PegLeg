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

    GameItem currentItem;
    int minLevel = 1;
    int maxLevel = 50;
    bool isShardable = false;

    public override void _Ready()
    {
        levelSlider.ValueChanged += LevelSliderChanged;
    }

    public void SetItem(GameItem item)
    {
        if (currentItem is not null)
            currentItem.OnChanged -= UpdateItem;

        currentItem = item;

        if (currentItem is not null)
        {
            currentItem.OnChanged += UpdateItem;
            UpdateItem();
        }
    }

    void UpdateItem()
    {
        int level = currentItem.attributes?["level"]?.GetValue<int>() ?? 0;
        minLevel = Mathf.Min(level, 50);
        isShardable =
            currentItem.template.Type == "Schematic" && 
            (currentItem.template.SubType ?? "Explosive") != "Explosive" && 
            (currentItem.template.Category ?? "Trap") != "Trap";

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
