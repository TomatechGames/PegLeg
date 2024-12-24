using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

    // Called when the node enters the scene tree for the first time.
    public override async void _Ready()
	{
        VisibilityChanged += () =>
        {
            if (IsVisibleInTree())
                LoadQuests();
        };
        RefreshTimerController.OnDayChanged += OnDayChanged;

        var account = GameAccount.activeAccount;
        if (await account.Authenticate())
            return;
        await account.ClientQuestLogin();
    }

    private async void OnDayChanged()
    {
        questsNeedUpdate = true;

        var account = GameAccount.activeAccount;
        if (await account.Authenticate())
            return;
        await account.ClientQuestLogin();
        if (IsVisibleInTree())
            LoadQuests();
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnDayChanged -= OnDayChanged;
    }

    public async void ClearPinnedQuests()
    {
        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return;
        account.ClearPinnedQuests();
    }

    bool questsNeedUpdate = true;
    bool isGeneratingQuests = false;
    async void LoadQuests()
    {
        if (isGeneratingQuests || !questsNeedUpdate)
            return;
        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return;

        questGroupViewer.Visible = false;
        questListLayout.Visible = false;
        loadingIcon.Visible = true;
        isGeneratingQuests = true;
        questsNeedUpdate = false;
        var generatedQuestGroups = QuestGroupGenerator.GetQuestGroups();

        foreach (var node in questGroupCollections)
        {
            node.QueueFree();
        }

        await Helpers.WaitForFrame();

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
                    questGroupViewer.SetQuestNodes(groupEntry.questSlotList, groupEntry.IsChain ||  group.Key == "Daily Endurance", !groupEntry.IsChain);
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

        questListLayout.Visible = true;
        loadingIcon.Visible = false;
        isGeneratingQuests = false;
        if (questsNeedUpdate)
            LoadQuests();
    }
}
