using Godot;
using System;

public partial class QuestNode : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);
    [Signal]
    public delegate void NotificationVisibleEventHandler(bool visible);
    [Signal]
    public delegate void PinnedVisibleEventHandler(bool visible);
    [Signal]
    public delegate void ColorChangedEventHandler(Color color);
    [Signal]
    public delegate void PressedEventHandler();

    [Export(PropertyHint.ArrayType)]
    Color[] colorStages = Array.Empty<Color>();
    [Export]
    CheckButton selectedToggle;
    QuestSlot questData;

    public void SetupQuestNode(QuestSlot newQuestData, ButtonGroup buttonGroup)
    {
        selectedToggle.ButtonGroup = buttonGroup;
        if (questData is not null)
            questData.OnPropertiesUpdated -= RefreshQuestNode;
        questData = newQuestData;
        questData.OnPropertiesUpdated += RefreshQuestNode;
        RefreshQuestNode(null);
    }

    void RefreshQuestNode(QuestSlot _)
    {
        EmitSignal(SignalName.NameChanged, questData.questTemplate.DisplayName);
        EmitSignal(SignalName.IconChanged, questData.questTemplate.GetTexture());
        EmitSignal(SignalName.NotificationVisible, questData.isNew);
        EmitSignal(SignalName.PinnedVisible, questData.isPinned);

        int colorIndex = 0;
        if (questData.isComplete)
            colorIndex = 2;
        else if (questData.isUnlocked)
            colorIndex = 1;
        EmitSignal(SignalName.ColorChanged, colorStages[colorIndex]);
    }

    public void Press()
    {
        selectedToggle.ButtonPressed = true;
        EmitSignal(SignalName.Pressed);
        EmitSignal(SignalName.NotificationVisible, false);
    }

    public override void _ExitTree()
    {
        if (questData is not null)
            questData.OnPropertiesUpdated -= RefreshQuestNode;
    }
}
