using Godot;
using System;

public partial class TargetResolutionSetter : Node
{
	[Export]
	Control rootNode;
	[Export]
	Vector2I TargetResolution = new(1280, 720);
    [Export]
    Vector2I MinResolution = new(360, 360);
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
        var window = GetTree().Root;
        window.ContentScaleSize = TargetResolution;
        window.MinSize = MinResolution;
        rootNode?.ResetOffsets();
    }
}
