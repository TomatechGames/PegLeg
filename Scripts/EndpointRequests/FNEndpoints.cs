using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

static class FNEndpoints
{
    public static readonly System.Net.Http.HttpClient gameEndpoint = new()
    {
        BaseAddress = new Uri("https://fngw-mcp-gc-livefn.ol.epicgames.com")
    };

    public static readonly System.Net.Http.HttpClient loginEndpoint = new()
    {
        BaseAddress = new Uri("https://account-public-service-prod.ol.epicgames.com")
    };
}
