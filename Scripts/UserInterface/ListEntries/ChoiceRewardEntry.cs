using Godot;
using System;
using System.Text.Json.Nodes;

public partial class ChoiceRewardEntry : Control
{
    [Export]
    double timerTime = 5;
    [Export]
    double tweenTime = 0.1;

    [Export]
    NodePath fromItemEntryPath;
    GameItemEntry fromItemEntry;
    [Export]
    NodePath toItemEntryPath;
    GameItemEntry toItemEntry;

    [Export]
    NodePath fromDividerPath;
    Control fromDivider;
    [Export]
    NodePath toDividerPath;
    Control toDivider;

    [Export]
    NodePath timerBarPath;
    TextureProgressBar timerBar;


    JsonObject[] itemDatas;
    JsonObject currentItemData;
    SceneTreeTimer timer;
    int nextIndex = 0;
    Tween dividerTween;

    public override void _Ready()
	{
        this.GetNodeOrNull(fromItemEntryPath, out fromItemEntry);
        this.GetNodeOrNull(toItemEntryPath, out toItemEntry);

        this.GetNodeOrNull(fromDividerPath, out fromDivider);
        this.GetNodeOrNull(toDividerPath, out toDivider);

        this.GetNodeOrNull(timerBarPath, out timerBar);
    }

    async void StartTimer()
    {
        if(timer is not null)
        {
            timer.TimeLeft = timerTime;
            return;
        }
        timer = GetTree().CreateTimer(timerTime);
        await this.WaitForTimer(timer);
        timer = null;
        AnimateToNextItem();
    }


    public void AnimateToNextItem()
    {
        if (dividerTween?.IsRunning() == true)
            return;

        StartTimer();
        if (itemDatas.Length <= 1)
            return;

        IncrementItem();

        fromDivider.AnchorRight = 1;
        toDivider.AnchorLeft = 1;

        dividerTween = GetTree().CreateTween().SetParallel();
        dividerTween.TweenProperty(fromDivider, "anchor_right", 0, tweenTime);
        dividerTween.TweenProperty(toDivider, "anchor_left", 0, tweenTime);
        dividerTween.SetTrans(Tween.TransitionType.Circ);
    }

    public void SkipToNextItem()
    {
        StartTimer();
        dividerTween?.Stop();
        IncrementItem();
        fromDivider.AnchorRight = 0;
        toDivider.AnchorLeft = 0;
    }

    public void SetItemTypes(JsonObject[] itemDatas)
    {
        if (itemDatas.Length == 0)
            return;
        this.itemDatas = itemDatas;
        nextIndex = -1;
        SkipToNextItem();
    }

    void IncrementItem()
    {
        nextIndex++;
        if (nextIndex >= itemDatas.Length)
            nextIndex = 0;
        fromItemEntry.SetItemData(currentItemData);
        currentItemData = itemDatas[nextIndex];
        toItemEntry.SetItemData(currentItemData);
    }

    public override void _Process(double delta)
    {
        if (timer is not null)
            timerBar.Value = timer.TimeLeft / timerTime;
    }
}