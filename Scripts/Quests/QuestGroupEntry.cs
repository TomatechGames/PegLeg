using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public class QuestData
{
    public QuestData(string questID, JsonObject questTemplate)
    {
        this.questID = questID;
        this.questTemplate = questTemplate;
    }

    public void LinkQuestItem(ProfileItemHandle newQuestItem)
    {
        if (questItem is not null)
            questItem.OnChanged -= ProfileItemChanged;
        questItem = newQuestItem;
        if (questItem is null)
            return;
        questItem.OnChanged += ProfileItemChanged;
        questTemplate = newQuestItem.GetItemUnsafe().GetTemplate();
        questID = newQuestItem.GetItemUnsafe()["templateId"].ToString();
    }

    void ProfileItemChanged(ProfileItemHandle handle)
    {
        OnPropertiesUpdated?.Invoke();
    }

    public string questID { get; private set; }
    public JsonObject questTemplate { get; private set; }
    public ProfileItemHandle questItem { get; private set; }

    public event Action OnPropertiesUpdated;

    public bool isUnlocked => questItem is not null;
    public bool isNew => !(questItem?.ItemSeen ?? true);
    public bool isPinned => isUnlocked && ProfileRequests.HasPinnedQuest(questItem.itemID.uuid);
    public bool isComplete => questItem?.GetItemUnsafe()["attributes"]["quest_state"]?.ToString() == "Claimed";
    public bool isRerollable => isUnlocked && questItem?.GetItemUnsafe().GetTemplate()["Category"].ToString() == "DailyQuests";
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

    bool isChain = false;
    public bool IsChain => isChain;
    bool hasNotification = false;
    public bool HasNotification => hasNotification;
    bool hasAvailableQuests = false;
    public bool HasAvailableQuests => hasAvailableQuests;
    List<QuestData> questDataList = new();
    public List<QuestData> QuestDataList => questDataList;

    public async Task SetupQuestGroup(string name, JsonObject questGroup)
    {
        EmitSignal(SignalName.NameChanged, name);
        questDataList.Clear();

        if (questGroup["questlines"] is JsonArray questlines)
        {
            isChain = true;
            foreach (var qline in questlines.Select(n=>n.AsArray()))
            {
                bool skip = true;
                for (int i = 0; i < qline.Count; i++)
                {
                    string currentQuestId = qline[i].ToString();

                    ProfileItemHandle questHandle = null;
                    if (await ProfileRequests.GetFirstProfileItem(FnProfiles.AccountItems, kvp => kvp.Value["templateId"].ToString() == currentQuestId) is JsonObject currentQuest)
                        questHandle = ProfileItemHandle.CreateHandleUnsafe(new(FnProfiles.AccountItems, currentQuest["uuid"].ToString()));

                    if (i == 0 && questHandle is null)
                        break;
                    skip = false;

                    JsonObject questTemplate = BanjoAssets.TryGetTemplate(currentQuestId);

                    QuestData newData = new(currentQuestId, questTemplate);
                    newData.LinkQuestItem(questHandle);
                    newData.OnPropertiesUpdated += UpdateNotificationAndIcon;
                    questDataList.Add(newData);

                    if(i==qline.Count-1 && newData.isComplete)
                        EmitSignal(SignalName.IconChanged, completeTex);
                }
                if (skip)
                    continue;
                hasAvailableQuests = questDataList.Count > 0;

                UpdateNotificationAndIcon();
                return;
            }
        }

        if(questGroup["quests"] is JsonArray quests)
        {
            isChain = false;
            for (int i = 0; i < quests.Count; i++)
            {
                string currentQuestId = quests[i].ToString();
                JsonObject questTemplate = BanjoAssets.TryGetTemplate(currentQuestId);

                ProfileItemHandle questHandle = null;
                if (await ProfileRequests.GetFirstProfileItem(FnProfiles.AccountItems, kvp => kvp.Value["templateId"].ToString() == currentQuestId) is JsonObject currentQuest)
                    questHandle = ProfileItemHandle.CreateHandleUnsafe(new(FnProfiles.AccountItems, currentQuest["uuid"].ToString()));

                QuestData newData = new(currentQuestId, questTemplate);
                newData.LinkQuestItem(questHandle);
                newData.OnPropertiesUpdated += UpdateNotificationAndIcon;
                questDataList.Add(newData);
            }
            hasAvailableQuests = questDataList.Exists(q => q.isUnlocked);

            if(questDataList.FirstOrDefault(q => q.isUnlocked)?.questTemplate["DisplayName"].ToString() is string firstQuestName && firstQuestName.EndsWith("Wave 5"))
                EmitSignal(SignalName.NameChanged, firstQuestName[..^7]);

            UpdateNotificationAndIcon();
            return;
        }
        hasAvailableQuests = false;

        Visible = false;
        //GD.PushWarning($"Error when handling Quest Group \"{name}\"");
    }
    
    public void UpdateNotificationAndIcon()
    {
        hasNotification = questDataList.Any(questData => questData.isNew && (isChain || !questData.isComplete));
        EmitSignal(SignalName.NotificationVisible, hasNotification);
        bool isPinned = questDataList.Any(q => q.isPinned);
        bool isComplete = questDataList.All(q => q.isComplete);

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
        foreach (var questData in questDataList)
        {
            questData.LinkQuestItem(null);
        }
    }
}
