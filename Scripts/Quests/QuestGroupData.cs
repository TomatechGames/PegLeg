using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

public struct QuestGroupCollectionData
{
    [JsonRequired]
    [JsonInclude]
    public string displayName { get; private set; } = null;
    [JsonInclude]
    public int priority { get; private set; } = 0;
    [JsonInclude]
    public string[] eventFlags { get; private set; } = [];
    [JsonInclude]
    string content { get; set; } = null;
    [JsonInclude]
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
                    .SelectMany(kvp=>kvp.Value.Reverse())
                    .SelectMany(arr=>arr);
                var eventQuestLines = PegLegResourceManager.LoadResourceDict<EventQuestLine>("GameAssets/EventQuestLines.json");
                //generate groups from questlines
                return questGroups =
                [
                    new("Campaign", mainQuestLines.Select(GameItemTemplate.Get).ToArray()),
                    ..eventQuestLines.Select(kvp=>new QuestGroupData(kvp.Key, kvp.Value.Quests.Select(GameItemTemplate.Get).ToArray(), kvp.Value.EventFlag))
                ];
            }
            if (PegLegResourceManager.ResourceExists(content))
            {
                //load from content file
                return questGroups = [.. PegLegResourceManager.LoadResourceDict<QuestGroupData>($"QuestGroups/{content}").Values];
            }
            return questGroups = [];
        }
    }

    public QuestGroupCollectionData() { }

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
    [JsonInclude]
    public string displayName { get; private set; } = null;
    [JsonInclude]
    bool auto { get; set; } = false;
    [JsonInclude]
    public bool chain { get; private set; } = false;
    [JsonInclude]
    bool sequence { get; set; } = false;
    [JsonIgnore]
    public readonly bool Sequence => sequence || chain;
    [JsonInclude]
    bool? showLocked { get; set; } = null;
    [JsonIgnore]
    public readonly bool ShowLocked => showLocked ?? Sequence;
    [JsonInclude]
    bool? showComplete { get; set; } = null;
    [JsonIgnore]
    public readonly bool ShowComplete => showComplete ?? Sequence;
    [JsonInclude]
    [JsonConverter(typeof(JsonStringEnumConverter<TimerMode>))]
    public TimerMode timer { get; private set; } = TimerMode.Default;
    [JsonInclude]
    public string eventFlag { get; private set; } = null;
    [JsonRequired]
    [JsonInclude]
    JsonElement? predicate { get; set; }
    GameItemTemplate[][] questlines;

    [JsonIgnore]
    public GameItemTemplate[] Quests => Questlines[0];

    [JsonIgnore]
    public GameItemTemplate[][] Questlines
    {
        get
        {
            if (questlines is not null)
                return questlines;
            if (predicate is null)
                return questlines = [[]];

            var rootQuests = ComplexQuestPredicate.Evaluate(predicate.Value);

            if (chain)
            {
                List<GameItemTemplate[]> qlines = [];
                foreach (var quest in rootQuests)
                {
                    List<GameItemTemplate> qline = [];
                    var currentQuest = quest;
                    do
                    {
                        qline.Add(currentQuest);
                        currentQuest = currentQuest
                            .GetHiddenQuestRewards()?
                            .FirstOrDefault(r => r.templateId.StartsWith("Quest:"))?
                            .template;
                    }
                    while (currentQuest is not null);
                    qlines.Add([.. qline]);
                }
                questlines = [.. qlines];
            }

            questlines ??= [rootQuests];
            predicate = null;
            return questlines;
        }
    }

    public QuestGroupData() { }
    public QuestGroupData(string displayName, GameItemTemplate[] quests, string eventFlag = null)
    {
        //ctor for exported questlines
        this.displayName = displayName;
        this.eventFlag = eventFlag;
        questlines = [quests];
        sequence = true;
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

        [JsonInclude]
        JsonElement? prefilter { get; set; }
        [JsonInclude]
        JsonElement? union { get; set; }

        [JsonInclude]
        string category { get; set; }
        [JsonInclude]
        string startsWith { get; set; }
        [JsonInclude]
        string endsWith { get; set; }
        [JsonInclude]
        string contains { get; set; }

        [JsonInclude]
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
