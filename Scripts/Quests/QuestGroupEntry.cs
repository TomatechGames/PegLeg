using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Principal;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public class QuestSlot
{
    public QuestSlot(GameItemTemplate questTemplate)
    {
        this.questTemplate = questTemplate;
    }

    public void ClearQuestItem() => LinkQuestItem(null);
    public void LinkQuestItem(GameItem newQuestItem)
    {
        if (newQuestItem == questItem)
            return;
        if (questItem is not null)
            questItem.OnChanged -= ProfileItemChanged;
        questItem = newQuestItem;
        if (questItem is null)
            return;
        OnPropertiesUpdated?.Invoke(this);
        questItem.OnChanged += ProfileItemChanged;
        questTemplate = questItem.template;
    }

    void ProfileItemChanged() => OnPropertiesUpdated?.Invoke(this);

    public string questId => questItem?.templateId ?? questTemplate.TemplateId;
    public GameItemTemplate questTemplate { get; private set; }
    public GameItem questItem { get; private set; }

    public event Action<QuestSlot> OnPropertiesUpdated;

    public bool isUnlocked => questItem?.profile is not null;
    public bool isNew => !(questItem?.IsSeen ?? true);
    public bool isPinned => questItem?.profile?.account.HasPinnedQuest(questItem.uuid) ?? false;
    public bool isComplete => questItem?.attributes["quest_state"]?.ToString() == "Claimed";
    public bool isRerollable => isUnlocked && questTemplate.Category == "DailyQuests" && questItem.profile.account.CanRerollQuest();
}

public partial class QuestGroupEntry : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);
    [Signal]
    public delegate void NotificationVisibleEventHandler(bool visible);
    [Signal]
    public delegate void PressedEventHandler();

    [Export]
    Texture2D pinnedTex;
    [Export]
    Texture2D completeTex;
    [Export]
    CheckButton highlightCheck;

    bool hasNotification = false;
    public bool HasNotification => hasNotification;
    public bool HasQuests => questSlotList.Any(q => q.isUnlocked && (questGroupData.ShowComplete || !q.isComplete));

    public List<QuestSlot> questSlotList { get; private set; } = [];
    public QuestGroupData questGroupData { get; private set; }

    public void SetupQuestGroup(QuestGroupData questGroup)
    {
        EmitSignal(SignalName.NameChanged, questGroup.displayName);
        questGroupData = questGroup;
        questSlotList.Clear();

        var profile = GameAccount.activeAccount.GetProfile(FnProfileTypes.AccountItems);

        if (questGroup.chain)
        {
            //find first quest that exists in inventory
            QuestSlot lastSlot = null;
            foreach (var qline in questGroup.Questlines)
            {
                GameItem questItem = profile.GetFirstTemplateItem(qline.FirstOrDefault()?.TemplateId);
                if (questItem is null)
                    continue;
                foreach (var quest in qline)
                {
                    questItem ??= profile.GetFirstTemplateItem(quest.TemplateId);

                    QuestSlot newData = new(quest);
                    newData.LinkQuestItem(questItem);
                    newData.OnPropertiesUpdated += UpdateNotificationAndIcon;
                    questSlotList.Add(newData);

                    lastSlot = newData;
                    questItem = null;
                }
                break;
            }

            if (lastSlot?.isComplete ?? false)
                EmitSignal(SignalName.IconChanged, completeTex);

            UpdateNotificationAndIcon(null);
            return;
        }

        foreach (var quest in questGroup.Quests)
        {
            GameItem questItem = profile.GetFirstTemplateItem(quest?.TemplateId);

            QuestSlot newData = new(quest);
            newData.LinkQuestItem(questItem);
            newData.OnPropertiesUpdated += UpdateNotificationAndIcon;
            questSlotList.Add(newData);
        }

        var enduranceQuest = questSlotList.FirstOrDefault(q => q.isUnlocked && (q.questTemplate?.DisplayName?.EndsWith("Wave 5") ?? false));
        if (enduranceQuest is not null)
            EmitSignal(SignalName.NameChanged, enduranceQuest.questTemplate.DisplayName[..^7]);

        var weeklySthQuest = questSlotList.FirstOrDefault(q => 
            q.isUnlocked && 
            q.questTemplate is GameItemTemplate qTemp &&
            qTemp.Category == "LTE_HordeV3" &&
            qTemp.DisplayName.EndsWith(" (Weekly)")
        );
        if (weeklySthQuest is not null)
            EmitSignal(SignalName.NameChanged, "Weekly STH: " + weeklySthQuest.questTemplate.DisplayName[..^9]);

        UpdateNotificationAndIcon(null);
    }
    
    public void UpdateNotificationAndIcon(QuestSlot _)
    {
        hasNotification = questSlotList.Any(questData => questData.isNew && (questGroupData.ShowComplete || !questData.isComplete));
        EmitSignal(SignalName.NotificationVisible, hasNotification);
        bool isPinned = questSlotList.Any(q => q.isPinned);
        bool isComplete = questSlotList.All(q => q.isComplete);

        if (isComplete)
            EmitSignal(SignalName.IconChanged, completeTex);
        else if (isPinned)
            EmitSignal(SignalName.IconChanged, pinnedTex);
        else
            EmitSignal(SignalName.IconChanged, (Texture2D)null);
    }

    public void LinkButtonGroup(ButtonGroup buttonGroup)
    {
        highlightCheck.ButtonGroup = buttonGroup;
    }

    public void Press()
    {
        highlightCheck.ButtonPressed = true;
        EmitSignal(SignalName.Pressed);
    }

    public override void _ExitTree()
    {
        foreach (var questData in questSlotList)
        {
            questData.LinkQuestItem(null);
        }
    }
}
