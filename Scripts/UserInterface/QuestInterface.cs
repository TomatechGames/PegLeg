using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class QuestInterface : Control
{
    [Export]
    QuestNodeController nodeController;
    [Export]
    QuestViewer questViewer;
    [Export]
    Control foldoutParent;
    [Export]
    PackedScene foldoutScene;
    [Export]
    PackedScene questGroupScene;

    List<Foldout> questGroupCollections = new();
    List<QuestGroupEntry> questGroups = new();

    Foldout currentFoldout;
    QuestGroupEntry currentEntry;
    List<QuestGroupEntry> currentQuestGroups = new();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{

        VisibilityChanged += () =>
        {
            if (Visible)
                LoadQuests();
        };
        nodeController.Pressed += RefreshCurrentSelection;
        questViewer.OnRefreshNeeded += RefreshCurrentSelection;
    }

    public static event Action OnPinnedQuestsCleared;
    async void ClearPinnedQuests()
    {
        LoadingOverlay.Instance.AddLoadingKey("pinnedQuest");
        
        await ProfileRequests.ClearPinnedQuests();

        await this.WaitForFrame();

        OnPinnedQuestsCleared?.Invoke();

        foreach (var entry in questGroups)
        {
            entry.UpdateNotificationAndIcon();
        }

        LoadingOverlay.Instance.RemoveLoadingKey("pinnedQuest");
    }

    bool hasGeneratedQuests = false;
	async void LoadQuests()
    {
        if (!await LoginRequests.TryLogin() || hasGeneratedQuests)
            return;
        hasGeneratedQuests = true;
        await ProfileRequests.PerformProfileOperation(FnProfiles.AccountItems, "ClientQuestLogin", @"{""streamingAppKey"": """"}");
        var generatedQuestGroups = QuestGroupGenerator.GetQuestGroups();

        foreach (var node in questGroupCollections)
        {
            node.QueueFree();
        }

        await this.WaitForFrame();

        ButtonGroup questButtonGroup = new ButtonGroup();

        foreach (var collection in generatedQuestGroups)
        {
            //create foldout
            var foldout = foldoutScene.Instantiate<Foldout>();
            foldout.SetName(collection["name"].ToString());
            List<QuestGroupEntry> groupsInFoldout = new();
            foreach (var group in collection["groupGens"].AsObject())
            {
                var groupEntry = questGroupScene.Instantiate<QuestGroupEntry>();
                groupEntry.SetupQuestGroup(group.Key, group.Value.AsObject());
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
                    GD.Print($"Group {groupEntry}");

                    currentEntry = groupEntry;
                    currentFoldout = foldout;
                    currentQuestGroups = groupsInFoldout;

                    nodeController.SetQuestNodes(groupEntry.QuestDataList, groupEntry.IsChain ||  group.Key == "Daily Endurance", !groupEntry.IsChain);
                    RefreshCurrentSelection();
                };
                foldout.AddFoldoutChild(groupEntry);
            }
            foldout.SetNotification(groupsInFoldout.Any(g => g.HasNotification));
            foldoutParent.AddChild(foldout);
        }
    }

    void RefreshCurrentSelection()
    {
        currentEntry.UpdateNotificationAndIcon();
        currentFoldout.SetNotification(currentQuestGroups.Any(g => g.HasNotification));
    }
}
