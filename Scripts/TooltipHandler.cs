using Godot;
using System;

public partial class TooltipHandler : Control
{
    [Export]
    PackedScene tooltipBasisScene;
    Control tooltipInstance;
    Label tooltipLabel;

    public override void _Ready()
    {
        base._Ready();
        tooltipInstance = CreateTooltip();
    }

    Control CreateTooltip()
    {
        var created = tooltipBasisScene.Instantiate<Control>();
        if (created.FindChild("tooltipLabel", true) is Label foundLabel)
            tooltipLabel = foundLabel;
        return created;
    }

    public override GodotObject _MakeCustomTooltip(string forText)
    {
        tooltipInstance ??= CreateTooltip();
        if (tooltipLabel is not null)
            tooltipLabel.Text = forText;
        GD.Print("tooltipping");
        return tooltipInstance;
    }
}
