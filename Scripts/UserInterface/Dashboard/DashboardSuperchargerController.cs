using Godot;
using System;
using System.Linq;

public partial class DashboardSuperchargerController : Control
{
    [Export]
    GameItemEntry entry;
    [Export]
    Control noSuperchargerMessage;
    [Export]
    Control checkmark;
	public override void _Ready()
	{
        GameAccount.ActiveAccountChanged += AccountChanged;
        RefreshTimerController.OnDayChanged += AccountChanged;
        entry.ClearItem();
        AccountChanged();
    }

    public override void _ExitTree()
    {
        GameAccount.ActiveAccountChanged -= AccountChanged;
        RefreshTimerController.OnDayChanged -= AccountChanged;
    }

    private async void AccountChanged()
    {
        entry.ClearItem();
        entry.Visible = false;
        checkmark.Visible = false;
        noSuperchargerMessage.Visible = false;

        await Helpers.WaitForTimer(0.1);
        var profile = await GameAccount.activeAccount.GetProfile(FnProfileTypes.AccountItems).Query(true);
        var possibleQuest = profile.GetFirstItem("Quest", q => q.templateId.StartsWith("Quest:weekly_elder"));
        GD.Print(possibleQuest?.template?.DisplayName ?? "NoSupercharger");

        checkmark.Visible = possibleQuest?.QuestComplete ?? false;
        entry.Visible = possibleQuest is not null;
        noSuperchargerMessage.Visible = possibleQuest is null;
        entry.SetItem(possibleQuest?.template?.GetVisibleQuestRewards()?.FirstOrDefault());
    }
}
