using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public static class PLSearch
{
    const RegexOptions regexOptions = RegexOptions.IgnorePatternWhitespace;

    const string termExpression =
@"
(?>
 (?>
  (?<=\ |\A)+
  (?>[ (]|\!\()*
 )
 (?:
  (?<Prefix>\!)?
  (?>
   (?<Union>(?<!\!)\|)|
   (?<TextQuery>
    (?<TQVar>[a-zA-Z]+(?:\.(?:[a-zA-Z]+|(?:\^?\d+)))*)=
    (?<TQVal>
              (?:\.\.)?\""[\w:_.!?\/\\]*\""(?:\.\.)?|
            \[(?:\.\.)?\""[\w:_.!?\/\\]*\""(?:\.\.)?
       (?:,\ ?(?:\.\.)?\""[\w:_.!?\/\\]*\""(?:\.\.)?)*\]
    )
   )|
   (?<NumericQuery>
    (?<NQVar>[a-zA-Z]+(?:\.(?:[a-zA-Z]+|(?:\^?\d+)))*)=
    (?<NQVal>
       (?:\d+(?:\.\d+)?\.\.\d+(?:\.\d+)?)|
       (?:             \.\.\d+(?:\.\d+)?)|
       (?:                 \d+(?:\.\d+)?(?:\.\.)?)
    )
   )|
   (?<FullSearchQuery>\""[a-zA-Z]+\"")|
   (?<SearchQuery>[a-zA-Z]+)
  )
 )
 (?>[ )]+|\Z)
)|
(?<Failure>[^ \b]+)
";

    const string bracketExpression = @"^(?:[^()]|(?<Push>\()|(?<Pop-Push>\)))*(?(Push)(?!))$";

    public enum InstructionOperation
    {
        Push,
        Pop,
        Union,
        NumericQuery,
        TextQuery,
        FullSearchQuery,
        SearchQuery
    }
    public record Instruction(int index, InstructionOperation operation, JsonNode meta = null, int endIndex = -1, bool inverted = false);

    public static Instruction[] GenerateSearchInstructions(string fromText, out string failureText)
    {
        List<Instruction> instructions = new();
        failureText = "";

        var match = Regex.Match(fromText, bracketExpression, regexOptions);
        if (!match.Success)
        {
            failureText = "Bracket Pattern Failure";
            return null;
        }
        var captures = match.Groups.Values.SelectMany(g => g.Captures);
        foreach (var capture in captures)
        {
            if (capture.Index == 0)
                continue;
            bool inverted = capture.Index > 1 && fromText[capture.Index - 2] == '!';
            instructions.Add(new(capture.Index - 1, InstructionOperation.Push, endIndex: capture.Index + capture.Length + 1));
            instructions.Add(new(capture.Index + capture.Length, InstructionOperation.Pop, inverted: inverted));
        }

        string partialFailText = null;
        EvaluateRegexMatches(fromText, termExpression, match =>
        {
            var validGroups = match.Groups.Values.Where(g => g.Name != "0" && !string.IsNullOrEmpty(g.Value)).ToArray();
            var firstGroup = validGroups[0];

            bool isInverted = false;
            if (firstGroup.Name == "Prefix")
            {
                if (firstGroup.Value == "!")
                    isInverted = true;
                firstGroup = validGroups[1];
            }


            //GD.Print($"Checking \"{match.Value}\" ({firstGroup.Name}:{firstGroup.Value})");
            JsonObject meta;
            switch (firstGroup.Name)
            {
                case "TextQuery":
                    if (instructions.Any(i => i.index == firstGroup.Index))
                        return;
                    meta = new()
                    {
                        ["varPath"] = match.Groups["TQVar"].Value,
                    };
                    string checks = match.Groups["TQVal"].Value;
                    if (checks.StartsWith("["))
                        meta["checks"] = new JsonArray(checks[1..^2].Split(',').Select(c => (JsonNode)c.Trim()).ToArray());
                    else
                        meta["checks"] = new JsonArray() { checks };

                    instructions.Add(new(
                        firstGroup.Index,
                        InstructionOperation.TextQuery,
                        meta,
                        firstGroup.Index + firstGroup.Length - 1,
                        isInverted
                    ));

                    return;
                case "NumericQuery":
                    if (instructions.Any(i => i.index == firstGroup.Index))
                        return;
                    var valText = match.Groups["NQVal"].Value;
                    meta = new()
                    {
                        ["varPath"] = match.Groups["NQVar"].Value,
                    };
                    if (valText.StartsWith(".."))
                    {
                        meta["maxValue"] = JsonNode.Parse(valText[2..]);
                    }
                    else if (valText.EndsWith(".."))
                    {
                        meta["minValue"] = JsonNode.Parse(valText[..^2]);
                    }
                    else if (valText.Contains(".."))
                    {
                        var rangeText = valText.Split("..");
                        meta["minValue"] = JsonNode.Parse(rangeText[0]);
                        meta["maxValue"] = JsonNode.Parse(rangeText[1]);

                        if (float.Parse(rangeText[0]) > float.Parse(rangeText[1]))
                        {
                            partialFailText ??= "";
                            partialFailText += $"Parsing failure at line {firstGroup.Index}: \"{firstGroup.Value}\"\n";
                            return;
                        }
                    }
                    else
                    {
                        meta["minValue"] = JsonNode.Parse(valText);
                        meta["maxValue"] = JsonNode.Parse(valText);
                    }

                    instructions.Add(new(
                        firstGroup.Index,
                        InstructionOperation.NumericQuery,
                        meta,
                        firstGroup.Index + firstGroup.Length - 1,
                        isInverted
                    ));
                    return;
                case "FullSearchQuery":
                    if (instructions.Any(i => i.index == firstGroup.Index))
                        return;
                    instructions.Add(new(
                        firstGroup.Index,
                        InstructionOperation.FullSearchQuery,
                        firstGroup.Value,
                        firstGroup.Index + firstGroup.Length - 1,
                        isInverted
                    ));
                    return;
                case "SearchQuery":
                    if (instructions.Any(i => i.index == firstGroup.Index))
                        return;
                    instructions.Add(new(
                        firstGroup.Index,
                        InstructionOperation.SearchQuery,
                        firstGroup.Value,
                        firstGroup.Index + firstGroup.Length - 1,
                        isInverted
                    ));
                    return;
                case "Union":
                    instructions.Add(new(firstGroup.Index, InstructionOperation.Union));
                    return;
                case "Failure":
                    partialFailText ??= "";
                    partialFailText += $"Parsing failure at line {firstGroup.Index}: \"{firstGroup.Value}\"\n";
                    return;
                default:
                    return;
            }
        });

        if (partialFailText is not null)
        {
            failureText = partialFailText;
            return null;
        }
        return instructions.ToArray();
    }

    public static bool EvaluateInstructions(Instruction[] instructions, JsonObject target) =>
        EvaluateInstructions(instructions, target, out var _, false);

    public static bool EvaluateInstructions(Instruction[] instructions, JsonObject target, out string output, bool useOutput = true)
    {
        output = "";
        int currentDepth = 0;
        int skipUntilIndex = 0;
        bool currentState = true;
        bool isOrMode = false;
        Stack<bool> stateStack = new();
        Stack<bool> isOrStack = new();
        foreach (var item in instructions.OrderBy(i => i.index))
        {
            bool skipThisOp = item.index < skipUntilIndex;
            if (isOrMode == currentState && item.operation != InstructionOperation.Pop && item.operation != InstructionOperation.Union)
            {
                skipThisOp = true;
            }

            string toInsert = "";
            if (item.operation == InstructionOperation.Push)
            {
                toInsert += Indent(currentDepth) + "(";
                currentDepth += 1;
                if (!skipThisOp)
                {
                    stateStack.Push(currentState);
                    isOrStack.Push(isOrMode);
                    currentState = true;
                    isOrMode = false;
                }
                else
                {
                    skipUntilIndex = item.endIndex;
                }
                output += toInsert + (skipThisOp ? " (Skipped)" : "") + "\n";
                continue;
            }
            if (item.operation == InstructionOperation.Pop)
            {
                currentDepth -= 1;
                toInsert += ")";

                if (!skipThisOp)
                {
                    bool resolvedState = currentState;
                    toInsert += (item.inverted ? " NOT " : " ") + (resolvedState ? "PASS" : "FAIL");
                    if (item.inverted)
                        resolvedState = !resolvedState;
                    currentState = stateStack.Pop();
                    isOrMode = isOrStack.Pop();
                    if (isOrMode)
                        currentState |= resolvedState;
                    else
                        currentState &= resolvedState;
                }

                output += Indent(currentDepth) + toInsert + (skipThisOp ? " (Skipped)" : "") + "\n";
                continue;
            }
            isOrMode = false;

            if (!useOutput && skipThisOp)
                continue;

            toInsert += item.inverted ? "NOT " : "";

            JsonObject metaObj;
            switch (item.operation)
            {
                case InstructionOperation.Union:
                    if (!skipThisOp)
                        isOrMode = true;
                    toInsert += "OR";
                    break;
                case InstructionOperation.TextQuery:
                    {
                        if (!skipThisOp)
                            skipUntilIndex = item.endIndex;
                        metaObj = item.meta.AsObject();
                        string varPath = metaObj["varPath"].ToString();
                        if (EvaluateJsonPath(target, varPath) is not JsonNode sourceNode)
                        {
                            toInsert += "T ERROR";
                            if (!skipThisOp)
                                currentState = false;
                            break;
                        }
                        string[] checks = metaObj["checks"].AsArray().Select(n => n.ToString()).ToArray();
                        bool comparisonTrue = false;
                        if (sourceNode is JsonArray sourceArray)
                        {
                            foreach (var sourceStringVal in sourceArray)
                            {
                                if ((string)sourceStringVal is string sourceString)
                                {
                                    comparisonTrue |= CheckStrings(sourceString, checks);
                                }
                                if (comparisonTrue)
                                    break;
                            }
                        }
                        else if (sourceNode is JsonValue sourceStringVal && (string)sourceStringVal is string sourceString)
                        {
                            comparisonTrue = CheckStrings(sourceString, checks);
                        }
                        if (item.inverted)
                            comparisonTrue = !comparisonTrue;
                        if (!skipThisOp)
                            currentState = comparisonTrue;
                        toInsert += comparisonTrue ? "PASS" : "FAIL";
                    }
                    break;
                case InstructionOperation.NumericQuery:
                    {
                        if (!skipThisOp)
                            skipUntilIndex = item.endIndex;
                        metaObj = item.meta.AsObject();
                        string varPath = metaObj["varPath"].ToString();
                        var endNode = EvaluateJsonPath(target, varPath);
                        if (ExtractJsonNumber(endNode) is not float sourceVar)
                        {
                            toInsert += "N ERROR";
                            if (!skipThisOp)
                                currentState = false;
                            break;
                        }
                        bool comparisonTrue = true;
                        if (ExtractJsonNumber(metaObj["minValue"]) is float minValue)
                        {
                            comparisonTrue &= sourceVar >= minValue;
                        }
                        if (ExtractJsonNumber(metaObj["maxValue"]) is float maxValue)
                        {
                            comparisonTrue &= sourceVar <= maxValue;
                        }
                        if (item.inverted)
                            comparisonTrue = !comparisonTrue;
                        if (!skipThisOp)
                            currentState = comparisonTrue;
                        toInsert += comparisonTrue ? "PASS" : "FAIL";
                    }
                    break;
                case InstructionOperation.FullSearchQuery:
                    {
                        if (!skipThisOp)
                            skipUntilIndex = item.endIndex;
                        bool comparisonTrue = false;
                        if (target["tags"] is JsonArray tagArray)
                        {
                            string checkString = item.meta.ToString()[1..^1];
                            comparisonTrue = tagArray.Any(t => t.ToString() == checkString);
                        }
                        if (!skipThisOp)
                            currentState = comparisonTrue;
                        toInsert += comparisonTrue ? "PASS" : "FAIL";
                    }
                    break;
                case InstructionOperation.SearchQuery:
                    {
                        if (!skipThisOp)
                            skipUntilIndex = item.endIndex;
                        bool comparisonTrue = false;
                        if (target["tags"] is JsonArray tagArray)
                        {
                            string checkString = item.meta.ToString();
                            comparisonTrue = tagArray.Any(t => t.ToString().Contains(checkString));
                        }
                        if (!skipThisOp)
                            currentState = comparisonTrue;
                        toInsert += comparisonTrue ? "PASS" : "FAIL";
                    }
                    break;
            }
            output += Indent(currentDepth) + toInsert + (skipThisOp ? " (Skipped)" : "") + "\n";
        }
        output += "final result: " + currentState;
        return currentState;
    }
    static string Indent(int level)
    {
        string indentText = "";
        for (int i = 0; i < level; i++)
            indentText += "   ";
        return indentText;
    }

    static float? ExtractJsonNumber(JsonNode possibleVal)
    {
        if (possibleVal is not JsonValue numberValue)
            return null;
        if (numberValue.TryGetValue(out float floatValue))
            return floatValue;
        if (numberValue.TryGetValue(out int intValue))
            return intValue;
        return null;
    }

    static JsonNode EvaluateJsonPath(JsonNode parent, string path) =>
        EvaluateJsonPath(parent, new Queue<string>(path.Split(".")));
    static JsonNode EvaluateJsonPath(JsonNode parent, Queue<string> pathQueue)
    {
        if (pathQueue.Count == 0)
            return parent;
        string nextKey = pathQueue.Dequeue();
        if (parent is JsonObject)
        {
            if (parent[nextKey] is JsonNode nextNode)
                return EvaluateJsonPath(nextNode, pathQueue);
        }
        else if (parent is JsonArray parentArray)
        {
            bool fromEnd = nextKey.StartsWith("^");
            if (fromEnd)
                nextKey = nextKey[1..];
            if (int.TryParse(nextKey, out int nextIndex))
            {
                if (fromEnd)
                {
                    if (nextIndex > 0 && parentArray.Count >= nextIndex && parentArray[^nextIndex] is JsonNode nextNode)
                        EvaluateJsonPath(nextNode, pathQueue);
                }
                else if (nextIndex >= 0 && parentArray.Count > nextIndex && parentArray[nextIndex] is JsonNode nextNode)
                    EvaluateJsonPath(nextNode, pathQueue);
            }
        }
        return null;
    }

    static bool CheckStrings(string sourceString, string[] checks)
    {
        foreach (var check in checks)
        {
            bool starts = check.StartsWith("..");
            bool ends = check.EndsWith("..");
            if (starts && ends)
            {
                if (sourceString.Contains(check[3..^3]))
                    return true;
            }
            else if (starts)
            {
                if (sourceString.EndsWith(check[3..^1]))
                    return true;
            }
            else if (ends)
            {
                if (sourceString.StartsWith(check[1..^3]))
                    return true;
            }
            else if (sourceString == check[1..^1])
                return true;
        }
        return false;
    }

    static void EvaluateRegexMatches(string sourceText, string expression, Action<Match> doPerMatch)
    {
        var matches = Regex.Matches(sourceText, expression, regexOptions);
        foreach (var item in matches.Cast<Match>())
        {
            doPerMatch?.Invoke(item);
        }
    }
}
