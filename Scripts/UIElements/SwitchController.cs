using Godot;
using System;

[Tool]
public partial class SwitchController : Control
{
    [Signal]
    public delegate void SwitchIndexChangedEventHandler(int index);

    [Export]
    bool requirePressed;

    [Export]
    Button[] switchButtons;

    public int CurrentIndex { get; private set; }
    protected ButtonGroup SwitchGroup { get; private set; }

    public override void _Ready()
    {
        Initialise(-1);
    }

    protected virtual void Initialise(int defaultIndex = -1)
    {
        SwitchGroup = new();
        for (int i = 0; i < switchButtons.Length; i++)
        {
            int curIndex = i;
            switchButtons[i].Toggled += val => UpdateIndex(val, curIndex);
            switchButtons[i].ButtonGroup = SwitchGroup;
        }
        SetIndex(defaultIndex);
    }

    protected virtual void SetIndex(int pressedIndex)
    {
        for (int i = 0; i < switchButtons.Length; i++)
        {
            if (pressedIndex != -1 && switchButtons[i].ButtonPressed != (i == pressedIndex))
            {
                switchButtons[i].ButtonPressed = (i == pressedIndex);
            }
            if (pressedIndex == -1 && switchButtons[i].ButtonPressed)
                pressedIndex = i;
        }
        if (pressedIndex == -1 && requirePressed)
        {
            switchButtons[0].ButtonPressed = true;
        }
    }

    protected virtual void UpdateIndex(bool val, int index)
    {
        if (!val)
            return;
        CurrentIndex = index;
        EmitSignal(SignalName.SwitchIndexChanged, index);
    }
}
