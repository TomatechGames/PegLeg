using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using System.Threading;
using System.Linq.Expressions;
using System.Text.Json;
using static Godot.HttpRequest;

static class Helpers
{
    public static void Unpress(this ButtonGroup group)
    {
        bool allowUnpress = group.AllowUnpress;
        group.AllowUnpress = true;
        if (group.GetPressedButton() is BaseButton button)
            button.ButtonPressed = false;
        group.AllowUnpress = allowUnpress;
    }

    public static void SetVisibleIfHasContent(this Label label)
    {
        label.Visible = !string.IsNullOrWhiteSpace(label.Text);
    }

    public static T Reserialise<T>(this T toReserialise) where T : JsonNode =>
        toReserialise is not null ? (T)JsonNode.Parse(toReserialise.ToJsonString()) : null;

    public static string ProperlyGlobalisePath(string godotPath)
    {
        if (godotPath.StartsWith("res://") && OS.HasFeature("template"))
            return OS.GetExecutablePath().Split("/")[..^1].Join("/") + "/" + ProjectSettings.GlobalizePath(godotPath);
        else
            return ProjectSettings.GlobalizePath(godotPath);
    }

    public static CancellationTokenSource Reset(this CancellationTokenSource original)
    {
        original?.Cancel();
        return new();
    }
    public static CancellationTokenSource Regenerate(this CancellationTokenSource original, out CancellationToken ct)
    {
        original?.Cancel();
        var newSource = new CancellationTokenSource();
        ct = newSource.Token;
        return newSource;
    }

    static SceneTree MainLoopSceneTree => (SceneTree)Engine.GetMainLoop();

    public static async Task WaitForFrame() => await MainLoopSceneTree.WaitForFrame();
    static async Task WaitForFrame(this SceneTree sceneTree) => 
        await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

