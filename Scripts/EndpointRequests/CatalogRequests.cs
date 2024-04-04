using Godot;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

static class CatalogRequests
{

    const string storefrontCacheSavePath = "user://epicStorefront.json";
    static JsonObject storefrontCache;

    static JsonObject[] llamaCache;
    public static async Task<JsonObject[]> GetLlamaShop(bool forceRefresh = false)
    {
        if(!StorefrontRequiresUpdate() && !forceRefresh && llamaCache is not null)
            return llamaCache;

        await EnsureStorefront(forceRefresh);

        var prerollOffers = storefrontCache[XRayLlamaCatalog].AsArray();

        if (!prerollOffers[0].AsObject().ContainsKey("prerollData"))
        {
            //assume that prerolls havent been generated for any offer
            var prerollDatas = await ProfileRequests.GetAllPrerollDatas();
            for (int i = 0; i < prerollOffers.Count; i++)
            {
                var thisOffer = prerollOffers[i].AsObject();
                var thisPreroll = prerollDatas.FirstOrDefault(val => val["attributes"]?["offerId"]?.ToString() == thisOffer["offerId"].ToString());
                thisPreroll ??= prerollDatas.FirstOrDefault(val => val["attributes"]?["linked_offer"]?.ToString() == "OfferId:" + thisOffer["offerId"].ToString());
                if (thisPreroll is not null)
                    thisOffer["prerollData"] = thisPreroll.Reserialise();
            }
        }
        llamaCache = prerollOffers
            .Select(val=>val.AsObject().Reserialise())
            .Union(
                storefrontCache[RandomLlamaCatalog]
                .AsArray()
                .Select(val=>val.AsObject().Reserialise())
            )
            .ToArray();

        return llamaCache;
    }

    public static DateTime DailyRefreshTime()
    {
        return DateTime.UtcNow.Date.AddDays(1);
    }
    public static DateTime WeeklyRefreshTime()
    {
        var daysTilThursday = (7 + 4 - (int)DateTime.UtcNow.DayOfWeek) % 7;
        return DateTime.UtcNow.Date.AddDays(daysTilThursday);
    }
    public static DateTime MonthlyRefreshTime()
    {
        var monthFromNow = DateTime.UtcNow.Date.AddMonths(1);
        return monthFromNow.AddDays((-monthFromNow.Day)+1);
    }

    public static async Task<int> GetPurchaseLimitFromOffer(this JsonObject offer)
    {
        string offerId = offer["offerId"].ToString();
        int totalLimit = 999;

        int dailyLimit = offer["dailyLimit"].GetValue<int>();
        if (dailyLimit != -1)
        {
            int purchaseAmount = (await ProfileRequests.GetOrderCounts(ProfileRequests.OrderRange.Daily))?[offerId]?.GetValue<int>() ?? 0;
            GD.Print($"Daily Limit: {purchaseAmount}/{dailyLimit}");
            totalLimit = Mathf.Min(totalLimit, dailyLimit-purchaseAmount);
        }

        int weeklyLimit = offer["weeklyLimit"].GetValue<int>();
        if (weeklyLimit != -1)
        {
            int purchaseAmount = (await ProfileRequests.GetOrderCounts(ProfileRequests.OrderRange.Weekly))?[offerId]?.GetValue<int>() ?? 0;
            GD.Print($"Weekly Limit: {purchaseAmount}/{weeklyLimit}");
            totalLimit = Mathf.Min(totalLimit, weeklyLimit - purchaseAmount);
        }

        int monthlyLimit = offer["monthlyLimit"].GetValue<int>();
        if (monthlyLimit != -1)
        {
            int purchaseAmount = (await ProfileRequests.GetOrderCounts(ProfileRequests.OrderRange.Monthly))?[offerId]?.GetValue<int>() ?? 0;
            GD.Print($"Monthly Limit: {purchaseAmount}/{monthlyLimit}");
            totalLimit = Mathf.Min(totalLimit, monthlyLimit - purchaseAmount);
        }

        int eventLimit = int.Parse(offer["metaInfo"]?.AsArray().FirstOrDefault(val => val["key"].ToString() == "EventLimit")?["value"].ToString() ?? "-1");
        if (eventLimit != -1)
        {
            string eventId = offer["metaInfo"].AsArray().First(val => val["key"].ToString() == "PurchaseLimitingEventId")["value"].ToString();

            JsonObject eventTracker = (await ProfileRequests.GetProfileItems(FnProfiles.Common, kvp =>
                    kvp.Value["templateId"].ToString().StartsWith("EventPurchaseTracker") &&
                    kvp.Value["attributes"]["event_instance_id"].ToString() == eventId
                )).FirstOrDefault().Value?.AsObject();

            int purchaseAmount = eventTracker?["attributes"]["event_purchases"]?[offerId]?.GetValue<int>() ?? 0;
            GD.Print($"Event Limit: {purchaseAmount}/{eventLimit}");
            totalLimit = Mathf.Min(totalLimit, eventLimit - purchaseAmount);
        }

        //TODO: event limit
        return totalLimit;
    }

