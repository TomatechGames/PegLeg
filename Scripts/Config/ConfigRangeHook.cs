using Godot;
using System;
using System.Text.Json.Nodes;

public partial class ConfigRangeHook : Node
{
    [Signal]
    public delegate void ConfigValueChangedEventHandler(double newValue);

    [Export]
    string section;

    [Export]
    string key;

    [Export]
    double defaultValue = 0;

    [Export]
    bool tryBind = true;

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
        AppConfig.OnConfigChanged += UpdateValue;
        EmitSignal(SignalName.ConfigValueChanged, AppConfig.Get<double>(section, key, defaultValue));
    }

    private void UpdateValue(string section, string key, JsonValue val)
    {
        if (section != this.section || key != this.key)
            return;
        valueIsChanging = true;
        EmitSignal(SignalName.ConfigValueChanged, val.GetValue<double>());
        valueIsChanging = false;
    }

    bool valueIsChanging;
    public void SetValue(double newValue)
    {
        if (!valueIsChanging)
            AppConfig.Set(section, key, newValue);
    }
}
