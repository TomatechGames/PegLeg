using Godot;
using System;

public partial class ShaderHook : Control
{
    protected ShaderMaterial shaderMat;
    public override void _Ready()
    {
        base._Ready();
        shaderMat = Material as ShaderMaterial;
    }

    public void SetShaderBool(bool val, string property)
    {
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetShaderFloat(float val, string property)
    {
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetShaderColor(Color val, string property)
    {
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetShaderTexture(Texture val, string property)
    {
        shaderMat.SetShaderParameter(property, val);
    }
}
