using Godot;
using System;

[Tool]
public partial class AdaptivePanelController : ShaderHook
{
    public override void _Process(double delta)
	{
        shaderMat.SetShaderParameter("ControlSize", Size);
	}
}
