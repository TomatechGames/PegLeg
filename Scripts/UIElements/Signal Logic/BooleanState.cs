using Godot;
using System;

public partial class BooleanState : Node, BooleanStateProvider
{
    [Signal]
    public delegate void StateChangedEventHandler(bool value);
    bool state;
    public bool State => state;
    public void SetState(bool value) => EmitSignal(SignalName.StateChanged, state = value);
}

public interface BooleanStateProvider
{
    public bool State { get; }
}
