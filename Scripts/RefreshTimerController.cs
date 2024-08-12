using Godot;
using System;
using System.Linq;

public partial class RefreshTimerController : Node
{
    public static event Action OnSecondChanged;
    public static event Action OnHourChanged;
    public static event Action OnDayChanged;

    public override void _Ready()
    {
        Timer perSecondTimer = new()
        {
            OneShot = false,
            WaitTime = 1,
            Autostart = true
        };
        AddChild(perSecondTimer);
        perSecondTimer.Timeout += UpdateTimers;
        lastTime = DateTime.UtcNow;
    }
    DateTime lastTime;
    private void UpdateTimers()
    {
        OnSecondChanged?.Invoke();
        var currentTime = DateTime.UtcNow;
        if (currentTime.Hour != lastTime.Hour)
            OnHourChanged?.Invoke();
        if (currentTime.Day != lastTime.Day)
            OnDayChanged?.Invoke();
        lastTime = currentTime;
    }

    static readonly DateTime referenceStartDate = new(2024, 1, 22);
    static readonly int[] seasonLengths = new int[]
    {
        10,
        11,
        11,
        11,
        9
    };
    static readonly int weeksInSeasonalYear = seasonLengths.Sum();

    public static DateTime GetRefreshTime(RefreshTimeType refreshType)
    {
        switch (refreshType)
        {
            case RefreshTimeType.Hourly:
                return DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour + 1);
            case RefreshTimeType.Daily:
                return DateTime.UtcNow.Date.AddDays(1);
            case RefreshTimeType.Weekly:
                int utcDayOfWeek = (int)DateTime.UtcNow.DayOfWeek;
                int daysUntilThursday = ((10 - utcDayOfWeek)) % 7;
                return DateTime.UtcNow.Date.AddDays(daysUntilThursday + 1);
        }
        int dayCount = (DateTime.UtcNow.Date - referenceStartDate).Days;
        GD.Print("DC: " + dayCount);
        dayCount = dayCount % (weeksInSeasonalYear * 7);
        GD.Print("DC2: " + dayCount);
        int daysRemaining = 0;
        for (int i = 0; i < seasonLengths.Length; i++)
        {
            if (dayCount < seasonLengths[i]*7)
            {
                GD.Print("S: " + i);
                daysRemaining = (seasonLengths[i] * 7) - dayCount;
                break;
            }
            dayCount -= seasonLengths[i]*7;
        }
        GD.Print("R: " + daysRemaining);
        var result = DateTime.UtcNow.Date.AddDays(daysRemaining + 1);
        GD.Print("D: " + result);
        return result;
    }
}
public enum RefreshTimeType
{
    Hourly,
    Daily,
    Weekly,
    Event
}
