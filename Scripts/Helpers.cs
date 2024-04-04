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
using System.IO;
using static Godot.HttpClient;

static class Helpers
{
    public static async Task WaitForFrame(this Node owner)
    {
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    public static async Task WaitForTimer(this Node owner, double time)
    {
        await owner.ToSignal(owner.GetTree().CreateTimer(time), SceneTreeTimer.SignalName.Timeout);
    }
    public static async Task WaitForTimer(this Node owner, SceneTreeTimer timer)
    {
        await owner.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
    }

    public static async void RunSafely(this Task task)
    {
        await task;
    }

    public static void GetNodeOrNull<T>(this Node owner, NodePath path, out T result) where T : Node
    {
        result = owner.GetNodeOrNull<T>(path);
    }
    public static void GetNodesOrNull<T>(this Node owner, NodePath[] paths, out T[] result) where T : Node
    {
        result = new T[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            result[i] = owner.GetNodeOrNull<T>(paths[i]);
        }
    }

    public static async void RunWithCallback<T>(this Task<T> taskWithResult, Action<T> onComplete)
    {
        T result = await taskWithResult;
        if (result is not null)
            onComplete(result);
    }

    public static async Task<JsonNode> MakeRequest(HttpMethod method, System.Net.Http.HttpClient endpoint, string uri, string body, AuthenticationHeaderValue authentication, string mediaType = "application/x-www-form-urlencoded")
    {
        using StringContent content = mediaType != "" ? new(body, Encoding.UTF8, mediaType) : null;
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = authentication;
        return await MakeRequest(endpoint, request);
    }


    public static async Task<JsonNode> MakeRequest(System.Net.Http.HttpClient endpoint, HttpRequestMessage request, bool disregardStatusCode = false)
    {
        using HttpResponseMessage response = await endpoint.SendAsync(request);
        try
        {
            if (true)
                response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            GD.Print("\nException Caught!");
            GD.Print($"Message :{ex.Message} ");
            GD.Print($"Response :{await response.Content.ReadAsStringAsync()} ");
        }
        string responseBody = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(responseBody);
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
}