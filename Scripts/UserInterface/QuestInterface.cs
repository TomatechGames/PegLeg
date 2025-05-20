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

    QuestGroupCollectionData[] questGroupCollectionData;
    List<Foldout> questGroupCollections = [];
    List<QuestGroupEntry> questGroups = [];

    public override void _Ready()
	{
        VisibilityChanged += () =>
        {
            if (IsVisibleInTree())
                LoadQuests();
        };
        questGroupCollectionData = 
        [.. 
            PegLegResourceManager.LoadResourceDict<QuestGroupCollectionData>("QuestGroups/questGroupIndex.json")
            .Values
            .OrderBy(col=> col.priority)
        ];
        GD.Print("questCollections: " + questGroupCollectionData.Length);
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
        await GameAccount.activeAccount.GetProfile(FnProfileTypes.AccountItems).Query();

        questGroupViewer.Visible = false;
        questListLayout.Visible = false;
        loadingIcon.Visible = true;

        while (questsDirty)
        {
            questsDirty = false;
            foreach (var node in questGroupCollections)
            {
                node.QueueFree();
            }
            questGroupCollections.Clear();

            ButtonGroup questButtonGroup = new();
            foreach (var collection in questGroupCollectionData)
            {
                //create foldout
                var foldout = foldoutScene.Instantiate<Foldout>();
                foldout.SetFoldoutName(collection.displayName);
                List<QuestGroupEntry> groupsInFoldout = [];
                foreach (var group in collection.QuestGroups)
                {
                    var groupEntry = questGroupScene.Instantiate<QuestGroupEntry>();
                    groupEntry.SetupQuestGroup(group);
                    if (!groupEntry.HasQuests)
                    {
                        //groupEntry.QueueFree();
                        continue;
                    }
                    groupsInFoldout.Add(groupEntry);
                    questGroups.Add(groupEntry);
                    groupEntry.LinkButtonGroup(questButtonGroup);
                    //foldout.GetInstanceId();
                    groupEntry.Pressed += () =>
                    {
                        questGroupViewer.Visible = true;
                        questGroupViewer.SetQuestNodes(groupEntry);
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
