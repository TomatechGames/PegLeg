using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

static class FNEndpoints
{
    public static readonly System.Net.Http.HttpClient serviceEndpoint = new()
    {
        BaseAddress = new Uri("https://fortnite-public-service-prod11.ol.epicgames.com")
    };

    public static readonly System.Net.Http.HttpClient gameEndpoint = new()
    {
        BaseAddress = new Uri("https://fngw-mcp-gc-livefn.ol.epicgames.com")
    };

    public static readonly System.Net.Http.HttpClient loginEndpoint = new()
    {
        BaseAddress = new Uri("https://account-public-service-prod.ol.epicgames.com")
    };
}

static class ExternalEndpoints
{
    public static readonly System.Net.Http.HttpClient cosmeticsEndpoint = new()
    {
        BaseAddress = new Uri("https://fortnite-api.com")
    };
}
