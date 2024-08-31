using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

public partial class QuestViewer : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);
    [Signal]
    public delegate void DescriptionChangedEventHandler(string description);
    [Signal]
    public delegate void CompleteVisibleEventHandler(bool visible);

    [Export]
    Control objectiveParent;
    [Export]
    PackedScene objectiveScene;
    List<QuestObjective> objectiveEntries = new();

    [Export]
    CheckButton pinButton;
    [Export]
    Button rerollButton;

    [Export]
    Control rewardParent;
    [Export]
    PackedScene rewardScene;
    List<GameItemEntry> rewardEntries = new();

    public override void _Ready()
    {
        pinButton.Pressed += UpdatePinnedState;
        rerollButton.Pressed += RerollQuest;
    }

    static readonly string[] cardPackFromRarity = new string[]
    {
        "CardPack:cardpack_choice_all_r",
        "CardPack:cardpack_choice_all_r",
        "CardPack:cardpack_choice_all_r",
        "CardPack:cardpack_choice_all_r",
        "CardPack:cardpack_choice_all_vr",
        "CardPack:cardpack_choice_all_sr",
    };

    QuestData currentQuest;
    async void UpdatePinnedState()
    {
        if (currentQuest is null || !currentQuest.isUnlocked || currentQuest.isComplete)
            return;
        LoadingOverlay.Instance.AddLoadingKey("pinnedQuest");

        if (pinButton.ButtonPressed)
            await ProfileRequests.AddPinnedQuest(currentQuest.questItem.profileItem.uuid);
        else
            await ProfileRequests.RemovePinnedQuest(currentQuest.questItem.profileItem.uuid);

        pinButton.ButtonPressed = currentQuest.isPinned;
        LoadingOverlay.Instance.RemoveLoadingKey("pinnedQuest");
    }

    async void RerollQuest()
    {
        if (currentQuest is null || !currentQuest.isUnlocked)
            return;
        LoadingOverlay.Instance.AddLoadingKey("rerollQuest");
        
        var newQuest = await ProfileRequests.RerollQuest(currentQuest.questItem.profileItem.uuid);
        currentQuest.LinkQuestItem(newQuest);

        SetupQuest(currentQuest);
        rerollButton.Visible = ProfileRequests.CanRerollQuestUnsafe();

        LoadingOverlay.Instance.RemoveLoadingKey("rerollQuest");
    }

    public void SetupQuest(QuestData quest)
    {
        currentQuest = quest;
        EmitSignal(SignalName.NameChanged, quest.questTemplate["DisplayName"].ToString());
        EmitSignal(SignalName.DescriptionChanged, quest.questTemplate["Description"].ToString());
        EmitSignal(SignalName.IconChanged, quest.questTemplate.GetItemTexture());
        EmitSignal(SignalName.CompleteVisible, quest.isComplete);

        rerollButton.Visible = quest.isRerollable && ProfileRequests.CanRerollQuestUnsafe();
        pinButton.Visible = quest.isUnlocked && !quest.isComplete;
        pinButton.ButtonPressed = quest.isPinned;


        var allRewards = quest.questTemplate["Rewards"]
            .AsArray()
            .Where(r => !r["Hidden"].GetValue<bool>());

        var rewards = allRewards
            .Where(r => !r["Selectable"].GetValue<bool>())
            .Select(r => BanjoAssets.TryGetTemplate(r["Item"].ToString())?.CreateInstanceOfItem(r["Quantity"].GetValue<int>()) ?? r.AsObject())
            .Where(r => r["Item"] is null)
            .ToList();

        var dynamicRewards = allRewards
            .Where(r => r["Selectable"].GetValue<bool>());

        if (dynamicRewards.Any())
        {
            //fake a cardpack to show a choice reward
            var cardpackID = cardPackFromRarity[dynamicRewards.Select(q=>BanjoAssets.TryGetTemplate(q["Item"].ToString()).GetItemRarity()).Max()];
            JsonObject attributes = new()
            {
                ["options"] = new JsonArray(dynamicRewards.Select(r=>new JsonObject()
                {
                    ["itemType"] = r["Item"].ToString(),
                    ["attributes"] = new JsonObject(),
                    ["quantity"] = r["Quantity"].GetValue<int>()
                }).ToArray())
            };
            var choiceReward = BanjoAssets.TryGetTemplate(cardpackID).CreateInstanceOfItem(1, attributes);
            rewards.Insert(0, choiceReward);
        }


        rewardParent.Visible = false;
        for (int i = 0; i < rewards.Count; i++)
        {
            if (i >= rewardEntries.Count)
            {
                var newEntry = rewardScene.Instantiate<GameItemEntry>();
                rewardParent.AddChild(newEntry);
                rewardEntries.Add(newEntry);
            }
            rewardEntries[i].SetItemData(rewards[i]);
            rewardEntries[i].SetRewardNotification();
            rewardEntries[i].SetInteractableSmart();
            rewardEntries[i].Visible = true;
        }
        for (int i = rewards.Count; i < rewardEntries.Count; i++)
        {
            rewardEntries[i].Visible = false;
        }
        rewardParent.Visible = true;

        var objectives = quest.questTemplate["Objectives"].AsArray();
        for (int i = 0; i < objectives.Count; i++)
        {
            if (i >= objectiveEntries.Count)
            {
                var newEntry = objectiveScene.Instantiate<QuestObjective>();
                objectiveParent.AddChild(newEntry);
                objectiveEntries.Add(newEntry);
            }
            var objective = objectives[i].AsObject();
            if (objective["Hidden"]?.GetValue<bool>() ?? false)
            {
                objectiveEntries[i].Visible = false;
                continue;
            }
            int currentProgress = quest.isUnlocked ? (quest.questItem.GetItemUnsafe()["attributes"]["completion_" + objective["BackendName"].ToString()]?.GetValue<int>() ?? 0) : 0;
            objectiveEntries[i].SetupObjective(objective, currentProgress);
            objectiveEntries[i].Visible = true;
        }
        for (int i = objectives.Count; i < objectiveEntries.Count; i++)
        {
            objectiveEntries[i].Visible = false;
        }
    }
}
