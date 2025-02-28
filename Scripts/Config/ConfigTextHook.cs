using Godot;
using System;
using System.Text.Json.Nodes;

public partial class ConfigTextHook : Control
{
    [Signal]
    public delegate void ConfigValueChangedEventHandler(string newValue);

    [Export]
    string section;

    [Export]
    string key;

    [Export]
    string defaultValue = "";

    [Export]
    bool tryBind = true;

    [Export]
    double cooldown = 1;

    bool valueIsChanging;

    public override void _Ready()
    {
        if (tryBind)
        {
            if (HasSignal("text_changed"))
            {
                Connect("text_changed", Callable.From<string>(TrySetValue));
            }
            if ((string)Get("text") is not null)
            {
                ConfigValueChanged += newVal => Set("text", newVal);
            }
        }

        base._Ready();
        AppConfig.OnConfigChanged += UpdateValue;
        valueIsChanging = true;
        EmitSignal(SignalName.ConfigValueChanged, AppConfig.Get(section, key, defaultValue));
        valueIsChanging = false;
    }

    private void UpdateValue(string section, string key, JsonValue val)
    {
        if (section != this.section || key != this.key)
            return;
        valueIsChanging = true;
        EmitSignal(SignalName.ConfigValueChanged, val.GetValue<string>());
        valueIsChanging = false;
    }

    string nextValue;
    double currentCooldown = 0;
    public void TrySetValue(string newValue)
    {
        if (currentCooldown <= 0)
            SetValue(newValue);
        else
        {
            currentCooldown = cooldown;
            nextValue = newValue;
        }
    }

    void SetValue(string newValue)
    {
        if (!valueIsChanging)
            AppConfig.Set(section, key, newValue);
    }

    public override void _Process(double delta)
    {
        if (currentCooldown <= 0)
            return;
        currentCooldown -= delta;
        if (currentCooldown <= 0)
            SetValue(nextValue);
    }
}
