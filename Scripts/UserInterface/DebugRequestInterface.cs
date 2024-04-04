using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class DebugRequestInterface : Control
{
    static readonly string[] endpointUrls = new string[]
    {
        "https://fngw-mcp-gc-livefn.ol.epicgames.com",
        "https://account-public-service-prod.ol.epicgames.com"
    };

    [Export]
    NodePath resultBoxPath;
    TextEdit resultBox;

    readonly List<System.Net.Http.HttpClient> endpointClients = new();

    public override void _Ready()
    {
        resultBox = GetNode<TextEdit>(resultBoxPath);
        foreach (var url in endpointUrls)
        {
            endpointClients.Add(new() { BaseAddress = new(url) });
        }
        //SubmitRequest();
    }

    static StringContent blankJsonContent = new("{}", Encoding.UTF8, "application/json");
    public async void SubmitRequest(string profileId, string route, string operation)
	{
        if (!await LoginRequests.WaitForLogin())
            return;

        using var request = 
            new HttpRequestMessage(
                HttpMethod.Post, 
                $"fortnite/api/game/v2/profile/{LoginRequests.AccountID}/{route}/{operation}?profileId={profileId}&rvn=-1" 
            ) 
            { 
                Content = blankJsonContent 
            };

        request.Headers.Authorization = LoginRequests.AccountAuthHeader;

        JsonNode result = 
            await Helpers.MakeRequest(
                endpointClients[0],
                request,
                true
            );

        resultBox.Text = result?.ToString();
    }
}
