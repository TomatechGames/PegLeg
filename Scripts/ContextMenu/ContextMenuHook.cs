using Godot;
using System;

public partial class ContextMenuHook : Node
{
    [Signal]
    public delegate void ContextMenuTriggeredEventHandler();
    [Export]
	public GameItemEntry itemContext;
    [Export]
    public GameOfferEntry offerContext;

    bool hover;

    public override void _Ready()
    {
        var parent = GetParent();
        if (parent is Control ctrlParent)
        {
            ctrlParent.MouseEntered += () =>
            {
                rClickWasPressed = false;
                hover = true;
            };
            ctrlParent.MouseExited += () =>
            {
                hover = false;
                rClickWasPressed = false;
                halfTriggered = false;
            };
        }
    }

    bool rClickWasPressed;
    bool halfTriggered;
    public override void _Input(InputEvent @event)
    {
        if(@event is InputEventMouseButton mbEvent)
        {
            bool rClickPressed = mbEvent.ButtonMask.HasFlag(MouseButtonMask.Right);
            bool rClickJustPressed = rClickPressed && !rClickWasPressed;
            bool rClickJustReleased = !rClickPressed && rClickWasPressed;
            rClickWasPressed = rClickPressed;

            if (hover && !halfTriggered && rClickJustPressed)
            {
                halfTriggered = true;
                return;
            }
            if (halfTriggered && rClickJustReleased)
            {
                halfTriggered = false;
                EmitSignal(SignalName.ContextMenuTriggered);
            }
        }
    }
}