    public static async Task WaitForTimer(double time) => await MainLoopSceneTree.WaitForTimer(time);
    static async Task WaitForTimer(this SceneTree sceneTree, double time)=>await sceneTree.CreateTimer(time).WaitForTimer();
    public static async Task WaitForTimer(this SceneTreeTimer timer) => await timer.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);

    public static KeyValuePair<string, JsonNode> CreateKVP(this JsonObject from, string keyTerm)
    {
        return KeyValuePair.Create<string, JsonNode>(from[keyTerm]?.ToString() ?? from.ToString(), from.Reserialise());
    }

    public static int GetCosmeticItemCounts(this JsonObject from)
    {
        int total = 0;
        if (from["brItems"] is JsonArray brItemArray)
            total += brItemArray.Count;
        if (from["tracks"] is JsonArray trackArray)
            total += trackArray.Count;
        if (from["instruments"] is JsonArray instrumentArray)
            total += instrumentArray.Count;
        if (from["cars"] is JsonArray carArray)
            total += carArray.Count;
        if (from["legoKits"] is JsonArray legoArray)
            total += legoArray.Count;
        return total;
    }
    public static JsonObject GetFirstCosmeticItem(this JsonObject from)
    {
        if (from["brItems"] is JsonArray brItemArray)
            return brItemArray[0].AsObject();
        if (from["tracks"] is JsonArray trackArray)
            return trackArray[0].AsObject();
        if (from["instruments"] is JsonArray instrumentArray)
            return instrumentArray[0].AsObject();
        if (from["cars"] is JsonArray carArray)
            return carArray[0].AsObject();
        if (from["legoKits"] is JsonArray legoArray)
            return legoArray[0].AsObject();
        return null;
    }
    public static JsonArray MergeCosmeticItems(this JsonObject from)
    {
        List<JsonNode> resultNodes = new();
        if (from["brItems"] is JsonArray brItemArray)
            resultNodes.AddRange(brItemArray);
        if (from["tracks"] is JsonArray trackArray)
            resultNodes.AddRange(trackArray);
        if (from["instruments"] is JsonArray instrumentArray)
            resultNodes.AddRange(instrumentArray);
        if (from["cars"] is JsonArray carArray)
            resultNodes.AddRange(carArray);
        if (from["legoKits"] is JsonArray legoArray)
            resultNodes.AddRange(legoArray);
        return resultNodes.Count == 0 ? null : new(resultNodes.Select(n => n.Reserialise()).ToArray());
    }

    public static async void RunWithDelay(Task task, float delay)
    {
        await WaitForTimer(delay);
        await task;
    }

    //public static void GetNodeOrNull<T>(this Node owner, NodePath path, out T result) where T : Node
    //{
    //    result = owner.GetNodeOrNull<T>(path);
    //}
    //public static void GetNodesOrNull<T>(this Node owner, NodePath[] paths, out T[] result) where T : Node
    //{
    //    result = new T[paths.Length];
    //    for (int i = 0; i < paths.Length; i++)
    //    {
    //        result[i] = owner.GetNodeOrNull<T>(paths[i]);
    //    }
    //}

    public const string cosmeticSalsa = "676b8175-a049-4f03-b829-323c95153a43";
    public static async Task<JsonNode> MakeRequest(HttpMethod method, System.Net.Http.HttpClient endpoint, string uri, string body, AuthenticationHeaderValue authentication, string mediaType = "application/x-www-form-urlencoded", bool addCosmeticHeader = false)
    {
        using StringContent content = mediaType != "" ? new(body, Encoding.UTF8, mediaType) : null;
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        if (authentication is not null)
            request.Headers.Authorization = authentication;
        if (addCosmeticHeader)
            request.Headers.Add("x-api-key", cosmeticSalsa);
        return await MakeRequest(endpoint, request);
    }

    public static async Task<JsonNode> MakeRequest(System.Net.Http.HttpClient endpoint, HttpRequestMessage request)
    {
        using var result = await MakeRequestRaw(endpoint, request);
        var resultText = result is not null ? await result.Content.ReadAsStringAsync() : null;
        if (resultText is null)
            return null;

        JsonNode resultNode = null;
        try
        {
            resultNode = JsonNode.Parse(resultText);
        }
        catch (JsonException _)
        {
            GD.Print("result was not json: " + resultText);
        }
        //todo: throw exception when encountering a response with an errorMessage

        if (resultNode is JsonObject && resultNode["numericErrorCode"]?.GetValue<int>() == 1031)
        {
            //todo: move this web stuff around so that we know which account is making the request
            GameAccount.activeAccount.ForceExpireToken();
        }

        return resultNode;
    }

    public static async Task<HttpResponseMessage> MakeRequestRaw(System.Net.Http.HttpClient endpoint, HttpRequestMessage request)
    {
        //GD.Print("Debug: "+request.ToString());
        HttpResponseMessage response = null;
        try
        {
            response = await endpoint.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            try
            {
                var resultText = response is not null ? await response.Content.ReadAsStringAsync() : null;
                var resultNode = JsonNode.Parse(resultText);
                if (resultNode["numericErrorCode"]?.GetValue<int>() == 1012)
                {
                    // silences Device Code check fails
                    return response;
                }
            }
            catch (JsonException) { }
            GD.Print("\nException Caught!");
            GD.Print($"Message :{ex.Message} ");
            GD.Print($"Response :{(response is not null ? (await response.Content.ReadAsStringAsync()) : "null response")} ");
        }
        return response;
    }


    static char[] compactNumberMilestones = "KMBT".ToCharArray();
    public static bool debugBool = false;
    public static string Compactify(this int number)
    {
        int milestoneLevel = 0;
        int solidNumber = Mathf.FloorToInt(number);
        int decimalNumber = 0;
        for (int i = 0; i < compactNumberMilestones.Length+1; i++)
        {
            if (solidNumber > 999)
            {
                decimalNumber = solidNumber % 1000;
                solidNumber = Mathf.FloorToInt(solidNumber*0.001);
            }
            else
            {
                milestoneLevel = i;
                break;
            }
        }

        int solidFigures = solidNumber.ToString().Length; //2=>2 1=>1

        if (debugBool)
            Debug.WriteLine($"Debug {solidFigures} {solidNumber} {decimalNumber}");

        decimalNumber = Mathf.FloorToInt(decimalNumber/Mathf.Pow(10, Mathf.Max(0, solidFigures)));//2=>push twice
        bool decimalExists = decimalNumber > 0 && solidFigures != 3;

        string combinedNumber = solidNumber + (decimalExists ? ("." + decimalNumber.ToString()) : "");
        return combinedNumber + (milestoneLevel==0 ? "" : compactNumberMilestones[milestoneLevel-1]);
    }

    public static string FormatTimeSeconds(this int timeInSeconds) =>
        TimeSpan.FromSeconds(timeInSeconds).FormatTime();
    public static string FormatTime(this TimeSpan time)
    {
        string text = time.Seconds.ToString();
        if (time.TotalMinutes >= 1)
        {
            text = time.Minutes + ":" + (time.Seconds < 10 ? "0" : "") + text;
            if (time.TotalHours >= 1)
            {
                text = time.Hours + ":" + (time.Minutes < 10 ? "0" : "") + text;

                if (time.TotalDays >= 1)
                {
                    text = time.Days + ":" + (time.Hours < 10 ? "0" : "") + text;
                }
            }
        }
        return text;
    }

    public static async void StartTask(this Task task) => await task;

    public static JsonObject AsFlexibleObject(this JsonNode node, string objectKey)
    {
        if (node is JsonObject nodeObj)
            return nodeObj;
        return new() { [objectKey] = node.Reserialise()};
    }

    public static JsonArray AsFlexibleArray(this JsonNode node)
    {
        if (node is JsonArray nodeArr)
            return nodeArr;
        return new() { node.Reserialise() };
    }
    public static JsonArray Slice(this JsonArray array, System.Range range)
    {
        (int startIdx, int length) = range.GetOffsetAndLength(array.Count);
        JsonArray result = new();
        for (int i = startIdx; i < startIdx + length; i++)
        {
            result.Add(array[i].Reserialise());
        }
        return result;
    }

    public static int RandomIndexFromWeights(float[] weights, int preventRepeat = -1)
    {
        //remove negative weights
        weights = weights.Select(w => w >= 0 ? w : 0).ToArray();

        if (preventRepeat >= 0 && preventRepeat < weights.Length)
        {
            float backup = weights[preventRepeat];
            weights[preventRepeat] = 0;
            if (weights.All(w => w == 0))
                weights[preventRepeat] = backup;
        }

        float totalRange = weights.Sum();
        float roll = (float)GD.RandRange(0, totalRange);
        float total = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            total += weights[i];
            if(roll<total)
                return i;
        }
        return weights.Length - 1;
    }

    //workaround for bug introduced in 4.3
    public static void FixControlOffsets(this Control control)
    {
        control.OffsetTop = control.OffsetTop;
        control.OffsetBottom = control.OffsetBottom;
        control.OffsetLeft = control.OffsetLeft;
        control.OffsetRight = control.OffsetRight;
    }
    public static void ResetOffsets(this Control control)
    {
        control.OffsetTop = 0;
        control.OffsetBottom = 0;
        control.OffsetLeft = 0;
        control.OffsetRight = 0;
    }

    public static async Task<GameAccount> EnsureProfile(this Task<GameAccount> accountTask, string profileId, bool force = false)
    {
        var account = await accountTask;
        return await account.EnsureProfile(profileId, force);
    }

    public static string ToAttribute(this OrderRange range) => range switch
    {
        OrderRange.Daily => "daily_purchases",
        OrderRange.Weekly => "weekly_purchases",
        OrderRange.Monthly => "monthly_purchases",
        _ => throw new NotImplementedException(),
    };

    public static DateTime ToInterval(this OrderRange range) => range switch
    {
        OrderRange.Daily => RefreshTimerController.GetRefreshTime(RefreshTimeType.Daily).AddDays(-1),
        OrderRange.Weekly => RefreshTimerController.GetRefreshTime(RefreshTimeType.Weekly).AddDays(-7),
        _ => throw new NotImplementedException(),
    };

    public static Func<T, bool> ToFunc<T>(this Predicate<T> predicate, bool defaultResult = true) => t => predicate.Try(t, defaultResult);
    public static bool Try<T>(this Predicate<T> predicate, T t, bool defaultResult = true) => predicate is not null ? predicate(t) : defaultResult;
}