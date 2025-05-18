using Godot;
using System;

public partial class RefreshTimerHook : Label
{
    [Export(PropertyHint.Enum, "Hour, Day, Week, BR Week, Event, Custom")]
    int timerType;
    [Export(PropertyHint.Enum, "Timer, SigLong, SigShort")]
    int formatType;
    [Export]
    int customWarningTime;
    [Export]
    int customCritTime;

    public override void _Ready()
    {
        TooltipText = "";
        Text = "";
        UpdateRefreshTime();
        criticalCountdownTime = timerType switch
        {
            0 => 1,         // last minute of hour
            1 => 5,         // last 5 minutes of day
            2 => 60,        // last hour of week
            3 => 60,        // last hour of week
            4 => 60 * 24,   // last day of event
            5 => customCritTime,
            _ => 5,
        };
        warningCountdownTime = timerType switch
        {
            0 => 10,            // last 10 minutes of hour
            1 => 60,            // last hour of day
            2 => 60 * 24,       // last 24 hours of week
            3 => 60 * 24,       // last 24 hours of week
            4 => 60 * 24 * 7,   // last week of event
            5 => customWarningTime,
            _ => 60,
        };
        RefreshTimerController.OnSecondChanged += UpdateTimeText;
        UpdateTimeText();

        VisibilityChanged += () =>
        {
            if (IsVisibleInTree())
                UpdateTimeText();
        };
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void SetCustomRefreshTime(DateTime customRefreshTime)
    {
        timerType = 5;
        refreshTime = customRefreshTime;
        TooltipText = refreshTime.ToString("d");
        warningCountdownTime = customWarningTime;
        criticalCountdownTime = customCritTime;
        UpdateTimeText();
    }

    DateTime refreshTime;
    int criticalCountdownTime = 1;
    int warningCountdownTime = 60;
    void UpdateRefreshTime()
    {
        if (timerType == 5)
            return;
        refreshTime = RefreshTimerController.GetRefreshTime(timerType switch
        {
            0 => RefreshTimeType.Hourly,
            1 => RefreshTimeType.Daily,
            2 => RefreshTimeType.Weekly,
            3 => RefreshTimeType.BRWeekly,
            4 => RefreshTimeType.Event,
            _ => RefreshTimeType.Daily,
        });
        TooltipText = refreshTime.ToString("d");
    }

    void UpdateTimeText()
    {
        if (!IsVisibleInTree())
            return;
        var remainingTime = (refreshTime - RefreshTimerController.RightNow);
        if (remainingTime.TotalMinutes < criticalCountdownTime)
            SelfModulate = Colors.Red;
        else if (remainingTime.TotalMinutes < warningCountdownTime)
            SelfModulate = Colors.Orange;
        else
            SelfModulate = Colors.White;
        Text = remainingTime.FormatTime(formatType switch
        {
            2 => Helpers.TimeFormat.SigLong,
            1 => Helpers.TimeFormat.SigShort,
            _ => Helpers.TimeFormat.Full,
        });
        if (DateTime.UtcNow.CompareTo(refreshTime) >= 0)
            UpdateRefreshTime();
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnSecondChanged -= UpdateTimeText;
    }
}

