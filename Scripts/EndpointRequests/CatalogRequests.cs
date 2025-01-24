using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
//using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

static class CatalogRequests
{
    static JsonObject storefrontCache;

    //static JsonObject[] llamaCache;
    //public static async Task<JsonObject[]> GetLlamaShop(bool forceRefresh = false)
    //{
    //    if(!StorefrontRequiresUpdate() && !forceRefresh && llamaCache is not null)
    //        return llamaCache;

    //    await EnsureStorefront(forceRefresh);

    //    var prerollOffers = storefrontCache[XRayLlamaCatalog].AsArray();

    //    if (!prerollOffers[0].AsObject().ContainsKey("prerollData"))
    //    {
    //        //assume that prerolls havent been generated for any offer
    //        var prerollData = await GameAccount.activeAccount.GetAllPrerollData();
    //        for (int i = 0; i < prerollOffers.Count; i++)
    //        {
    //            var thisOffer = prerollOffers[i].AsObject();
    //            var thisPreroll = prerollData.FirstOrDefault(item => item.attributes?["offerId"]?.ToString() == thisOffer["offerId"].ToString());
    //            thisPreroll ??= prerollData.FirstOrDefault(item => item.attributes?["linked_offer"]?.ToString() == "OfferId:" + thisOffer["offerId"].ToString());
    //            if (thisPreroll is not null)
    //                thisOffer["prerollData"] = thisPreroll.attributes.Reserialise();
    //        }
    //    }
    //    llamaCache = prerollOffers
    //        .Select(val=>val.AsObject().Reserialise())
    //        .Union(
    //            storefrontCache[RandomLlamaCatalog]
    //            .AsArray()
    //            .Select(val=>val.AsObject().Reserialise())
    //        )
    //        .ToArray();

    //    return llamaCache;
    //}


    static JsonObject cosmeticCache;
    public static async Task<JsonObject> GetCosmeticShop(bool forceRefresh = false)
    {
        if (!StorefrontRequiresUpdate() && !forceRefresh && cosmeticCache is not null)
            return cosmeticCache;

        var storefrontTask = EnsureStorefront(forceRefresh);
        var cosmeticDisplayData = await RequestCosmeticDisplayData();
        await storefrontTask;

        return cosmeticCache = await ProcessCosmetics(cosmeticDisplayData);
    }

    //static JsonObject weeklyCache;
    //public static async Task<JsonObject> GetWeeklyShop(bool forceRefresh = false)
    //{
    //    if (!StorefrontRequiresUpdate() && !forceRefresh && weeklyCache is not null)
    //        return weeklyCache;
    //    await EnsureStorefront(forceRefresh);

    //    return weeklyCache = ProcessShop(WeeklyShopCatalog);
    //}

    //static JsonObject eventCache;
    //public static async Task<JsonObject> GetEventShop(bool forceRefresh = false)
    //{
    //    if (!StorefrontRequiresUpdate() && !forceRefresh && eventCache is not null)
    //        return eventCache;
    //    await EnsureStorefront(forceRefresh);

    //    return eventCache = ProcessShop(EventShopCatalog);
    //}

    static readonly Dictionary<string, string[]> defaultShops = new()
    {
        [FnStorefrontTypes.WeeklyShopCatalog] = new string[]
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
        [FnStorefrontTypes.EventShopCatalog] = new string[]
        {
            "v2:/222374fc7ea9f6ef8eb0b3c20f3a5d7f64f612e9f3435c74e3d51d785739bf9f",
            "v2:/570ff3bed6fc8a1f7006610dbb6ce9e4bcd244a32caa435a60392460da356c88",
            "v2:/6633ab8087f2a2e80bdf7a90d06351e7a03b82790cc2e286f4b6851020532ed4",
            "v2:/5c841be6c7cf1635cca83f2d4c345242c85192bf5beda2af0317e1cc745a3a38",
            "v2:/bfe19601a5107b1a6ba83ab25ac9fef02ae14b78ee451ab33c6b5218938183c4"
        }
    };

