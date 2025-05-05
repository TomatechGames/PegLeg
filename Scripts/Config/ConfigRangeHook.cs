using Godot;
using System;
using System.Text.Json.Nodes;

public partial class ConfigRangeHook : Node
{
    [Signal]
    public delegate void ConfigValueChangedEventHandler(double newValue);
    [Signal]
    public delegate void UnappliedLabelChangedEventHandler(string newValue);
    [Signal]
    public delegate void AppliedChangedEventHandler(bool value);

    [Export]
    string section;

    [Export]
    string key;

    [Export]
    bool asInt;

    [Export]
    double defaultValue = 0;

    [Export]
    bool tryBind = true;

    [Export]
    bool requireApply = true;



    public override void _Ready()
    {        
        if (tryBind)
        {
            if (HasSignal("value_changed"))
            {
                Connect("value_changed", Callable.From<double>(SetValue));
            }
            if ((double?)Get("value") is double)
            {
                ConfigValueChanged += newVal => Set("value", (double)newVal);
            }
        }

        base._Ready();
        EmitSignal(SignalName.AppliedChanged, true);
        AppConfig.OnConfigChanged += UpdateValue;
        EmitSignal(SignalName.ConfigValueChanged, AppConfig.Get(section, key, defaultValue));
    }

    private void UpdateValue(string section, string key, JsonValue val)
    {
        if (section != this.section || key != this.key)
            return;
        valueIsChanging = true;
        if (val.TryGetValue(out int intVal))
            EmitSignal(SignalName.ConfigValueChanged, intVal);
        else if (val.TryGetValue(out double doubleVal))
            EmitSignal(SignalName.ConfigValueChanged, doubleVal);
        else
            GD.PushWarning($"Could not get number from config {section}:{key}");
        valueIsChanging = false;
    }

    double unappliedValue;
    public void ApplyValue()
    {
        if (requireApply)
        {
            ApplyValueTyped(unappliedValue);
            EmitSignal(SignalName.AppliedChanged, true);
        }
    }

    bool valueIsChanging;
    public void SetValue(double newValue)
    {
        if (!valueIsChanging)
        {
            if (!requireApply)
            {
                ApplyValueTyped(newValue);
                EmitSignal(SignalName.UnappliedLabelChanged, newValue.ToString()[..Mathf.Min(newValue.ToString().Length, 4)]);
                EmitSignal(SignalName.AppliedChanged, true);
            }
            else
            {
                unappliedValue = newValue;
                EmitSignal(SignalName.UnappliedLabelChanged, unappliedValue.ToString()[..Mathf.Min(unappliedValue.ToString().Length, 4)]);
                EmitSignal(SignalName.AppliedChanged, false);
            }
        }
    }

    void ApplyValueTyped(double newValue)
    {
        if(asInt)
            AppConfig.Set(section, key, (int)newValue);
        else
            AppConfig.Set(section, key, newValue);
    }
}
