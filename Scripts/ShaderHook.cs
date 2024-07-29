using Godot;
using System;

[Tool]
public partial class ShaderHook : Control
{

    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);

    protected ShaderMaterial shaderMat;
    [Export]
    bool syncTimeProperty = false;
    [Export]
    bool syncControlSize = false;

    public override void _Ready()
    {
        ItemRectChanged += OnRectUpdated;
    }

    private void OnRectUpdated()
    {
        if (syncControlSize)
            SetShaderVector(Size, "ControlSize");
    }

    float syncTimeUntil = -1;
    public void StartSyncingTimeFor(float duration)
    {
        float currentTime = Time.GetTicksMsec() * 0.001f;
        syncTimeUntil = currentTime + duration;
    }

    public override void _Process(double delta)
    {
        float currentTime = Time.GetTicksMsec() * 0.001f;
        if (syncTimeProperty || syncTimeUntil > currentTime)
            SetShaderFloat(currentTime, "time");
    }

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

    public float GetShaderFloat(string property)
    {
        shaderMat ??= Material as ShaderMaterial;
        return (float)shaderMat.GetShaderParameter(property);
    }

    public void SetShaderColor(Color val, string property)
    {
        shaderMat ??= Material as ShaderMaterial;
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetShaderVector(Vector2 val, string property)
    {
        shaderMat ??= Material as ShaderMaterial;
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetShaderTexture(Texture val, string property)
    {
        shaderMat ??= Material as ShaderMaterial;
        shaderMat.SetShaderParameter(property, val);
    }

    public void SetMainTexture(Texture val)
    {
        EmitSignal(SignalName.IconChanged, val);
    }
}