    //static JsonObject ProcessShop(string shopId)
    //{
    //    var shopOffers = storefrontCache[shopId]?.AsArray().Reserialise();
    //    JsonArray highlights = new();
    //    for (int i = 0; i < shopOffers.Count; i++)
    //    {
    //        var item = shopOffers[i];
    //        if (!(defaultShops[shopId]?.Contains(item["offerId"].ToString()) ?? true))
    //        {
    //            highlights.Add(item.Reserialise());
    //            shopOffers.RemoveAt(i);
    //            i--;
    //        }
    //    }

    //    return new()
    //    {
    //        ["regular"] = shopOffers,
    //        ["highlights"] = highlights
    //    };
    //}

    static string ParseLayoutName(string basis) =>
        Regex.Replace(basis, "([a-z0-9])([A-Z])", "$1 $2");

    static async Task<JsonObject> ProcessCosmetics(JsonObject cosmeticDisplayData)
    {
        var shopOfferList = storefrontCache[FnStorefrontTypes.WeeklyCosmeticShopCatalog]?.AsArray().ToList();
        if(shopOfferList is null)
            return null;
        shopOfferList.AddRange(storefrontCache[FnStorefrontTypes.DailyCosmeticShopCatalog].AsArray());
        var shopOfferDict = shopOfferList.ToDictionary(n => n["offerId"].ToString());

        await Parallel.ForEachAsync(shopOfferDict, async (offer, _) =>
        {
            if (!cosmeticDisplayData.ContainsKey(offer.Key))
            {
                var fallbackDisplayData = new JsonObject()
                {
                    ["isFallback"] = true,
                    ["devName"] = offer.Value["devName"]?.ToString() ?? null,
                    ["fallbackType"] = offer.Value["meta"]?["templateId"]?.ToString().Split(":")[0] ?? null,
                    ["offerId"] = offer.Value["offerId"]?.ToString() ?? null,
                    ["inDate"] = offer.Value["meta"]?["inDate"]?.ToString() ?? null,
                    ["outDate"] = offer.Value["meta"]?["outDate"]?.ToString() ?? null,
                    ["regularPrice"] = offer.Value["prices"]?.AsArray().FirstOrDefault()?["regularPrice"]?.GetValue<int>() ?? null,
                    ["finalPrice"] = offer.Value["prices"]?.AsArray().FirstOrDefault()?["finalPrice"]?.GetValue<int>() ?? null,
                    ["webURL"] = offer.Value["meta"]?["webURL"]?.ToString() ?? null,
                    ["layoutId"] = offer.Value["meta"]?["LayoutId"]?.ToString() ?? null,
                    ["layout"] = new JsonObject()
                    {
                        ["id"] = offer.Value["meta"]?["AnalyticOfferGroupId"]?.ToString() ?? null,
                        ["name"] = offer.Value["meta"]?["AnalyticOfferGroupId"]?.ToString() ?? null,
                    },
                    ["tileSize"] = offer.Value["meta"]?["TileSize"]?.ToString() ?? null,
                    ["sortPriority"] = offer.Value["sortPriority"]?.GetValue<int>() ?? null,
                };
                if (offer.Value["dynamicBundleInfo"] is JsonObject bundleInfo)
                {
                    int totalPrice = bundleInfo["bundleItems"].AsArray().Select(n => n["regularPrice"].GetValue<int>()).Sum();
                    fallbackDisplayData["regularPrice"] = totalPrice;
                    fallbackDisplayData["finalPrice"] = totalPrice + bundleInfo["discountedBasePrice"].GetValue<int>();
                }
                if (false && offer.Value["meta"]?["NewDisplayAssetPath"]?.ToString() is string imgDaPath && imgDaPath.Contains("/NewDisplayAssetPath/"))
                {
                    imgDaPath = imgDaPath.Replace("/Game/Catalog/", "/OfferCatalog/");
                    bool isBundle = offer.Value["dynamicBundleInfo"] is not null;
                    string nameDaPath = isBundle ?
                        offer.Value["meta"]?["displayAssetPath"]?.ToString().Replace("/Game/Catalog/", "/OfferCatalog/"):"";

                    var nameDisplayAssetTask = Helpers.MakeRequest(
                        HttpMethod.Get,
                        ExternalEndpoints.fnCentralEndpoint,
                        $"api/v1/export?path={nameDaPath}",
                        "",
                        null
                    );
                    var imageDisplayAssetTask = Helpers.MakeRequest(
                        HttpMethod.Get,
                        ExternalEndpoints.fnCentralEndpoint,
                        $"api/v1/export?path={imgDaPath}",
                        "",
                        null
                    );

                    await imageDisplayAssetTask;
                    await imageDisplayAssetTask;

                    fallbackDisplayData["fallbackImage"] = imageDisplayAssetTask.Result?["jsonOutput"]?[0]["Properties"][0];
                }
                cosmeticDisplayData.Add(offer.Key, fallbackDisplayData);
                return;
            }

            var displayData = cosmeticDisplayData[offer.Key];

            //additions
            displayData["webURL"] = offer.Value["meta"]?["webURL"]?.ToString() ?? null;
            displayData["inDate"] = offer.Value["meta"]?["inDate"]?.ToString() ?? null;
            displayData["outDate"] = offer.Value["meta"]?["outDate"]?.ToString() ?? null;

            if (offer.Value["dynamicBundleInfo"] is JsonObject dynBundleInfo)
                displayData["dynamicBundleInfo"] = dynBundleInfo.Reserialise();

            //sometimes these are just missing
            displayData["layoutId"] ??= offer.Value["meta"]?["LayoutId"].ToString() ?? "?";
            displayData["layout"] ??= new JsonObject();
            displayData["layout"]["name"] ??= ParseLayoutName(offer.Value["meta"]?["AnalyticOfferGroupId"].ToString() ?? "?");
            displayData["layout"]["index"] = int.TryParse(displayData["layoutId"].ToString().Split(".")[^1], out int result) ? result : 0;


            if (offer.Value.AsObject().ContainsKey("dynamicBundleInfo"))
                displayData["dynamicBundleInfo"] = offer.Value["dynamicBundleInfo"].Reserialise();

            if ((offer.Value["prices"]?.AsArray().Count ?? 0) > 0)
                displayData["prices"] = offer.Value["prices"].Reserialise();

            if (!(displayData["layout"]["category"]?.ToString() is string cat && !string.IsNullOrWhiteSpace(cat)))
                displayData["layout"]["category"] = "Uncategorised";

            //jam tracks are funky, gotta reformat them
            if (displayData["tracks"] is JsonArray trackList)
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

            cosmeticDisplayData[offer.Key] = displayData;
        });

        var partiallyOrganisedCosmetics = cosmeticDisplayData
            .Select(n => KeyValuePair.Create(n.Key, n.Value.Reserialise()))
            .OrderBy(n => -n.Value["sortPriority"]?.GetValue<int>() ?? 0)// sort by offer index (descending)
            .GroupBy(n => n.Value["layoutId"]?.ToString() ?? "Unknown")// group into pages
            .OrderBy(p => PagePriorityFromLayoutID(p.Key))// sort by page index (descending)
            .GroupBy(p => p.First().Value["layout"]?["name"]?.ToString() ?? "Unknown")// group by page header
            .OrderBy(g => g.First().First().Value["layout"]?["index"]?.GetValue<int>())// sort by page header index
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

    static int PagePriorityFromLayoutID(string layoutID)
    {
        if (int.TryParse(layoutID.Split(".")[^1], out int parseResult))
            return -parseResult;
        else if (int.TryParse(layoutID[^2..], out int fallbackParseResult))
        {
            GD.Print("Layout Priority fallback: " + fallbackParseResult);
            return -fallbackParseResult;
        }
        return -100;
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
        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return null;

        GD.Print("retrieving catalog from epic...");
        JsonNode fullStorefront = await Helpers.MakeRequest(
                HttpMethod.Get,
                FnEndpoints.gameEndpoint,
                "fortnite/api/storefront/v2/catalog",
                "",
                account.AuthHeader
            );
        if (fullStorefront["errorCode"] is not null)
        {
            await GenericConfirmationWindow.ShowErrorForWebResult(fullStorefront.AsObject());
            return null;
        }
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
                ExternalEndpoints.cosmeticShopEndpoint,
                "v2/shop?responseFlags=4", // 1 = paths, 2 = gameplayTags, 4 = shop history
                "",
                null,
                addCosmeticHeader:true
            );
        if (cosmeticDisplayData["data"]?["entries"]?.AsArray().Count == 0)
            GD.Print(cosmeticDisplayData.ToString());
        return new(cosmeticDisplayData["data"]["entries"].AsArray()
            .Select(n => n.AsObject().CreateKVP("offerId")));
    }

