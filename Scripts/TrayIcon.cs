using Godot;
using System;

public partial class TrayIcon : StatusIndicator
{
    public override void _ExitTree()
    {
		Visible = false;
    }
}
