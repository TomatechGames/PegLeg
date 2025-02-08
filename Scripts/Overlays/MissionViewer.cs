using Godot;
using System;

public partial class MissionViewer : ModalWindow
{
	static MissionViewer instance;
	[Export]
	MissionEntry missionEntry;

	public override void _Ready()
	{
		instance = this;

        base._Ready();
	}

	public static void ShowMission(GameMission mission)
	{
		instance.missionEntry.SetMission(mission);
		instance.SetWindowOpen(true);
	}
}
