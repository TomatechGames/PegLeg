using Godot;
using System;

public partial class InvertVisibility : Control
{
	[Export]
	Control target;
	public void SetVisibleInverted(bool value) => (target ??= this).Visible = !value;
}
