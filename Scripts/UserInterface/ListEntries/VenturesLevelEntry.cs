using Amazon.S3.Model;
using Godot;
using System;

public partial class VenturesLevelEntry : Control
{
	[Export]
	Label xpLabel;
	[Export]
	GameItemEntry itemEntry;
	[Export]
	Control missionUnlockMarker;
	[Export]
	Label missionUnlockLabel;
	[Export]
	Label ventureLevelLabel;
	[Export]
	Control bgNode;
	[Export]
	Control progressContainer;
	[Export]
	ProgressBar progress;
	[Export]
	Control prevProgressContainer;
	[Export]
	ProgressBar prevProgress;
	[Export]
	Color offColor;
	[Export]
	Color onColor;

	public override void _Ready()
	{
		//GameAccount.ActiveAccountChanged += UpdateProgress;
		//OnCustomXPChanged += UpdateProgress;
	}

	public override void _ExitTree()
	{
		parentInterface?.OnDisplayXPChanged -= UpdateProgress;
	}

	VenturesInterface parentInterface;
	public void SetInterface(VenturesInterface newParentInterface)
	{
		parentInterface?.OnDisplayXPChanged -= UpdateProgress;
		parentInterface = newParentInterface;
		parentInterface?.OnDisplayXPChanged += UpdateProgress;
	}


	public void SetInfo(GameItem item, int prevRequiredXP, int requiredXP, int nextRequiredXP, int level, int missionUnlock)
	{
		itemEntry.SetItem(item);

		progress.MinValue = requiredXP;
		progress.MaxValue = nextRequiredXP;
		progressContainer.Visible = nextRequiredXP >= 0;

		prevProgress.MinValue = prevRequiredXP;
		prevProgress.MaxValue = requiredXP;
		prevProgressContainer.Visible = prevRequiredXP >= 0;

		xpLabel.Text = $"{requiredXP.Compactify()} XP";
		xpLabel.TooltipText = requiredXP.Notate();

		missionUnlockMarker.Visible = missionUnlock > 0;
		missionUnlockLabel.Text = $"PL {missionUnlock} Missions";

		ventureLevelLabel.Text = $"Lv {level}";
		UpdateProgress();
	}

	void UpdateProgress()
	{
		if (!Visible)
			return;
		int xp = parentInterface?.DisplayXP ?? 0;
		bgNode.SelfModulate = xp < progress.MinValue ? offColor : onColor;

		SetProgressBarValue(progress, xp);
		SetProgressBarValue(prevProgress, xp);
	}

	static void SetProgressBarValue(ProgressBar bar, int xp)
	{
		bar.Visible = xp < bar.MaxValue;
		bar.Value = xp;
		int remaining = (int)bar.MaxValue - xp;
		bar.TooltipText = xp > bar.MinValue ? $"{xp.Compactify()}/{((int)bar.MaxValue).Compactify()} ({remaining.Compactify()} Remaining)" : "";
	}
}
