using Godot;
using System;

public partial class BooleanPair : Node
{
    [Signal]
    public delegate void ValueEventHandler(bool value);
    [Signal]
    public delegate void InvertedValueEventHandler(bool value);
    public void EmitValues(bool value)
    {
        EmitSignal(SignalName.Value, value);
        EmitSignal(SignalName.InvertedValue, !value);
    }
}
