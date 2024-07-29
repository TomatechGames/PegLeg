using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

static class CalenderRequests
{
    static JsonObject calenderCache;

    static Task<JsonObject> activeCalenderRequest = null;
    static async Task EnsureCalender(bool forceRefresh)
    {
        if (activeCalenderRequest is not null && activeCalenderRequest.IsCompleted)
            activeCalenderRequest = null;

        if (forceRefresh)
        {
            GD.Print("forcing refresh");
            calenderCache = null;
        }

        //TODO: save calender cache between sessions

        if (calenderCache is not null)
        {
            var refreshTime = DateTime.Parse(calenderCache["expiration"].ToString(), null, DateTimeStyles.RoundtripKind);
            if (DateTime.UtcNow.CompareTo(refreshTime) >= 0)
                calenderCache = null;
        }

        if (calenderCache is null)
        {
            GD.Print("requesting calender");
            activeCalenderRequest ??= RequestCalender();
            await Task.WhenAny(activeCalenderRequest);
        }
    }

    static async Task<JsonObject> RequestCalender()
    {
        if (!await LoginRequests.TryLogin())
            return null;

        GD.Print("retrieving calender from epic...");
        JsonNode calender = await Helpers.MakeRequest(
                HttpMethod.Get,
                FNEndpoints.serviceEndpoint,
                "fortnite/api/calendar/v1/timeline",
                "",
                LoginRequests.AccountAuthHeader
            );
        calenderCache = calender["channels"]["client-events"]["states"][0].AsObject();
        calenderCache["expiration"] = calender["channels"]["client-events"]["cacheExpire"].ToString();
        //GD.Print(calenderCache.ToString());
        return calenderCache;
    }

    public static Task<DateTime> DailyShopRefreshTime()
    {
        //for some reason the calender is giving the wrong times
        //await EnsureCalender(false);
        //return DateTime.Parse(calenderCache["state"]["dailyStoreEnd"].ToString(), null, DateTimeStyles.RoundtripKind);
        return Task.FromResult(DateTime.UtcNow.Date.AddDays(1));
    }

    public static Task<DateTime> WeeklyShopRefreshTime()
    {
        //for some reason the calender is giving the wrong times
        //await EnsureCalender(false);
        //return DateTime.Parse(calenderCache["state"]["stwWeeklyStoreEnd"].ToString(), null, DateTimeStyles.RoundtripKind);
        int utcDayOfWeek = (int)DateTime.UtcNow.DayOfWeek;
        int daysUntilThursday = ((10- utcDayOfWeek)) % 7;
        return Task.FromResult(DateTime.UtcNow.Date.AddDays(daysUntilThursday+1));
    }

    public static async Task<DateTime> EventShopRefreshTime()
    {
        await EnsureCalender(false);
        if (calenderCache?["state"]?["stwEventStoreEnd"] is null)
            await GenericConfirmationWindow.ShowErrorForWebResult(calenderCache);
        return DateTime.Parse(calenderCache["state"]["stwEventStoreEnd"].ToString(), null, DateTimeStyles.RoundtripKind);
    }

    public static async Task<EventTimeRange> EventTimeSpan(string eventType)
    {
        await EnsureCalender(false);
        var matchedEvent = calenderCache["activeEvents"].AsArray().FirstOrDefault(val => val["eventType"].ToString() == eventType);
        var from = DateTime.Parse(matchedEvent["activeSince"].ToString(), null, DateTimeStyles.RoundtripKind);
        var to = DateTime.Parse(matchedEvent["activeUntil"].ToString(), null, DateTimeStyles.RoundtripKind);
        return new EventTimeRange(from, to);
    }

    public struct EventTimeRange
    {
        public readonly DateTime start;
        public readonly DateTime end;
        public EventTimeRange(DateTime start, DateTime end)
        {
            this.start = start;
            this.end = end;
        }
    }
}
