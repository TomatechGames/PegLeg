using Godot;
using System;

public partial class TooltipFixer : Node
{
    public override void _Ready()
	{
        GetTree().NodeAdded += OnNodeAdded;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is not PopupPanel pp)
            return;
        pp.TransparentBg = true;
        pp.Transparent = true;
        pp.PopupWindow = false;
        if (pp.ThemeTypeVariation != "TooltipPanel")
            return;
        var label = pp.GetChild<Label>(0);
        label.CustomMinimumSize = new(0, 0);
        if (label.Size.X > 500)
        {
            label.CustomMinimumSize = new(500, 0);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }
        else
        {
            label.AutowrapMode = TextServer.AutowrapMode.Off;
        }
    }
}
