using Godot;
using System;

public partial class TooltipFixer : Node
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        GetTree().NodeAdded += OnNodeAdded;
	}

    private void OnNodeAdded(Node node)
    {
        if (node is PopupPanel pp && pp.ThemeTypeVariation=="TooltipPanel")
        {
            pp.TransparentBg = true;
            var label = pp.GetChild<Label>(0);
            label.CustomMinimumSize = new(0, 0);
            if (label.Size.X>500)
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
}
