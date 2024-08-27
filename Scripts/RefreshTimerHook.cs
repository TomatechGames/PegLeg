using Godot;
using System;

public partial class RefreshTimerHook : Label
{
    [Export(PropertyHint.Enum, "Hour, Day, Week, Event")]
    int timerType;

    public override void _Ready()
    {
        UpdateRefreshTime();
        criticalCountdownTime = timerType switch
        {
            0 => 1,         // last minute of hour
            1 => 5,         // last 5 minutes of day
            2 => 60,        // last hour of week
            3 => 60 * 24,   // last day of event
            _ => 5,
        };
        warningCountdownTime = timerType switch
        {
            0 => 10,            // last 10 minutes of hour
            1 => 60,            // last hour of day
            2 => 60 * 24,       // last 24 hours of week
            3 => 60 * 24 * 7,   // last week of event
            _ => 60,
        };
        RefreshTimerController.OnSecondChanged += UpdateTimeText;
        UpdateTimeText();

        VisibilityChanged += () =>
        {
            if (IsVisibleInTree())
                UpdateTimeText();
        };
    }

    DateTime refreshTime;
    int criticalCountdownTime = 1;
    int warningCountdownTime = 60;
    void UpdateRefreshTime()
    {
        refreshTime = RefreshTimerController.GetRefreshTime(timerType switch
        {
            0 => RefreshTimeType.Hourly,
            1 => RefreshTimeType.Daily,
            2 => RefreshTimeType.Weekly,
            3 => RefreshTimeType.Event,
            _ => RefreshTimeType.Daily,
        });
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
        Text = remainingTime.FormatTime();
        if (DateTime.UtcNow.CompareTo(refreshTime) >= 0)
            UpdateRefreshTime();
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnSecondChanged -= UpdateTimeText;
    }
}

