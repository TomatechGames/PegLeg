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
                        string nextQuest = quest;
                        do
                        {
                            questline.Add(nextQuest);
                            if (GameItemTemplate.Get(nextQuest) is GameItemTemplate questTemplate)
                            {
                                nextQuest = questTemplate
                                    .GetHiddenQuestRewards()
                                    .Select(r => r.templateId)
                                    .FirstOrDefault(i => i.StartsWith("Quest:"));
                            }
                            else
                                nextQuest = null;
                        }
                        while (nextQuest != null);
                        questlines.Add(questline);
                    }
                    entry.Value["questlines"] = questlines;
                    continue;
                }

                entry.Value["quests"] = new JsonArray(quests.Where(q => !GameItemTemplate.Get(q).DisplayName.StartsWith("(Hidden)")).Select(q=>(JsonNode)q).ToArray());
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

    static string[] GetQuestsFromObject(JsonNode questSource)
    {
        if(questSource is JsonValue val)
        {
            return new string[] { val.ToString() };
        }

        string[] excludedQuests = Array.Empty<string>();
        if (questSource["exclude"] is JsonNode excludeQuests)
        {
            excludedQuests = GetQuestsFromObject(excludeQuests);
        }

        if (questSource["union"] is JsonArray unionArray)
        {
            List<string> combinedUnion = new();
            foreach (var item in unionArray)
            {
                combinedUnion.AddRange(GetQuestsFromObject(item));
            }
            return combinedUnion.Except(excludedQuests).ToArray();
        }

        if (questSource["startsWith"] is JsonValue startsWith)
        {
            string startsWithVal = startsWith.ToString();
            return GameItemTemplate.GetTemplatesFromSource("Quest", item => item.Name.ToLower().StartsWith(startsWithVal))
                    .Select(item=>item.TemplateId)
                    .Except(excludedQuests)
                    .ToArray();
        }

        if (questSource["endsWith"] is JsonValue endsWith)
        {
            string endsWithVal = endsWith.ToString();
            return GameItemTemplate.GetTemplatesFromSource("Quest", item => item.Name.ToLower().EndsWith(endsWithVal))
                    .Select(item => item.TemplateId)
                    .Except(excludedQuests)
                    .ToArray();
        }

        if (questSource["category"] is JsonValue category)
        {
            string categoryVal = category.ToString();
            return GameItemTemplate.GetTemplatesFromSource("Quest", item => item.Category.ToLower() == categoryVal.ToLower())
                    .Select(item => item.TemplateId)
                    .Except(excludedQuests)
                    .ToArray();
        }

        return Array.Empty<string>();
    }
}