    static JsonObject weeklyCache;
    public static async Task<JsonObject> GetWeeklyShop(bool forceRefresh = false)
    {
        if (!StorefrontRequiresUpdate() && !forceRefresh && weeklyCache is not null)
            return weeklyCache;
        await EnsureStorefront(forceRefresh);
        return weeklyCache = storefrontCache[WeeklyShopCatalog].AsObject().Reserialise();
    }

    static JsonObject eventCache;
    public static async Task<JsonObject> GetEventShop(bool forceRefresh = false)
    {
        if (!StorefrontRequiresUpdate() && !forceRefresh && eventCache is not null)
            return eventCache;
        await EnsureStorefront(forceRefresh);
        return eventCache = storefrontCache[WeeklyShopCatalog].AsObject().Reserialise();
    }

    public static bool StorefrontRequiresUpdate()
    {
        if (storefrontCache is null)
            return true;
        var expirationTime = DateTime.Parse(storefrontCache["expiration"].ToString(), null, DateTimeStyles.RoundtripKind);
        return DateTime.UtcNow.CompareTo(expirationTime) >= 0;
    }

    static Task<JsonObject> activeStorefrontRequest = null;
    static async Task EnsureStorefront(bool forceRefresh)
    {
        if (activeStorefrontRequest is not null && activeStorefrontRequest.IsCompleted)
            activeStorefrontRequest = null;

        if (forceRefresh)
        {
            GD.Print("forcing refresh");
            storefrontCache = null;
        }
        //else if (storefrontCache is null && FileAccess.FileExists(storefrontCacheSavePath))
        //{
        //    //load from file
        //    using FileAccess storefrontFile = FileAccess.Open(storefrontCacheSavePath, FileAccess.ModeFlags.Read);
        //    storefrontCache = JsonNode.Parse(storefrontFile.GetAsText()).AsObject();
        //    Debug.WriteLine("storefront file loaded");
        //}

        if (storefrontCache is not null)
        {
            var refreshTime = DateTime.Parse(storefrontCache["expiration"].ToString(), null, DateTimeStyles.RoundtripKind);
            if (DateTime.UtcNow.CompareTo(refreshTime) >= 0)
                storefrontCache = null;
        }


        if (storefrontCache is null)
        {
            GD.Print("requesting storefront");
            activeStorefrontRequest ??= RequestStorefront();
            await Task.WhenAny(activeStorefrontRequest);
        }
    }

    static async Task<JsonObject> RequestStorefront()
    {
        if (!await LoginRequests.WaitForLogin())
            return null;

        GD.Print("retrieving catalog from epic...");
        JsonNode fullStorefront = await Helpers.MakeRequest(
                HttpMethod.Get,
                FNEndpoints.gameEndpoint,
                "fortnite/api/storefront/v2/catalog",
                "",
                LoginRequests.AccountAuthHeader
            );
        storefrontCache = SimplifyStorefront(fullStorefront);
        //save to file
        //using FileAccess storefrontFile = FileAccess.Open(storefrontCacheSavePath, FileAccess.ModeFlags.Write);
        //storefrontFile.StoreString(storefrontCache.ToString());
        //storefrontFile.Flush();
        return storefrontCache;
    }

    const string XRayLlamaCatalog = "CardPackStorePreroll";
    const string RandomLlamaCatalog = "CardPackStoreGameplay";
    const string WeeklyShopCatalog = "STWRotationalEventStorefront";
    const string EventShopCatalog = "STWSpecialEventStorefront";
    static readonly string[] relevantStorefronts = new string[]
    {
        XRayLlamaCatalog,
        RandomLlamaCatalog,
        WeeklyShopCatalog,
        EventShopCatalog
    };

    static JsonObject SimplifyStorefront(JsonNode fullStorefront)
    {
        var filteredStorefronts = fullStorefront["storefronts"].AsArray().Where(val => relevantStorefronts.Contains(val["name"].ToString()));
        JsonObject jsonFilteredStorefronts = new()
        {
            ["expiration"] = fullStorefront["expiration"].ToString()
        };
        foreach (var item in filteredStorefronts)
        {
            jsonFilteredStorefronts.Add(item["name"].ToString(), item["catalogEntries"].Reserialise());
        }
        return jsonFilteredStorefronts;
    }

}
