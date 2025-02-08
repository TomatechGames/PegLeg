using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public partial class QuestInterface : Control
{
    [Export]
    QuestGroupViewer questGroupViewer;
    [Export]
    Control foldoutParent;
    [Export]
    PackedScene foldoutScene;
    [Export]
    PackedScene questGroupScene;
    [Export]
    Control questListLayout;
    [Export]
    Control loadingIcon;

    List<Foldout> questGroupCollections = new();
    List<QuestGroupEntry> questGroups = new();

    public override void _Ready()
	{
        VisibilityChanged += () =>
        {
            if (IsVisibleInTree())
                LoadQuests();
        };
        RefreshTimerController.OnDayChanged += ReloadQuests;
        GameAccount.ActiveAccountChanged += ReloadQuests;
        ReloadQuests();
    }

    private async void ReloadQuests()
    {
        foreach (var node in questGroupCollections)
        {
            node.QueueFree();
        }
        questGroupCollections.Clear();

        await GameAccount.activeAccount.ClientQuestLogin();

        questsDirty = true;
        if (IsVisibleInTree())
            LoadQuests();

    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnDayChanged -= ReloadQuests;
        GameAccount.ActiveAccountChanged -= ReloadQuests;
    }

    public async void ClearPinnedQuests()
    {
        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return;
        account.ClearPinnedQuests();
    }

    bool questsDirty = true;
    SemaphoreSlim loadQuestsSemaphore = new(1);
    async void LoadQuests()
    {
        using var st = await loadQuestsSemaphore.AwaitToken(() => {
            loadingIcon.Visible = false;
            questListLayout.Visible = true;
        });
        if (!questsDirty)
            return;

        if (!await GameAccount.activeAccount.Authenticate())
            return;

        questGroupViewer.Visible = false;
        questListLayout.Visible = false;
        loadingIcon.Visible = true;
        var generatedQuestGroups = QuestGroupGenerator.GetQuestGroups();

        while (questsDirty)
        {
            questsDirty = false;
            foreach (var node in questGroupCollections)
            {
                node.QueueFree();
            }
            questGroupCollections.Clear();

            ButtonGroup questButtonGroup = new();
            foreach (var collection in generatedQuestGroups)
            {
                //create foldout
                var foldout = foldoutScene.Instantiate<Foldout>();
                foldout.SetFoldoutName(collection["name"].ToString());
                List<QuestGroupEntry> groupsInFoldout = new();
                foreach (var group in collection["groupGens"].AsObject())
                {
                    var groupEntry = questGroupScene.Instantiate<QuestGroupEntry>();
                    await groupEntry.SetupQuestGroup(group.Key, group.Value.AsObject());
                    if (!groupEntry.HasAvailableQuests)
                    {
                        groupEntry.QueueFree();
                        continue;
                    }
                    groupsInFoldout.Add(groupEntry);
                    questGroups.Add(groupEntry);
                    groupEntry.LinkButtonGroup(questButtonGroup);
                    //foldout.GetInstanceId();
                    groupEntry.Pressed += () =>
                    {
                        questGroupViewer.Visible = true;
                        questGroupViewer.SetQuestNodes(groupEntry.questSlotList, groupEntry.IsChain || group.Key == "Daily Endurance", !groupEntry.IsChain);
                    };
                    groupEntry.NotificationVisible += _ =>
                    {
                        foldout.SetNotification(groupsInFoldout.Any(g => g.HasNotification));
                    };
                    foldout.AddFoldoutChild(groupEntry);
                }
                foldout.SetNotification(groupsInFoldout.Any(g => g.HasNotification));
                foldoutParent.AddChild(foldout);
                questGroupCollections.Add(foldout);
            }
        }
    }
}
