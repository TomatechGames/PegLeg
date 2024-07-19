using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

static class CatalogRequests
{
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

    public static async Task<int> GetPurchaseLimitFromOffer(this JsonObject offer)
    {
        string offerId = offer["offerId"].ToString();
        int totalLimit = 999;

        int dailyLimit = offer["dailyLimit"].GetValue<int>();
        if (dailyLimit != -1)
        {
            int purchaseAmount = (await ProfileRequests.GetOrderCounts(ProfileRequests.OrderRange.Daily))?[offerId]?.GetValue<int>() ?? 0;
            //GD.Print($"Daily Limit: {purchaseAmount}/{dailyLimit}");
            totalLimit = Mathf.Min(totalLimit, dailyLimit-purchaseAmount);
        }

        int weeklyLimit = offer["weeklyLimit"].GetValue<int>();
        if (weeklyLimit != -1)
        {
            int purchaseAmount = (await ProfileRequests.GetOrderCounts(ProfileRequests.OrderRange.Weekly))?[offerId]?.GetValue<int>() ?? 0;
            //GD.Print($"Weekly Limit: {purchaseAmount}/{weeklyLimit}");
            totalLimit = Mathf.Min(totalLimit, weeklyLimit - purchaseAmount);
        }

        int monthlyLimit = offer["monthlyLimit"].GetValue<int>();
        if (monthlyLimit != -1)
        {
            int purchaseAmount = (await ProfileRequests.GetOrderCounts(ProfileRequests.OrderRange.Monthly))?[offerId]?.GetValue<int>() ?? 0;
            //GD.Print($"Monthly Limit: {purchaseAmount}/{monthlyLimit}");
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
            //GD.Print($"Event Limit: {purchaseAmount}/{eventLimit}");
            totalLimit = Mathf.Min(totalLimit, eventLimit - purchaseAmount);
        }

        return totalLimit;
    }

    static JsonObject cosmeticCache;
    public static async Task<JsonObject> GetCosmeticShop(bool forceRefresh = false)
    {
        if (!StorefrontRequiresUpdate() && !forceRefresh && cosmeticCache is not null)
            return cosmeticCache;

        var storefrontTask = EnsureStorefront(forceRefresh);
        var cosmeticDisplayData = await RequestCosmeticDisplayData();
        await storefrontTask;

        return cosmeticCache = ProcessCosmetics(cosmeticDisplayData);
    }

    static JsonObject weeklyCache;
    public static async Task<JsonObject> GetWeeklyShop(bool forceRefresh = false)
    {
        if (!StorefrontRequiresUpdate() && !forceRefresh && weeklyCache is not null)
            return weeklyCache;
        await EnsureStorefront(forceRefresh);

        return weeklyCache = ProcessShop(WeeklyShopCatalog);
    }

    static JsonObject eventCache;
    public static async Task<JsonObject> GetEventShop(bool forceRefresh = false)
    {
        if (!StorefrontRequiresUpdate() && !forceRefresh && eventCache is not null)
            return eventCache;
        await EnsureStorefront(forceRefresh);

        return eventCache = ProcessShop(EventShopCatalog);
    }

    static readonly Dictionary<string, string[]> defaultShops = new()
    {
        [WeeklyShopCatalog] = new string[]
        {
            "v2:/8833e6245fe4bf6f0a87e2d248398ec079aac302a1d0b17d036cdd6a1f485d85",
            "v2:/a3eeb54f8f9d2f32ba2f1769a095a9fa406a5c6f239235a8d810d7263cd727e5",
            "v2:/485f70bb37ced8eb25c4b4e42302ee5274532823c17091afb486e1879c4ecc16",
            "v2:/9b91076467e61cf01a3c16e39a18331d2e23d754cdafc860aac0fdd7155615ae",
            "v2:/365d69d31591ba699bdf2c89730b8fa02883302ac56d1bd43b06d81f2ef25f0e",
            "v2:/d9fe40e917bf98babee1c239153990efe3e1a568dd0e985c663dbba228eef03f",
            "v2:/bfd337ddb7380a663929ae0ad03f6cdbff5b562d1639c8c813cb8316b37f83bb",
            "v2:/d8c8f59ca26294a0192676567f75ee6c3631f96eea201fd14f8cac0c47acfb5c",
            "v2:/4f1c82dc8fb66fef5a0046fb2163344069b65b6ba64e496939d2fc8e8f779157",
            "v2:/9af32d7a9a16f864eae99d17542ec08763d118f3ce9c72ad05d5fc5f44586dc1",
            "v2:/fd2b5edc1839496be18a0cb1ef1bc74c07f391b4448de53d07bb63f695f1763b"
        },
        [EventShopCatalog] = new string[]
        {
            "v2:/222374fc7ea9f6ef8eb0b3c20f3a5d7f64f612e9f3435c74e3d51d785739bf9f",
            "v2:/570ff3bed6fc8a1f7006610dbb6ce9e4bcd244a32caa435a60392460da356c88",
            "v2:/6633ab8087f2a2e80bdf7a90d06351e7a03b82790cc2e286f4b6851020532ed4",
            "v2:/5c841be6c7cf1635cca83f2d4c345242c85192bf5beda2af0317e1cc745a3a38",
            "v2:/bfe19601a5107b1a6ba83ab25ac9fef02ae14b78ee451ab33c6b5218938183c4"
        }
    };

