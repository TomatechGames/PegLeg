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
    [Signal]
    public delegate void OnRefreshNeededEventHandler();

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
    Control rewardLoadingIcon;
    [Export]
    Control rewardParent;
    [Export]
    PackedScene rewardScene;
    List<GameItemEntry> rewardEntries = new();

    public override void _Ready()
    {
        pinButton.Pressed += UpdatePinnedState;
        rerollButton.Pressed += RerollQuest;
        Visible = false;
        QuestInterface.OnPinnedQuestsCleared += () =>
        {
            if (currentQuest is null || !currentQuest.isUnlocked)
                return;
            pinButton.ButtonPressed = currentQuest.isPinned;
        };
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
    QuestData rewardTaskQuest;
    public async void UpdatePinnedState()
    {
        if (currentQuest is null || !currentQuest.isUnlocked)
            return;
        LoadingOverlay.Instance.AddLoadingKey("pinnedQuest");
        await this.WaitForFrame();
        if (pinButton.ButtonPressed)
            await ProfileRequests.AddPinnedQuest(currentQuest.questItem.profileItem.uuid);
        else
            await ProfileRequests.RemovePinnedQuest(currentQuest.questItem.profileItem.uuid);

        EmitSignal(SignalName.OnRefreshNeeded);
        pinButton.ButtonPressed = currentQuest.isPinned;

        LoadingOverlay.Instance.RemoveLoadingKey("pinnedQuest");
    }
    public async void RerollQuest()
    {
        if (currentQuest is null || !currentQuest.isUnlocked)
            return;
        LoadingOverlay.Instance.AddLoadingKey("rerollQuest");
        
        var newQuest = await ProfileRequests.RerollQuest(currentQuest.questItem.profileItem.uuid);
        currentQuest.ReplaceQuest(newQuest);

        EmitSignal(SignalName.OnRefreshNeeded);
        rerollButton.Visible = ProfileRequests.CanRerollQuestUnsafe();

        LoadingOverlay.Instance.RemoveLoadingKey("rerollQuest");
    }

    public void SetupQuest(QuestData quest)
    {
        Visible = true;
        currentQuest = quest;
        EmitSignal(SignalName.NameChanged, quest.questTemplate["DisplayName"].ToString());
        EmitSignal(SignalName.DescriptionChanged, quest.questTemplate["Description"].ToString());
        EmitSignal(SignalName.IconChanged, quest.questTemplate.GetItemTexture());

        rerollButton.Visible = quest.isRerollable && ProfileRequests.CanRerollQuestUnsafe();

        pinButton.Visible = false;
        EmitSignal(SignalName.CompleteVisible, false);
        if (quest.isUnlocked)
        {
            if (quest.isComplete)
                EmitSignal(SignalName.CompleteVisible, true);
            pinButton.Visible = true;
            pinButton.ButtonPressed = quest.isPinned;
        }


        var allRewards = quest.questTemplate["Rewards"]
            .AsArray()
            .Where(r => !r["Hidden"].GetValue<bool>());

        var rewards = allRewards
            .Where(r => !r["Selectable"].GetValue<bool>())
            .Select(r => 
            {
                //if (r["Item"].ToString().StartsWith("STWAccoladeReward"))
                //{
                //    var accoladeID = r["Item"].ToString().Replace("STWAccoladeReward:stwaccolade_", "Accolades:accoladeid_stw_");
                //    GD.Print(accoladeID);
                //    var accoladeTemplate = BanjoAssets.TryGetTemplate(accoladeID);
                //    return accoladeTemplate?.CreateInstanceOfItem(1) ?? r.AsObject();
                //}
                return BanjoAssets.TryGetTemplate(r["Item"].ToString())?.CreateInstanceOfItem(r["Quantity"].GetValue<int>()) ?? r.AsObject();
            })
            .Where(r =>
            {
                bool isBadReward = r["Item"] is not null;
                if (isBadReward)
                    GD.Print("bad reward detected: " + r);
                return !isBadReward;
            })
            .ToList();

        var dynamicRewards = allRewards
            .Where(r => r["Selectable"].GetValue<bool>());

        if (dynamicRewards.Count()>0)
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

        async void SetRewards()
        {
            rewardTaskQuest = currentQuest;
            rewardParent.Visible = false;
            for (int i = 0; i < rewards.Count; i++)
            {
                if (i >= rewardEntries.Count)
                {
                    var newEntry = rewardScene.Instantiate<GameItemEntry>();
                    rewardParent.AddChild(newEntry);
                    rewardEntries.Add(newEntry);
                }
                await rewards[i].SetItemRewardNotification();
                if (currentQuest != rewardTaskQuest)
                    return;
                rewardEntries[i].SetItemData(rewards[i]);
                rewardEntries[i].SetInteractableSmart();
                rewardEntries[i].Visible = true;
            }
            for (int i = rewards.Count; i < rewardEntries.Count; i++)
            {
                rewardEntries[i].Visible = false;
            }
            rewardParent.Visible = true;
        }
        if (rewardTaskQuest != currentQuest)
            SetRewards();

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
