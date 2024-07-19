using Godot;
using System;

[Tool]
public partial class AdaptivePanelController : TextureRect
{
    protected ShaderMaterial shaderMat;

    public void SetShaderBool(bool val, string property)
    {
        shaderMat ??= Material as ShaderMaterial;
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetShaderFloat(float val, string property)
    {
        shaderMat ??= Material as ShaderMaterial;
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetShaderColor(Color val, string property)
    {
        shaderMat ??= Material as ShaderMaterial;
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetShaderTexture(Texture val, string property)
    {
        shaderMat ??= Material as ShaderMaterial;
        shaderMat.SetShaderParameter(property, val);
    }
    public override void _Process(double delta)
    {
        shaderMat ??= Material as ShaderMaterial;
        shaderMat.SetShaderParameter("ControlSize", Size);
	}
}
