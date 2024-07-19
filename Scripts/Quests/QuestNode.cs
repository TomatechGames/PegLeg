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
    QuestData questData;

    public void SetupQuestNode(QuestData questData, ButtonGroup buttonGroup)
    {
        selectedToggle.ButtonGroup = buttonGroup;
        this.questData = questData;
        RefreshQuestNode();
    }

    public void RefreshQuestNode()
    {
        EmitSignal(SignalName.NameChanged, questData.questTemplate["DisplayName"].ToString());
        EmitSignal(SignalName.IconChanged, questData.questTemplate.GetItemTexture());
        EmitSignal(SignalName.NotificationVisible, questData.hasNotif);
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
}
