using Godot;
using System;

[Tool]
public partial class ShaderHook : Control
{
    [Export]
    bool syncTimeProperty = false;
    [Export]
    bool syncControlSize = false;
    [Export]
    double modTime = 0;

    //for Labels
    public string Text
    {
        get => Get("text").As<string>();
        set => Set("text", value);
    }

    //for TextureRects
    public Texture2D Texture
    {
        get => Get("texture").As<Texture2D>();
        set => Set("texture", value);
    }

    public override void _Ready()
    {
        ItemRectChanged += OnRectUpdated;
    }

    private void OnRectUpdated()
    {
        if (Material is null)
            return;
        if (syncControlSize)
            SetShaderVector(Size, "ControlSize");
    }

    ulong syncTimeUntil = 0;
    public void StartSyncingTimeFor(float duration)
    {
        ulong durationMsec = (ulong)(Mathf.Max(duration, 0) * 1000);
        syncTimeUntil = Time.GetTicksMsec() + durationMsec;
    }

    ulong timeOffset;
    public override void _Process(double delta)
    {
        if (Material is null)
            return;
        UpdateTime(Time.GetTicksMsec());
        if (Engine.IsEditorHint())
            SetShaderVector(Size, "ControlSize");
    }

    public void SetTimeOffset(ulong offset)
    {
        ulong currentTimeMsec = Time.GetTicksMsec();
        timeOffset = offset > currentTimeMsec ? currentTimeMsec : offset;
        UpdateTime(currentTimeMsec);
    }

    void UpdateTime(ulong currentTimeMsec)
    {
        if (!syncTimeProperty && syncTimeUntil <= currentTimeMsec)
            return;

        double currentTimeSec = (currentTimeMsec - timeOffset) * 0.001;
        if (modTime > 0)
            currentTimeSec %= modTime;
        SetShaderFloat((float)currentTimeSec, "time");
    }

    ShaderMaterial ShaderMat => Material as ShaderMaterial;

    public void SetShaderBool(bool val, string property)=>
        ShaderMat.SetShaderParameter(property, val);

    public void SetShaderFloat(float val, string property)=>
        ShaderMat.SetShaderParameter(property, val);

    public float GetShaderFloat(string property) =>
        (float)ShaderMat.GetShaderParameter(property);

    public void SetShaderColor(Color val, string property)=>
        ShaderMat.SetShaderParameter(property, val);

    public void SetShaderVector(Vector2 val, string property)=>
        ShaderMat.SetShaderParameter(property, val);

    public void SetShaderTexture(Texture val, string property)=>
        ShaderMat.SetShaderParameter(property, val);
}
