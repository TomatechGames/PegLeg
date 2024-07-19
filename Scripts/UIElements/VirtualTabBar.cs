using Godot;
using System;
using System.Collections.Generic;

public partial class VirtualTabBar : Control
{
    [Signal]
    public delegate void TabChangedEventHandler(int value);
    [Export]
	Control checkButtonParent;

    List<CheckButton> buttons = new();

    int currentTab = -1;
    public int CurrentTab
    {
        get => currentTab;
        set
        {
            if (value == currentTab)
                return;
            currentTab = value;
            EmitSignal(SignalName.TabChanged, value);
        }
    }

    public override void _Ready()
    {
        base._Ready();
        var possibleCheckButtons = checkButtonParent.GetChildren();
        for (int i = 0; i < possibleCheckButtons.Count; i++)
        {
            var buttonNode = possibleCheckButtons[i];
            if (buttonNode is CheckButton checkButton)
            {
                buttons.Add(checkButton);
                int index = i;
                if (checkButton.ButtonPressed)
                    currentTab = i;
                checkButton.Toggled += newVal =>
                {
                    if (!newVal)
                        return;
                    CurrentTab = index;
                };
            }
        }
        if(currentTab==-1 && buttons.Count>0)
            buttons[0].ButtonPressed = true;
    }
    bool isUpdatingValue = false;



}
