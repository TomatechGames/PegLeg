using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

public static class QuestGroupGenerator
{
    const string questGeneratorPath = "res://External/questGroupGenerators.json";
    static JsonArray generatedQuests;
    public static JsonArray GetQuestGroups()
    {
        if (generatedQuests is not null)
            return generatedQuests;

        JsonNode questGen;

        using (FileAccess questGenFile = FileAccess.Open(questGeneratorPath, FileAccess.ModeFlags.Read))
        {
            questGen = JsonNode.Parse(questGenFile.GetAsText());
        }

        generatedQuests = questGen["collections"].AsArray();
        foreach (var collection in generatedQuests)
        {
            if (collection["autoPopulateQuestlines"]?.GetValue<bool>() ?? false)
            {
                //generate questlines from banjo questline files
                BanjoAssets.TryGetSource("MainQuestLines", out var mainQuests);
                JsonObject groupGens = new();
                JsonArray mainQuestLine = new();

                GetQuestLineFromArray(mainQuests["Stonewood"].AsArray(), ref mainQuestLine);
                GetQuestLineFromArray(mainQuests["Plankerton"].AsArray(), ref mainQuestLine);
                GetQuestLineFromArray(mainQuests["Canny Valley"].AsArray(), ref mainQuestLine);

                groupGens["Campaign"] = new JsonObject() { ["questlines"] = new JsonArray() { mainQuestLine } };

                BanjoAssets.TryGetSource("EventQuestLines", out var eventQuests);
                foreach (var eventQuestLine in eventQuests)
                {
                    JsonArray eventQuestLineArray = new();
                    GetQuestLineFromArray(eventQuestLine.Value.AsArray(), ref eventQuestLineArray);
                    groupGens[eventQuestLine.Key] = new JsonObject() { ["questlines"] = new JsonArray() { eventQuestLineArray } };
                }

                collection["groupGens"] = groupGens;
                continue;
            }

            foreach (var entry in collection["groupGens"].AsObject())
            {
                var quests = GetQuestsFromObject(entry.Value);

                if (entry.Value["chain"]?.GetValue<bool>() ?? false)
                {
                    //use each quest as a starting point for generating questlines, following the rewarded quests
                    JsonArray questlines = new();
                    foreach (var quest in quests)
                    {
                        JsonArray questline = new();
                        var currentQuest = quest;
                        do
                        {
                            questline.Add(currentQuest);

                            var nextQuestId = currentQuest
                                .GetHiddenQuestRewards()
                                .Select(r => r.templateId)
                                .FirstOrDefault(i => i.StartsWith("Quest:"));
                            currentQuest = GameItemTemplate.Get(nextQuestId);
                        }
                        while (currentQuest != null);
                        questlines.Add(questline);
                    }
                    entry.Value["questlines"] = questlines;
                    continue;
                }

                entry.Value["quests"] = new JsonArray(quests.Where(q => !q.DisplayName.StartsWith("(Hidden)")).Select(q => (JsonNode)q.TemplateId).ToArray());
                //just lump all the quests into an array
            }
        }

        return generatedQuests;
    }

    static void GetQuestLineFromArray(JsonArray basis, ref JsonArray questLine)
    {
        questLine ??= new();
        foreach (var page in basis)
        {
            foreach (var quest in page.AsArray())
            {
                var splitQuest = quest.ToString().Split(':');
                questLine.Add(splitQuest[0] + ":" + splitQuest[1].ToLower());
            }
        }
    }

    static GameItemTemplate[] GetQuestsFromObject(JsonNode questSource, GameItemTemplate[] filteredQuests = null)
    {
        if(questSource is JsonValue val)
        {
            return new GameItemTemplate[] { GameItemTemplate.Get(val.ToString()) };
        }

        if (questSource["prefilter"] is JsonNode prefilteredQuests)
            filteredQuests = GetQuestsFromObject(prefilteredQuests, filteredQuests);

        filteredQuests ??= GameItemTemplate.GetTemplatesFromSource("Quest");

        if (questSource["union"] is JsonArray unionArray)
        {
            List<GameItemTemplate> combinedUnion = new();
            foreach (var item in unionArray)
            {
                combinedUnion.AddRange(GetQuestsFromObject(item, filteredQuests));
            }
            filteredQuests = combinedUnion.Distinct().ToArray();
        }

        string category = questSource["category"]?.ToString().ToLower();
        string startsWith = questSource["startsWith"]?.ToString().ToLower();
        string endsWith = questSource["endsWith"]?.ToString().ToLower();
        string contains = questSource["contains"]?.ToString().ToLower();

        if(category is not null || startsWith is not null || endsWith is not null || contains is not null)
        {
            filteredQuests = filteredQuests.Where(item =>
                    (category == null || item.Category.ToLower() == category) &&
                    item.Name.ToLower() is string name &&
                    (startsWith == null || name.StartsWith(startsWith)) &&
                    (endsWith == null || name.EndsWith(endsWith)) &&
                    (contains == null || name.Contains(contains))
                ).ToArray();
        }

        if (questSource["exclude"] is JsonNode excludeQuests)
        {
            var excludedQuestItems = GetQuestsFromObject(excludeQuests, filteredQuests);
            filteredQuests = filteredQuests.Except(excludedQuestItems).ToArray();
        }

        return filteredQuests;
    }
}
