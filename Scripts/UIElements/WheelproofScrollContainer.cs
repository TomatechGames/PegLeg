using Godot;
using System;

public partial class WheelproofScrollContainer : ScrollContainer
{
    [Export]
    bool allowOnShift = true;
    [Export]
    ScrollContainer parentContainer;

    public override void _Ready()
    {
        base._Ready();

        GetHScrollBar().AddChild(new ScrollWheelBlocker(allowOnShift));
        GetVScrollBar().AddChild(new ScrollWheelBlocker(allowOnShift));

        if (parentContainer is not null)
            return;
        Control currentParent = GetParent<Control>();
        for (int i = 0; i < 5; i++)
        {
            if (currentParent is ScrollContainer scrollParent)
            {
                parentContainer = scrollParent;
                break;
            }
            currentParent = currentParent.GetParent<Control>();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.IsPressed() && (mb.ButtonIndex==MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown) && !(allowOnShift && mb.ShiftPressed))
            {
                GD.Print("ScrollContainer:" + (allowOnShift && mb.ShiftPressed));
                GD.Print(parentContainer?.Name);
                parentContainer._GuiInput(@event);
                return;
            }
        }
        //base._GuiInput(@event);
    }
}
