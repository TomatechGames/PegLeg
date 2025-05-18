using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public struct QuestGroupCollection
{
    [JsonRequired]
    public string displayName { get; private set; } = null;
    public string[] eventFlags { get; private set; } = [];
    string content { get; set; } = null;
    bool autoPopulateQuestlines { get; set; } = false;
    QuestGroupData[] questGroups;
    [JsonIgnore]
    public QuestGroupData[] QuestGroups
    {
        get
        {
            if(questGroups is not null)
                return questGroups;
            if (autoPopulateQuestlines)
            {
                var mainQuestLines = PegLegResourceManager.LoadResourceDict<string[][]>("GameAssets/MainQuestLines.json")
                    .SelectMany(kvp=>kvp.Value)
                    .SelectMany(arr=>arr);
                var eventQuestLines = PegLegResourceManager.LoadResourceDict<EventQuestLine>("GameAssets/EventQuestLines.json");
                questGroups =
                [
                    new("Campaign", mainQuestLines.Select(GameItemTemplate.Get).ToArray()),
                    ..eventQuestLines.Select(kvp=>new QuestGroupData(kvp.Key, kvp.Value.Quests.Select(GameItemTemplate.Get).ToArray(), kvp.Value.EventFlag))
                ];
                //generate groups from questlines
            }
            if (PegLegResourceManager.ResourceExists(content))
            {
                //load from content file
                return questGroups = PegLegResourceManager.LoadResourceArray<QuestGroupData>(content);
            }
            return questGroups = [];
        }
    }

    public QuestGroupCollection() { }

    struct EventQuestLine
    {
        public string EventFlag { get; set; }
        public string[][] QuestPages { get; set; }
        [JsonIgnore]
        public IEnumerable<string> Quests => QuestPages.SelectMany(arr => arr);
    }
}

public struct QuestGroupData
{
    [JsonRequired]
    public string displayName { get; private set; } = null;
    bool auto { get; set; } = false;
    public bool chain { get; private set; } = false;
    bool sequence { get; set; } = false;
    [JsonIgnore]
    public readonly bool Sequence => sequence || chain;
    bool? showLocked { get; set; } = null;
    [JsonIgnore]
    public readonly bool ShowLocked => showLocked ?? Sequence;
    bool? showComplete { get; set; } = null;
    [JsonIgnore]
    public readonly bool ShowComplete => showComplete ?? Sequence;
    public TimerMode timer { get; private set; } = TimerMode.Default;
    public string eventFlag { get; private set; } = null;
    [JsonRequired]
    JsonElement? predicate { get; set; }
    [JsonIgnore]
    GameItemTemplate[] quests;

    public GameItemTemplate[] Quests
    {
        get
        {
            if (quests is not null)
                return quests;
            if (predicate is null)
                return quests = [];
            quests = ComplexQuestPredicate.Evaluate(predicate.Value);
            predicate = null;
            return quests;
        }
    }

    public QuestGroupData() { }
    public QuestGroupData(string displayName, GameItemTemplate[] quests, string eventFlag = null)
    {
        //ctor for exported questlines
        this.displayName = displayName;
        this.quests = quests;
        this.eventFlag = eventFlag;
    }

    public enum TimerMode
    {
        Default,
        Daily,
        Weekly,
    }
    struct ComplexQuestPredicate
    {
        static GameItemTemplate[] allQuests;

        JsonElement? prefilter { get; set; }
        JsonElement? union { get; set; }

        string category { get; set; }
        string startsWith { get; set; }
        string endsWith { get; set; }
        string contains { get; set; }

        JsonElement? exclude { get; set; }

        public GameItemTemplate[] EvaluateComplex(GameItemTemplate[] filteredQuests = null)
        {
            if (prefilter is not null)
                filteredQuests = Evaluate(prefilter.Value, filteredQuests);

            filteredQuests ??= (allQuests ??= GameItemTemplate.GetTemplatesOfType("Quest"));

            if (union is not null)
                filteredQuests = Evaluate(union.Value, filteredQuests).Distinct().ToArray();

            filteredQuests = filteredQuests.Where(FilterQuest).ToArray();

            if (exclude is not null)
            {
                var excludedQuestItems = Evaluate(exclude.Value, filteredQuests);
                filteredQuests = filteredQuests.Except(excludedQuestItems).ToArray();
            }

            return filteredQuests;
        }

        bool FilterQuest(GameItemTemplate item) =>
            item is not null &&
            (category == null || item.Category?.ToLower() == category) &&
            item.Name?.ToLower() is string name &&
            (startsWith == null || name.StartsWith(startsWith)) &&
            (endsWith == null || name.EndsWith(endsWith)) &&
            (contains == null || name.Contains(contains));

        public static GameItemTemplate[] Evaluate(JsonElement predicate, GameItemTemplate[] fromQuests = null) =>
            predicate.ValueKind switch
            {
                JsonValueKind.String => GameItemTemplate.Get(predicate.ToString()) is GameItemTemplate singleTemplate ? [singleTemplate] : [],
                JsonValueKind.Array => predicate.EnumerateArray().SelectMany(e => Evaluate(e, fromQuests)).ToArray(),
                JsonValueKind.Object => predicate.Deserialize<ComplexQuestPredicate>().EvaluateComplex(fromQuests),
                _ => []
            };
    }
}
