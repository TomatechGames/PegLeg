using Godot;
using System;
using System.Linq;

public partial class RefreshTimerController : Node
{
    public static event Action OnSecondChanged;
    public static event Action OnHourChanged;
    public static event Action OnDayChanged;
    [Export]
    int daysToAddDebug = 0;
    [Export]
    int monthsToAddDebug = 0;
    [Export]
    int yearsToAddDebug = 0;

    static RefreshTimerController instance;

    public override void _Ready()
    {
        instance = this;
        Timer perSecondTimer = new()
        {
            OneShot = false,
            WaitTime = 1,
            Autostart = true
        };
        AddChild(perSecondTimer);
        perSecondTimer.Timeout += UpdateTimers;
        lastTime = DateTime.UtcNow;
        AppConfig.OnConfigChanged += OnConfigChanged;
        offset = AppConfig.Get("advanced", "offset", false) ? 0 : 5;
    }

    float offset = 5;
    private void OnConfigChanged(string arg1, string arg2, System.Text.Json.Nodes.JsonValue arg3)
    {
        offset = AppConfig.Get("advanced", "offset", false) ? 0 : 5;
    }

    DateTime lastTime;
    private void UpdateTimers()
    {
        OnSecondChanged?.Invoke();
        var currentTime = DateTime.UtcNow.AddSeconds(-offset);
        if (currentTime.Hour != lastTime.Hour)
            OnHourChanged?.Invoke();
        if (currentTime.Day != lastTime.Day)
            OnDayChanged?.Invoke();
        lastTime = currentTime;
    }

    static readonly DateTime referenceStartDate = new(2024, 1, 25);
    static readonly int[] seasonLengths = new int[]
    {
        10,
        11,
        11,
        11,
        9
    };
    static readonly int weeksInSeasonalYear = seasonLengths.Sum();

    public static DateTime RightNow => 
        instance is null ? 
            DateTime.UtcNow : 
            DateTime.UtcNow
                .AddDays(instance.daysToAddDebug)
                .AddMonths(instance.monthsToAddDebug)
                .AddYears(instance.yearsToAddDebug);

    public static DateTime GetRefreshTime(RefreshTimeType refreshType)
    {
        var rightNow = RightNow;
        var today = rightNow.Date;
        switch (refreshType)
        {
            case RefreshTimeType.Hourly:
                return today.AddHours(rightNow.Hour + 1);
            case RefreshTimeType.Daily:
                return today.AddDays(1);
            case RefreshTimeType.Weekly:
                int utcDayOfWeek = (int)rightNow.DayOfWeek;
                int daysUntilThursday = ((10 - utcDayOfWeek)) % 7;
                return today.AddDays(daysUntilThursday + 1);
            case RefreshTimeType.BRWeekly:
                int utcDayOfWeekBR = (int)rightNow.AddHours(14).DayOfWeek;
                int daysUntilTuesday = ((10 - utcDayOfWeekBR)) % 7;
                return today.AddDays(daysUntilTuesday + 1).AddHours(14);
        }
        int dayCount = (today - referenceStartDate).Days;
        dayCount = dayCount % (weeksInSeasonalYear * 7);
        int daysRemaining = 0;
        int startDayOffset = 0;
        for (int i = 0; i < seasonLengths.Length; i++)
        {
            int reducedDayCount = dayCount - startDayOffset;
            if (reducedDayCount < seasonLengths[i]*7)
            {
                daysRemaining = (seasonLengths[i] * 7) - reducedDayCount;
                break;
            }
            startDayOffset += seasonLengths[i] * 7;
        }
        var result = today.AddDays(daysRemaining);
        return result;
    }
}
public enum RefreshTimeType
{
    Hourly,
    Daily,
    Weekly,
    BRWeekly,
    Event
}
