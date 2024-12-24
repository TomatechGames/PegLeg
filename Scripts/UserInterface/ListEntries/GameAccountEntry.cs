using Godot;
using System;

public partial class GameAccountEntry : Node
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);

    [Signal]
    public delegate void TooltipChangedEventHandler(string tooltip);

    [Signal]
    public delegate void IconLoadingChangedEventHandler(bool iconLoading);

    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);

    public GameAccount currentAccount { get; protected set; }
    public void SetAccount(GameAccount account)
	{

	}
}
