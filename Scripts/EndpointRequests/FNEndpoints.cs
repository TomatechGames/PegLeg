using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

static class FNEndpoints
{
    public static readonly HttpClient serviceEndpoint = new()
    {
        BaseAddress = new Uri("https://fortnite-public-service-prod11.ol.epicgames.com")
    };

    public static readonly HttpClient gameEndpoint = new()
    {
        BaseAddress = new Uri("https://fngw-mcp-gc-livefn.ol.epicgames.com")
    };

    public static readonly HttpClient loginEndpoint = new()
    {
        BaseAddress = new Uri("https://account-public-service-prod.ol.epicgames.com")
    };
}

static class ExternalEndpoints
{
    public static readonly HttpClient cosmeticsEndpoint = new()
    {
        BaseAddress = new Uri("https://fortnite-api.com")
    };
    public static readonly HttpClient jamTracksEndpoint = new()
    {
        BaseAddress = new Uri("https://cdn.fortnite-api.com")
    };
}
