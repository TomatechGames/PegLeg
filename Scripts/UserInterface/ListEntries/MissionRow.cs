using Godot;
using System;

public partial class MissionRow : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);

    [Export]
    public Control missionParent { get; private set; }

    public void SetName(string newName)
    {
        EmitSignal(SignalName.NameChanged, newName);
    }
}