    static JsonObject ProcessShop(string shopId)
    {
        var shopOffers = storefrontCache[shopId].AsArray().Reserialise();
        JsonArray highlights = new();
        for (int i = 0; i < shopOffers.Count; i++)
        {
            var item = shopOffers[i];
            if (!(defaultShops[shopId]?.Contains(item["offerId"].ToString()) ?? true))
            {
                highlights.Add(item.Reserialise());
                shopOffers.RemoveAt(i);
                i--;
            }
        }

        return new()
        {
            ["regular"] = shopOffers,
            ["highlights"] = highlights
        };
    }

    static JsonObject ProcessCosmetics(JsonObject cosmeticDisplayData)
    {
        var shopOfferList = storefrontCache[WeeklyCosmeticShopCatalog].AsArray().ToList();
        shopOfferList.AddRange(storefrontCache[DailyCosmeticShopCatalog].AsArray());
        var shopOfferDict = shopOfferList.ToDictionary(n => n["offerId"].ToString());

        foreach (var offer in shopOfferDict)
        {
            if (!cosmeticDisplayData.ContainsKey(offer.Key))
                continue;

            //additions
            cosmeticDisplayData[offer.Key]["inDate"] = offer.Value["meta"]?["inDate"]?.ToString() ?? null;
            cosmeticDisplayData[offer.Key]["outDate"] = offer.Value["meta"]?["outDate"]?.ToString() ?? null;

            if (offer.Value.AsObject().ContainsKey("dynamicBundleInfo"))
                cosmeticDisplayData[offer.Key]["dynamicBundleInfo"] = offer.Value["dynamicBundleInfo"].Reserialise();

            if ((offer.Value["prices"]?.AsArray().Count ?? 0) > 0)
                cosmeticDisplayData[offer.Key]["prices"] = offer.Value["prices"].Reserialise();

            //removals
            //if (cosmeticDisplayData[offer.Key]["newDisplayAsset"]?["materialInstances"] is not null)
            //    cosmeticDisplayData[offer.Key]["newDisplayAsset"].AsObject().Remove("materialInstances");

            //void RemoveHistoryFromItemList(string listKey)
            //{
            //    if (cosmeticDisplayData[offer.Key][listKey] is JsonArray itemList)
            //    {
            //        foreach (var item in itemList)
            //        {
            //            if (item["shopHistory"] is JsonArray shopHistory)
            //            {
            //                item["firstShopAppearance"] = shopHistory[0].ToString();
            //                if (shopHistory.Count > 1)
            //                    item["lastRecentShopAppearance"] = shopHistory[^2].ToString();
            //            }
            //            item.AsObject().Remove("shopHistory");
            //        }
            //    }
            //}
            //RemoveHistoryFromItemList("brItems");
            //RemoveHistoryFromItemList("tracks");
            //RemoveHistoryFromItemList("instruments");
            //RemoveHistoryFromItemList("cars");
            //RemoveHistoryFromItemList("legoKits");

            //jam tracks are funky, gotta reformat them
            if (cosmeticDisplayData[offer.Key]["tracks"] is JsonArray trackList)
            {
                foreach (var item in trackList)
                {
                    var trackObj = item.AsObject();
                    trackObj["name"] = trackObj["title"].ToString();
                    trackObj["type"] = new JsonObject()
                    {
                        ["value"] = "track",
                        ["displayValue"] = "Jam Track",
                    };
                    trackObj["rarity"] = new JsonObject()
                    {
                        ["value"] = "rare",
                        ["displayValue"] = "Rare",
                        ["backendValue"] = "EFortRarity::Rare",
                    };
                    trackObj["images"] = new JsonObject()
                    {
                        ["icon"] = trackObj["albumArt"].ToString(),
                    };
                    trackObj["description"] =
                        (trackObj["artist"] is JsonValue artist ? $"Artist: \"{artist}\"\n" : "") +
                        (trackObj["album"] is JsonValue album ? $"Album: \"{album}\"\n" : "") +
                        (trackObj["releaseYear"] is JsonValue releaseYear ? $"Released in: {releaseYear}\n" : "") +
                        (trackObj["duration"] is JsonValue duration ? $"Duration: {duration.GetValue<int>().FormatTimeSeconds()}\n" : "");
                }
            }
        }

        var partiallyOrganisedCosmetics = cosmeticDisplayData
            .Select(n => KeyValuePair.Create(n.Key, n.Value.Reserialise()))
            .OrderBy(n => -n.Value["sortPriority"].GetValue<int>())// sort by offer index (descending)
            .GroupBy(n => n.Value["layoutId"].ToString())// group into pages
            .OrderBy(p => -int.Parse(p.Key.Split(".")[^1]))// sort by page index (descending)
            .GroupBy(p => p.First().Value["layout"]["name"].ToString())// group by page header
            .OrderBy(g => g.First().First().Value["layout"]["index"].GetValue<int>())// sort by page header index
            .GroupBy(g => g.First().First().Value["layout"]?["category"]?.ToString() ?? "Uncategorised");// group by page category

        //partiallyOrganisedCosmetics.Select(g =>
        //{
        //    GD.Print(g.Key+":"+(g.First().Value["layout"]?["category"]?.ToString() ?? "missing"));
        //    return g;
        //}).ToArray();

        var organisedCosmetics = partiallyOrganisedCosmetics
            .Select(c =>
                KeyValuePair.Create<string, JsonNode>(c.Key, new JsonObject(c.Select(g =>
                        KeyValuePair.Create<string, JsonNode>(g.Key, new JsonArray(g.Select(p=>new JsonObject(p)).ToArray()))
                    )))
                );
        JsonObject organisedCosmeticsJson = new(organisedCosmetics);

        if (organisedCosmeticsJson.ContainsKey("Uncategorised"))
        {
            //moves uncategorised sections to the end of the list
            var uncategorised = organisedCosmeticsJson["Uncategorised"].AsObject();
            organisedCosmeticsJson.Remove("Uncategorised");
            organisedCosmeticsJson["Uncategorised"] = uncategorised;
        }

        return organisedCosmeticsJson;
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

        if (storefrontCache is not null)
        {
            var refreshTime = DateTime.Parse(storefrontCache["expiration"].ToString(), null, DateTimeStyles.RoundtripKind);
            if (DateTime.UtcNow.CompareTo(refreshTime) >= 0)
            {
                storefrontCache = null;
            }
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
        if (!await LoginRequests.TryLogin())
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

    public static async Task<JsonObject> RequestCosmeticDisplayData()
    {
        GD.Print("retrieving cosmetic visuals from fortnite-api...");
        JsonNode cosmeticDisplayData = await Helpers.MakeRequest(
                HttpMethod.Get,
                ExternalEndpoints.cosmeticsEndpoint,
                "v2/shop",
                "",
                null,
                addCosmeticHeader:true
            );
        if (cosmeticDisplayData["data"]?["entries"]?.AsArray().Count == 0)
            GD.Print(cosmeticDisplayData.ToString());
        return new(cosmeticDisplayData["data"]["entries"].AsArray()
            .Select(n => n.AsObject().CreateKVP("offerId")));
    }

    const string XRayLlamaCatalog = "CardPackStorePreroll";
    const string RandomLlamaCatalog = "CardPackStoreGameplay";
    const string WeeklyShopCatalog = "STWRotationalEventStorefront";
    const string EventShopCatalog = "STWSpecialEventStorefront";
    const string WeeklyCosmeticShopCatalog = "BRWeeklyStorefront";
    const string DailyCosmeticShopCatalog = "BRDailyStorefront";
    static readonly string[] relevantStorefronts = new string[]
    {
        XRayLlamaCatalog,
        RandomLlamaCatalog,
        WeeklyShopCatalog,
        EventShopCatalog,
        WeeklyCosmeticShopCatalog,
        DailyCosmeticShopCatalog
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

    const string fnapiLinkPrefix = "https://fortnite-api.com/images/cosmetics/";
    const string cacheFolderPath = "user://cosmetic_images/";
    static readonly Dictionary<string, WeakRef> activeResourceCache = new();

    public static ImageTexture GetLocalCosmeticResource(string serverPath)
    {
        if (activeResourceCache.ContainsKey(serverPath) && activeResourceCache[serverPath].GetRef().Obj is ImageTexture cachedTexture)
            return cachedTexture;

        string localPath = cacheFolderPath + serverPath[fnapiLinkPrefix.Length..].Replace("/", "-");

        if (!FileAccess.FileExists(localPath))
            return null;

        Image resourceImage = new();
        using var imageFile = FileAccess.Open(localPath, FileAccess.ModeFlags.ReadWrite);
        if (localPath.EndsWith(".png"))
            resourceImage.LoadPngFromBuffer(imageFile.GetBuffer((long)imageFile.GetLength()));
        else if (localPath.EndsWith(".webp"))
            resourceImage.LoadWebpFromBuffer(imageFile.GetBuffer((long)imageFile.GetLength()));

        //make a fake modification to change the modified date when the file is disposed
        imageFile.SeekEnd(-1);
        byte temp = imageFile.Get8();
        imageFile.SeekEnd(-1);
        imageFile.Store8(temp);

        var imageTex = ImageTexture.CreateFromImage(resourceImage);
        activeResourceCache[serverPath] = GodotObject.WeakRef(imageTex);

        return imageTex;
    }

    public static async Task<ImageTexture> GetCosmeticResource(string serverPath)
    {
        var localImageTex = GetLocalCosmeticResource(serverPath);
        if(localImageTex is not null)
            return localImageTex;

        string localPath = cacheFolderPath + serverPath[fnapiLinkPrefix.Length..].Replace("/", "-");

        using var request =
        new HttpRequestMessage(
            HttpMethod.Get,
            "/images/cosmetics/"+serverPath[fnapiLinkPrefix.Length..]
        );

        GD.Print($"Requesting cosmetic \"{serverPath}\"");
        using var result = await Helpers.MakeRequestRaw(ExternalEndpoints.cosmeticsEndpoint, request);
        if (!result.IsSuccessStatusCode)
        {
            GD.Print(result);
            return null;
        }

        Image resourceImage = new();
        byte[] imageBuffer = await result.Content.ReadAsByteArrayAsync();
        var error = LoadImageWithCtx(resourceImage, imageBuffer, serverPath);
        if (error != Error.Ok)
            return null;

        if (!DirAccess.DirExistsAbsolute(cacheFolderPath))
            DirAccess.MakeDirAbsolute(cacheFolderPath);
        using var imageFile = FileAccess.Open(localPath, FileAccess.ModeFlags.Write);
        imageFile.StoreBuffer(imageBuffer);

        var imageTex = ImageTexture.CreateFromImage(resourceImage);
        activeResourceCache[serverPath] = GodotObject.WeakRef(imageTex);

        return imageTex;
    }

    static Error LoadImageWithCtx(Image image, byte[] data, string path)
    {
        var urlEnding = path.Split(".")[^1].ToLower();
        switch (urlEnding)
        {
            case "png":
                return image.LoadPngFromBuffer(data);
            case "webp":
                return image.LoadWebpFromBuffer(data);
            default:
                return Error.Failed;
        }
    }

    public static void CleanCosmeticResourceCache()
    {
        if (!DirAccess.DirExistsAbsolute(cacheFolderPath))
            return;

        var cacheFolder = DirAccess.Open(cacheFolderPath);
        DateTime invalidDateTime = DateTime.Now.AddDays(-2); //images are removed if they havent been used in more than 2 days
        var invalidCacheFilePaths = cacheFolder.GetFiles()
            .Where(p => p.EndsWith(".png") || p.EndsWith(".webp"))
            .Select(p => cacheFolderPath + "/" + p)
            .Where(p => DateTime.Parse(Time.GetDatetimeStringFromUnixTime((long)FileAccess.GetModifiedTime(p))).CompareTo(invalidDateTime) < 0);

        foreach (var filePath in invalidCacheFilePaths)
        {
            GD.Print($"Cleaning cosmetic \"{filePath}\" ({DateTime.Parse(Time.GetDatetimeStringFromUnixTime((long)FileAccess.GetModifiedTime(filePath)))})");
            DirAccess.RemoveAbsolute(filePath);
        }
    }

}
