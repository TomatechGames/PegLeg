using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
    bool hasAvailableQuests = false;
    public bool HasAvailableQuests => hasAvailableQuests;

    public List<QuestSlot> questSlotList { get; private set; } = [];
    bool isSequence = false;
    public bool IsSequence => isSequence;
    bool? showLocked = false;
    public bool ShowLocked => showLocked ?? isSequence;

    public async Task SetupQuestGroup(string name, JsonObject questGroup)
    {
        EmitSignal(SignalName.NameChanged, name);
        questSlotList.Clear();
        hasAvailableQuests = false;
        Visible = false;

        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return;

        showLocked = questGroup["showLocked"]?.GetValue<bool>();

        if (questGroup["questlines"] is JsonArray questlines)
        {
            isSequence = true;
            foreach (var qline in questlines.Select(n=>n.AsArray()))
            {
                bool skip = true;
                for (int i = 0; i < qline.Count; i++)
                {
                    string currentQuestId = qline[i].ToString();

                    GameItem questItem = 
                        (await account.GetProfile(FnProfileTypes.AccountItems).Query())
                        .GetFirstTemplateItem(currentQuestId);

                    if (i == 0 && questItem is null)
                        break;

                    skip = false;

                    GameItemTemplate questTemplate = questItem?.template ?? GameItemTemplate.Get(currentQuestId);

                    QuestSlot newData = new(questTemplate);
                    newData.LinkQuestItem(questItem);
                    newData.OnPropertiesUpdated += UpdateNotificationAndIcon;
                    questSlotList.Add(newData);

                    if(i==qline.Count-1 && newData.isComplete)
                        EmitSignal(SignalName.IconChanged, completeTex);
                }
                if (skip)
                    continue;
                hasAvailableQuests = questSlotList.Count > 0;

                UpdateNotificationAndIcon(null);
                Visible = true;
                return;
            }
        }

        if(questGroup["quests"] is JsonArray quests)
        {
            isSequence = questGroup["sequence"]?.GetValue<bool>() ?? false;
            for (int i = 0; i < quests.Count; i++)
            {
                string currentQuestId = quests[i].ToString();

                GameItem questItem =
                        (await account.GetProfile(FnProfileTypes.AccountItems).Query())
                        .GetFirstTemplateItem(currentQuestId);

                GameItemTemplate questTemplate = questItem?.template ?? GameItemTemplate.Get(currentQuestId);

                QuestSlot newData = new(questTemplate);
                newData.LinkQuestItem(questItem);
                newData.OnPropertiesUpdated += UpdateNotificationAndIcon;
                questSlotList.Add(newData);
            }
            hasAvailableQuests = questSlotList.Exists(q => q.isUnlocked);

            //makes the endurance daily group show which region the edurance is in
            if(
                questSlotList.FirstOrDefault(q => q.isUnlocked)?.questTemplate["DisplayName"].ToString() is string firstQuestName && 
                firstQuestName.EndsWith("Wave 5")
              )
                EmitSignal(SignalName.NameChanged, firstQuestName[..^7]);

            UpdateNotificationAndIcon(null);
            Visible = true;
            return;
        }

        //GD.PushWarning($"Error when handling Quest Group \"{name}\"");
    }
    
    public void UpdateNotificationAndIcon(QuestSlot _)
    {
        hasNotification = questSlotList.Any(questData => questData.isNew && (isSequence || !questData.isComplete));
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
