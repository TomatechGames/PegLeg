using Godot;
using System;

public partial class WindowSizeDragger : Control
{
    bool isDragging;
    Vector2I windowSizeStart;
    Vector2I windowPosStart;
    Vector2I mouseStart;
    [Export]
    GrabberType grabberType;

    public enum GrabberType
    {
        Top = 0,
        Left = 1,
        Bottom = 2,
        Right = 3
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                isDragging = !isDragging;
                var window = GetWindow();
                windowPosStart = window.Position;
                windowSizeStart = window.Size;
                mouseStart = DisplayServer.MouseGetPosition();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (isDragging)
        {
            bool attachToWindow = ((int)grabberType)<2;
            Vector2I axisScale = ((int)grabberType) % 2 == 0 ? Vector2I.Down : Vector2I.Right;
            var window = GetWindow();

            window.Size = windowSizeStart + ((DisplayServer.MouseGetPosition() - mouseStart) * axisScale * (attachToWindow ? -1 : 1));

            if(attachToWindow)
                window.Position = windowPosStart + ((DisplayServer.MouseGetPosition() - mouseStart) * axisScale);
        }
    }
}
