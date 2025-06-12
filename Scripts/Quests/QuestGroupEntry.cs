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
        questTemplate = questItem.template;
        ProfileItemChanged();
        questItem.OnChanged += ProfileItemChanged;
    }

    public string questId => questItem?.templateId ?? questTemplate.TemplateId;
    public GameItemTemplate questTemplate { get; private set; }
    public GameItem questItem { get; private set; }

    void ProfileItemChanged() => OnPropertiesUpdated?.Invoke();
    public event Action OnPropertiesUpdated;

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
    public delegate void NotificationVisibleEventHandler(bool visible);
    [Signal]
    public delegate void PressedEventHandler();

    [Export]
    Control pinnedIcon;
    [Export]
    Control completeIcon;
    [Export]
    Control notification;
    [Export]
    CheckButton highlightCheck;
    [Export]
    RefreshTimerHook eventTimer;
    [Export]
    ProgressBar sequenceProgress;

    bool hasNotification = false;
    public bool HasNotification => hasNotification;
    public bool HasQuests => questSlotList.Any(q => q.isUnlocked && (questGroupData.ShowComplete || !q.isComplete));

    public List<QuestSlot> questSlotList { get; private set; } = [];
    public QuestGroupData questGroupData { get; private set; }

    public void SetupQuestGroup(QuestGroupData questGroup)
    {
        EmitSignalNameChanged(questGroup.displayName);
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

            UpdateSequenceProgress();
            UpdateEventTimer();
            UpdateNotificationAndIcon();
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

        UpdateSequenceProgress();
        UpdateEventTimer();
        UpdateNotificationAndIcon();
    }

    public void UpdateSequenceProgress()
    {
        if (!questGroupData.ShowLocked || !questGroupData.ShowComplete)
        {
            sequenceProgress.Visible = false;
            return;
        }
        sequenceProgress.Visible = true;
        sequenceProgress.MaxValue = questSlotList.Count;
        sequenceProgress.Value = questSlotList.Count(q => q.isComplete);
        sequenceProgress.SelfModulate = 
            sequenceProgress.MaxValue == sequenceProgress.Value ?
            Colors.Green : Colors.Yellow;
    }

    public void UpdateEventTimer()
    {
        eventTimer.Visible = true;
        switch (questGroupData.timer)
        {
            case QuestGroupData.TimerMode.Weekly:
                eventTimer.SetTimerType(2);
                return;
            case QuestGroupData.TimerMode.Daily:
                eventTimer.SetTimerType(1);
                return;
            case QuestGroupData.TimerMode.None:
                eventTimer.Visible = false;
                return;
        }
        if (CalenderRequests.EventFlagActive(questGroupData.eventFlag))
        {
            var endDate = CalenderRequests.EventEnd(questGroupData.eventFlag);
            //Hunt The Titan ends in 2027
            if ((endDate - DateTime.UtcNow).TotalDays < 100)
            {
                eventTimer.SetCustomRefreshTime(
                    endDate, 
                    CalenderRequests.EventStart(questGroupData.eventFlag)
                );
                return;
            }
        }
        eventTimer.Visible = false;
    }
    
    public void UpdateNotificationAndIcon()
    {
        var notif = questSlotList.Any(questData => questData.isNew && (questGroupData.ShowComplete || !questData.isComplete));
        notification.Visible = notif;
        EmitSignalNotificationVisible(notif);
        pinnedIcon.Visible = questSlotList.Any(q => (!questGroupData.ShowLocked || q.isUnlocked) && q.isPinned);
        completeIcon.Visible = questSlotList.All(q => (!questGroupData.ShowLocked || q.isUnlocked) && q.isComplete);
    }

    public void LinkButtonGroup(ButtonGroup buttonGroup)
    {
        highlightCheck.ButtonGroup = buttonGroup;
    }

    public void Press()
    {
        highlightCheck.ButtonPressed = true;
        EmitSignalPressed();
    }

    public override void _ExitTree()
    {
        foreach (var questData in questSlotList)
        {
            questData.LinkQuestItem(null);
        }
    }
}
