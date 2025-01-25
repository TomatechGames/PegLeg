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
            if (buttons.Count <= value || value < 0)
                return;
            buttons[value].ButtonPressed = true;
            TryChangeTab(value);
        }
    }

    ButtonGroup test;

    public override void _Ready()
    {
        base._Ready();
        var possibleCheckButtons = checkButtonParent.GetChildren();
        CheckButton firstVisible = null;
        for (int i = 0; i < possibleCheckButtons.Count; i++)
        {
            CheckButton checkButton = null;
            var buttonNode = possibleCheckButtons[i];
            if(buttonNode is CheckButton cb)
                checkButton = cb;
            else if (buttonNode.FindChild("CheckButton") is CheckButton childCb)
                checkButton = childCb;
            if (checkButton is null)
                continue;
            
            buttons.Add(checkButton);
            if (checkButton.Visible)
                firstVisible ??= checkButton;
            int index = i;
            if (currentTab != -1)
                checkButton.ButtonPressed = false;
            else if (checkButton.ButtonPressed && checkButton.Visible)
            {
                currentTab = i;
            }
            checkButton.Toggled += newVal =>
            {
                if (!newVal)
                    return;
                TryChangeTab(index);
            };
        }
        if(currentTab==-1 && buttons.Count>0)
            firstVisible.ButtonPressed = true;
    }

    void TryChangeTab(int value)
    {
        if (value == currentTab)
            return;
        currentTab = value;
        EmitSignal(SignalName.TabChanged, value);
    }


}
