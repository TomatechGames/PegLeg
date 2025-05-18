using Godot;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Net.Http;
using System.Security.Principal;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class CalenderRequests
{
    const string calenderCachePath = "user://calender.json";
    static bool hasCalender = false;
    static Calender currentCalender;

    public static event Action OnCalenderUpdate;

    struct Calender
    {
        public DateTime cacheExpire { get; set; }
        public EventState[] states { get; set; }
        public int latestCalenderIndex { get; set; }

        Dictionary<string, EventTimeRange> knownEvents;

        [JsonIgnore]
        public Dictionary<string, EventTimeRange> KnownEvents => knownEvents ??=
            states.Length > 1 ?
            states.Last().ActiveEvents.Union(states.First().ActiveEvents).ToDictionary() :
            states.First().ActiveEvents;

        public int GetCurrentIndex(bool update = false)
        {
            //assumes that states are in chronological order
            int cur = 0;
            for (int i = states.Length - 1; i >= 1; i--)
            {
                if (states[i].validFrom < DateTime.UtcNow)
                {
                    cur = i;
                    break;
                }
            }
            if(update)
                latestCalenderIndex = cur;
            return cur;
        }

        public EventState GetLatestState() => states?[latestCalenderIndex] ?? default;
        public EventState GetCurrentState(bool update = false) => states?[GetCurrentIndex(update)] ?? default;
    }

    struct EventState
    {
        public DateTime validFrom { get; set; }
        public Dictionary<string, EventTimeRange> activeEvents { get; set; }

        [JsonIgnore]
        public Dictionary<string, EventTimeRange> ActiveEvents => activeEvents ??= [];

        public bool Equals(EventState other)
        {
            if (activeEvents is null || other.activeEvents is null)
                return activeEvents is null== other.activeEvents is null;

            if (activeEvents?.Keys != other.activeEvents?.Keys)
                return false;

            if (activeEvents.Any(kvp => !other.activeEvents[kvp.Key].Equals(kvp.Value)))
                return false;

            return true;
        }
    }

    struct EventTimeRange
    {
        public DateTime activeSince { get; set; }
        public DateTime activeUntil { get; set; }
        [JsonIgnore]
        public readonly TimeSpan Duration => activeUntil - activeSince;

        public readonly bool Equals(EventTimeRange other) =>
            activeSince == other.activeSince && 
            activeUntil == other.activeUntil;
    }

    public static async Task CheckCalender(this GameAccount account)
    {
        bool? notify = null;

        if (!hasCalender && FileAccess.FileExists(calenderCachePath))
        {
            using FileAccess calenderFile = FileAccess.Open(calenderCachePath, FileAccess.ModeFlags.Read);
            currentCalender = JsonSerializer.Deserialize<Calender>(calenderFile.GetAsText());
            notify = true;
            hasCalender = true;
        }

        if (currentCalender.cacheExpire < DateTime.UtcNow)
            notify ??= await RequestCalender(account);

        notify ??= currentCalender.latestCalenderIndex != currentCalender.GetCurrentIndex(true);

        if (notify == true)
        {
            GD.Print("Calender Update!");
            OnCalenderUpdate?.Invoke();
        }
    }

    static async Task<bool?> RequestCalender(GameAccount account)
    {
        var calResponse = await Helpers.MakeRequest(
            HttpMethod.Get,
            FnWebAddresses.game,
            "/fortnite/api/calendar/v1/timeline",
            "",
            account.AuthHeader,
            ""
        );
        var newData = calResponse?.AsObject();

        if (!newData.ContainsKey("channels"))
            return null;

        var clientEvents = newData["channels"]["client-events"];
        var oldState = currentCalender.GetLatestState();
        currentCalender = new()
        {
            cacheExpire = clientEvents["cacheExpire"].AsTime(),
            states = [..clientEvents["states"].AsArray().Select(s => new EventState
            {
                validFrom = s["validFrom"].AsTime(),
                activeEvents = s["activeEvents"].AsArray().ToDictionary(
                    e => e["eventType"].ToString(),
                    e => new EventTimeRange
                    {
                        activeSince = e["activeSince"].AsTime(),
                        activeUntil = e["activeUntil"].AsTime()
                    }
                )
            })]
        };

        using FileAccess calenderFile = FileAccess.Open(calenderCachePath, FileAccess.ModeFlags.Write);
        calenderFile.StoreString(JsonSerializer.Serialize(currentCalender));

        var newState = currentCalender.GetCurrentState(true);
        return !oldState.Equals(newState) ? true : null;
    }

    public static bool HasCalender => hasCalender;

    public static bool EventFlagActive(string flag) => 
        hasCalender && currentCalender.GetLatestState().ActiveEvents.ContainsKey(flag);

    public static DateTime EventStart(string flag) =>
        currentCalender.KnownEvents.TryGetValue(flag, out var time) ? time.activeSince : default; //todo: estimate times of missing events

    public static DateTime EventEnd(string flag) =>
        currentCalender.KnownEvents.TryGetValue(flag, out var time) ? time.activeUntil : default; //todo: estimate times of missing events

    public static int BRSeasonNumber => 1;
    public static string BRSeasonEventFlag => $"EventFlag.Event_S{BRSeasonNumber}_UISeasonEnd";
}