    public static readonly string[] relevantStorefronts = new string[]
    {
        FnStorefrontTypes.XRayLlamaCatalog,
        FnStorefrontTypes.RandomLlamaCatalog,
        FnStorefrontTypes.WeeklyShopCatalog,
        FnStorefrontTypes.EventShopCatalog,
        FnStorefrontTypes.WeeklyCosmeticShopCatalog,
        FnStorefrontTypes.DailyCosmeticShopCatalog
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
    const string fnapiJamTrackPrefix = "https://cdn.fortnite-api.com/tracks/";
    const string cacheFolderPath = "user://cosmetic_images/";
    static readonly Dictionary<string, WeakRef> activeResourceCache = new();

    public static ImageTexture GetLocalCosmeticResource(string serverPath)
    {
        if (activeResourceCache.ContainsKey(serverPath) && activeResourceCache[serverPath]?.GetRef().Obj is ImageTexture cachedTexture)
        {
            //GD.Print("cache exists");
            return cachedTexture;
        }
        
        bool isJamTrack = serverPath.StartsWith(fnapiJamTrackPrefix);
        string localPath = cacheFolderPath +
            (
                isJamTrack ?
                    serverPath[fnapiJamTrackPrefix.Length..].Replace("/", "-") :
                    serverPath[fnapiLinkPrefix.Length..].Replace("/", "-")
            );

        if (!FileAccess.FileExists(localPath))
            return null;

        //GD.Print("file exists");
        Image resourceImage = new();
        using var imageFile = FileAccess.Open(localPath, FileAccess.ModeFlags.ReadWrite);
        var error = LoadImageWithCtx(resourceImage, imageFile.GetBuffer((long)imageFile.GetLength()), localPath);
        if (error != Error.Ok)
            return null;
        //GD.Print("file loaded");

        //make a fake modification to change the modified date when the file is disposed
        imageFile.SeekEnd(-1);
        byte temp = imageFile.Get8();
        imageFile.SeekEnd(-1);
        imageFile.Store8(temp);

        var imageTex = ImageTexture.CreateFromImage(resourceImage);
        activeResourceCache[serverPath] = GodotObject.WeakRef(imageTex);
        imageTex.ResourceName = serverPath;

        return imageTex;
    }

    public static async Task<ImageTexture> GetCosmeticResource(string serverPath)
    {
        var localImageTex = GetLocalCosmeticResource(serverPath);
        if(localImageTex is not null)
        {
            //GD.Print("using local");
            return localImageTex;
        }

        bool isJamTrack = serverPath.StartsWith(fnapiJamTrackPrefix);
        //if (isJamTrack)
        //{
        //    GD.Print("Interpreting as Jam Track");
        //    GD.Print("/tracks/" + serverPath[fnapiJamTrackPrefix.Length..]);
        //    GD.Print(ExternalEndpoints.jamTracksEndpoint);
        //}
        string localPath = cacheFolderPath + 
            ( 
                isJamTrack ?
                    serverPath[fnapiJamTrackPrefix.Length..].Replace("/", "-") : 
                    serverPath[fnapiLinkPrefix.Length..].Replace("/", "-")
            );

        using var request =
        new HttpRequestMessage(
            HttpMethod.Get,
            isJamTrack ? 
                "/tracks/" + serverPath[fnapiJamTrackPrefix.Length..] : 
                "/images/cosmetics/" + serverPath[fnapiLinkPrefix.Length..]
        );

        GD.Print($"Requesting cosmetic \"{serverPath}\"");
        using var result = await Helpers.MakeRequestRaw(isJamTrack ? ExternalEndpoints.jamTracksEndpoint : ExternalEndpoints.cosmeticShopEndpoint, request);
        if (!result.IsSuccessStatusCode)
        {
            GD.Print(result);
            return null;
        }
        //GD.Print("remote file exists");

        Image resourceImage = new();
        byte[] imageBuffer = await result.Content.ReadAsByteArrayAsync();
        var error = LoadImageWithCtx(resourceImage, imageBuffer, serverPath);
        if (error != Error.Ok)
            return null;
        //GD.Print("remote file loaded");

        if (!DirAccess.DirExistsAbsolute(cacheFolderPath))
            DirAccess.MakeDirAbsolute(cacheFolderPath);

        using (var imageFile = FileAccess.Open(localPath, FileAccess.ModeFlags.Write))
        {
            //GD.Print($"Caching cosmetic: \"{localPath}\"");
            imageFile.StoreBuffer(imageBuffer);
        }

        var imageTex = ImageTexture.CreateFromImage(resourceImage);
        activeResourceCache[serverPath] = GodotObject.WeakRef(imageTex);
        imageTex.ResourceName = serverPath;

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
            case "jpg":
                return image.LoadJpgFromBuffer(data);
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

public static class FnStorefrontTypes
{
    public const string XRayLlamaCatalog = "CardPackStorePreroll";
    public const string RandomLlamaCatalog = "CardPackStoreGameplay";
    public const string WeeklyShopCatalog = "STWRotationalEventStorefront";
    public const string EventShopCatalog = "STWSpecialEventStorefront";
    public const string WeeklyCosmeticShopCatalog = "BRWeeklyStorefront";
    public const string DailyCosmeticShopCatalog = "BRDailyStorefront";
}

public class GameStorefront
{
    #region Static Methods

    static Dictionary<RefreshTimeType, DateTime> expirationDates = new()
    {
        [RefreshTimeType.Hourly] = default,
        [RefreshTimeType.Daily] = default,
        [RefreshTimeType.Weekly] = default,
        [RefreshTimeType.Event] = default,
    };

    static Dictionary<string, JsonObject> storefrontCache;
    static Dictionary<string, GameStorefront> storefronts = new();
    public static bool RequiresUpdate(RefreshTimeType? refreshType)
    {
        return storefronts is null || refreshType is null || DateTime.UtcNow.CompareTo(expirationDates[refreshType.Value]) >= 0;
    }

    public static async Task<bool> UpdateCatalog(RefreshTimeType? refreshType = null)
    {
        if (!RequiresUpdate(refreshType))
            return true;

        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return false;

        GD.Print("retrieving catalog from epic...");
        var catalog = await Helpers.MakeRequest(
                HttpMethod.Get,
                FnEndpoints.gameEndpoint,
                "fortnite/api/storefront/v2/catalog",
                "",
                account.AuthHeader
            );
        if (catalog["errorCode"] is not null)
        {
            await GenericConfirmationWindow.ShowErrorForWebResult(catalog.AsObject());
            return false;
        }
        storefrontCache = catalog["storefronts"].AsArray().Select(n => n.AsObject()).ToDictionary(n => n["name"].ToString());

        List<string> toRemove = new();
        foreach (var kvp in storefronts)
        {
            if(!storefrontCache.ContainsKey(kvp.Key))
            {
                toRemove.Add(kvp.Key);
                continue;
            }
            kvp.Value.CheckForChanges(storefrontCache[kvp.Key]["catalogEntries"].AsArray());
        }
        foreach (var sfKey in toRemove)
        {
            GD.Print("disconnecting SF");
            storefronts[sfKey].DisconnectAll();
            storefronts.Remove(sfKey);
        }

        foreach (var refreshTypeKey in expirationDates.Keys)
        {
            expirationDates[refreshTypeKey] = RefreshTimerController.GetRefreshTime(refreshTypeKey);
        }

        return true;
    }

    static GameStorefront GetOrCreateStorefront(string storefrontKey, RefreshTimeType? refreshType = null)
    {
        if (storefronts.ContainsKey(storefrontKey))
            return storefronts[storefrontKey];

        if (!storefrontCache.ContainsKey(storefrontKey))
            return null;

        return storefronts[storefrontKey] = new(storefrontCache[storefrontKey], refreshType);
    }

    public static async Task<GameStorefront> GetStorefront(string storefrontKey, RefreshTimeType? refreshType = null)
    {
        if (!await UpdateCatalog(refreshType))
            return null;
        return GetOrCreateStorefront(storefrontKey, refreshType);
    }

    public static async Task<GameStorefront[]> GetStorefronts(params string[] storefrontKeys) => await GetStorefronts(RefreshTimeType.Hourly, storefrontKeys);
    public static async Task<GameStorefront[]> GetStorefronts(RefreshTimeType? refreshType, params string[] storefrontKeys)
    {
        if(!await UpdateCatalog(refreshType))
            return Array.Empty<GameStorefront>();
        return storefrontKeys.Select(sfKey => GetOrCreateStorefront(sfKey)).Where(sf => sf is not null).ToArray();
    }

    #endregion

    public event Action<GameOffer> OnOfferAdded;
    public event Action<GameOffer> OnOfferChanged;
    public event Action<GameOffer> OnOfferRemoved;

    RefreshTimeType linkedRefreshType;
    public bool isValid { get; private set; } = true;
    public string storefrontId { get; private set; }
    Dictionary<string, GameOffer> offers;

    public GameStorefront(JsonObject rawData, RefreshTimeType? linkedRefreshType = null)
    {
        storefrontId = rawData["name"].ToString();
        offers = rawData["catalogEntries"].AsArray().Select(n => new GameOffer(this, n.AsObject())).ToDictionary(offer => offer.OfferId);
        this.linkedRefreshType = linkedRefreshType ?? RefreshTimeType.Hourly;
    }

    public async Task Update(bool force = false) => await UpdateCatalog(force ? null : linkedRefreshType);

    void CheckForChanges(JsonArray catalogEntries)
    {
        var catalogEntriesDict = catalogEntries.Select(n => n.AsObject()).ToDictionary(n => n["offerId"].ToString());
        var oldOfferIds = offers.Keys.ToArray();
        var newOfferIds = catalogEntries.Select(n => n["offerId"].ToString()).ToArray();

        var addedOffers = newOfferIds.Except(oldOfferIds);
        var removedOffers = oldOfferIds.Except(newOfferIds);
        var possiblyChangedOffers = oldOfferIds.Intersect(newOfferIds);

        foreach (var offerId in removedOffers)
        {
            var offer = offers[offerId];
            offer.NotifyRemoving();
            offers.Remove(offerId);
            OnOfferRemoved?.Invoke(offer);
            offer.DisconnectFromStorefront();
        }
        foreach (var offerId in possiblyChangedOffers)
        {
            var offer = offers[offerId];
            var from = offer.rawData.ToString();
            var to = catalogEntriesDict[offerId].ToString();
            if (from != to)
            {
                offer.SetRawData(catalogEntriesDict[offerId]);
                offer.NotifyChanged();
                OnOfferChanged?.Invoke(offer);
            }
        }
        foreach (var offerId in addedOffers)
        {
            GameOffer offer = new(this, catalogEntriesDict[offerId]);
            offers[offerId] = offer;
            OnOfferAdded?.Invoke(offer);
        }
    }

    public void DisconnectAll()
    {
        isValid = false;

        foreach (var offer in offers?.Values)
        {
            offer.DisconnectFromStorefront();
        }

        offers.Clear();
    }

    public GameOffer this[string offerId] => offers[offerId];
    public GameOffer[] Offers => offers.Values.ToArray();

}

public class GameOffer
{
    public event Action<GameOffer> OnChanged;
    public event Action<GameOffer> OnRemoving;
    public event Action<GameOffer> OnRemoved;
    public GameOffer(GameStorefront storefront, JsonObject rawData)
    {
        this.storefront = storefront;
        SetRawData(rawData);
    }

    public void SetRawData(JsonObject rawData)
    {
        this.rawData = rawData;
        itemGrants = rawData["itemGrants"].AsArray().Select(n => new GameItem(null, null, n.AsObject())).ToArray();

        if (rawData["dynamicBundleInfo"] is JsonObject dynamicBundleInfo)
        {
            var priceTemplateId = dynamicBundleInfo["currencyType"].ToString() == "MtxCurrency" ? "Currency:mtxpurchased" : dynamicBundleInfo["currencySubType"].ToString();
            var priceTemplate = GameItemTemplate.Get(priceTemplateId);

            discountAmount = -dynamicBundleInfo["discountedBasePrice"].GetValue<int>();
            discountMin = dynamicBundleInfo["floorPrice"].GetValue<int>();
            var itemsArray = dynamicBundleInfo["bundleItems"].AsArray();
            int basePriceAmount = itemsArray.Select(n => n["regularPrice"].GetValue<int>()).Sum();

            conditionalDiscounts = new(
                    itemsArray
                        .Where(n => n["alreadyOwnedPriceReduction"].GetValue<int>() > 0)
                        .Select(n => new KeyValuePair<string, int>(n["item"]["templateId"].ToString(), n["alreadyOwnedPriceReduction"].GetValue<int>()))
                );

            basePrice = priceTemplate?.CreateInstance(basePriceAmount);
        }
        else if (rawData["prices"][0]?.AsObject() is JsonObject priceData)
        {
            var priceTemplateId = priceData["currencyType"].ToString() == "MtxCurrency" ? "Currency:mtxpurchased" : priceData["currencySubType"].ToString();
            var priceTemplate = GameItemTemplate.Get(priceTemplateId);
            int basePriceAmount = priceData["regularPrice"].GetValue<int>();
            conditionalDiscounts = null;
            discountAmount = basePriceAmount - priceData["finalPrice"].GetValue<int>();
            discountMin = 0;
            basePrice = priceTemplate?.CreateInstance(basePriceAmount);
        }
        ResetCachedData();
    }

    void ResetCachedData()
    {
        eventId = null;
        eventLimit = null;
        simultaniousLimit = null;
        price = null;
        personalPrice = null;
    }

    public GameStorefront storefront { get; private set; }
    public JsonObject rawData { get; private set; }
    public JsonNode this[string propertyName] => rawData[propertyName];

    public string OfferId => rawData["offerId"].ToString();
    public string GetMeta(string key) => rawData["meta"] is JsonObject meta ? 
        meta[key]?.ToString() : 
        rawData["metaInfo"]?.AsArray()
        .FirstOrDefault(val => val["key"].ToString() == key)?
        ["value"].ToString();

    public string Title => rawData["title"]?.ToString();
    public int DailyLimit => rawData["dailyLimit"]?.GetValue<int>() ?? -1;
    public int WeeklyLimit => rawData["weeklyLimit"]?.GetValue<int>() ?? -1;
    public int MonthlyLimit => rawData["monthlyLimit"]?.GetValue<int>() ?? -1;

    int? eventLimit;
    public int EventLimit => eventLimit ??= int.Parse(GetMeta("EventLimit") ?? "-1");

    string eventId;
    public string EventId => eventId ??= GetMeta("PurchaseLimitingEventId");

    Dictionary<string, int> GenerateRequirementList(string type) =>
        rawData["requirements"].AsArray()
        .Where(n => n["requirementType"]?.ToString() == type)
        .ToDictionary(
            n => n["requiredId"].ToString(),
            n => n["minQuantity"].GetValue<int>()
        );

    Dictionary<string, int> fulfillmentDenyList;
    public Dictionary<string, int> FulfillmentDenyList => fulfillmentDenyList ??= GenerateRequirementList("DenyOnFulfillment");

    Dictionary<string, int> fulfillmentRequireList;
    public Dictionary<string, int> FulfillmentRequireList => fulfillmentRequireList ??= GenerateRequirementList("RequireFulfillment");

    Dictionary<string, int> itemDenyList;
    public Dictionary<string, int> ItemDenyList => itemDenyList ??= GenerateRequirementList("DenyOnItemOwnership");

    int? simultaniousLimit;
    public int SimultaniousLimit => simultaniousLimit ??= int.Parse(GetMeta("MaxConcurrentPurchases") ?? "-1");

    GameItem basePrice;
    public GameItem BasePrice => basePrice;
    int discountAmount = 0;
    int discountMin = 0;
    public bool IsFree => discountMin == 0 && discountAmount >= basePrice.quantity;
    public bool IsDiscountBundle => conditionalDiscounts?.Count > 0;

    Dictionary<string, int> conditionalDiscounts;
    GameItem price;
    public GameItem Price => price ??= GetRegularPrice();
    GameItem personalPrice;

    public GameItem[] itemGrants { get; private set; }

    GameItem GetRegularPrice()
    {
        int price = basePrice.quantity;
        price -= discountAmount;
        price = Mathf.Max(price, discountMin);
        var newPriceItem = basePrice?.template?.CreateInstance(price);

        return newPriceItem;
    }

    public async Task<GameItem> GetPersonalPrice(bool forcePrice = false, bool forceCosmetics = false)
    {
        if (!forcePrice && personalPrice is not null)
            return personalPrice;

        int price = basePrice.quantity;
        price -= discountAmount;

        //if dynamic bundle, generate discount based on owned items
        var account = GameAccount.activeAccount;
        if (IsDiscountBundle && await account.Authenticate())
        {
            var cosmeticItems = await account.GetProfile(FnProfileTypes.CosmeticInventory).Query(forceCosmetics);
            foreach (var kvp in conditionalDiscounts)
            {
                if (cosmeticItems.GetTemplateItems(kvp.Key).Any())
                    price -= kvp.Value;
            }
        }

        price = Mathf.Max(price, discountMin);

        return personalPrice = basePrice?.template?.CreateInstance(price);
    }

    Dictionary<string, GameItem> prerollData = new();
    public async Task<GameItem> GetPrerollData(GameAccount account = null, bool force = false)
    {
        account ??= GameAccount.activeAccount;
        if (GetMeta("Preroll") != "True")
            return null;
        if (!force && prerollData.ContainsKey(account.accountId))
            return prerollData[account.accountId];
        if (!await account.Authenticate())
            return null;
        var prerollItems = (await account.GetProfile(FnProfileTypes.AccountItems).Query()).GetItems("PrerollData");
        return prerollData[account.accountId] = prerollItems.FirstOrDefault(item => item.attributes?["offerId"].ToString() == OfferId);
    }

    public void NotifyChanged()
    {
        ResetCachedData();
        OnChanged?.Invoke(this);
    }

    public void NotifyRemoving() => OnRemoving?.Invoke(this);
    public void DisconnectFromStorefront()
    {
        storefront = null;
        OnRemoved?.Invoke(this);
    }
}
