using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

static class CosmeticRequests
{
    const string fnapiLinkPrefix = "https://fortnite-api.com/images/cosmetics/";
    const string fnapiJamTrackPrefix = "https://cdn.fortnite-api.com/tracks/";
    const string fnCentralLinkPrefix = "";

    const string imageCacheFolderPath = "user://cosmetic_images/";
    const string templateCacheFolderPath = "user://cosmetic_templates/";

    static readonly Dictionary<string, WeakRef> activeResourceCache = [];

    public static bool HasLocalCosmeticImage()
    {
        return false;
    }
    public static async Task<Texture2D> GetCosmeticImage(string imagePath)
    {
        return null;
    }

    public static async Task<GameItemTemplate> GetCosmeticTemplate(string templateId)
    {
        if (GameItemTemplate.Get(templateId) is GameItemTemplate cachedTemplate)
            return cachedTemplate;
        return null;
    }

    public static async Task LoadShopAssets()
    {
        JsonNode cosmeticDisplayData = await Helpers.MakeRequest(
                HttpMethod.Get,
                ExternalWebAddresses.fnApi,
                "v2/shop?responseFlags=4", // 1 = paths, 2 = gameplayTags, 4 = shop history
                "",
                null,
                addCosmeticHeader: true
            );
        var entries = cosmeticDisplayData["data"]?["entries"]?.AsArray();
        if ((entries?.Count ?? 0) > 0)
            return;

        foreach (var offer in entries)
        {
            var allCosmetics = offer.AsObject().GetAllCosmetics();

            var displayAssetId = $"DisplayAsset:{offer["newDisplayAssetPath"]}";
            var displayAssetTemplate = GameItemTemplate.GetOrCreate(displayAssetId, () =>
            {
                string imagePath = null;
                JsonObject colors = null;
                if(offer["newDisplayAsset"]?["materialAsset"] is JsonArray materials)
                {
                    var mainMat =
                        materials.FirstOrDefault(m => m["productTag"].ToString() != "Product.Juno") ??
                        materials.FirstOrDefault();
                    if(mainMat is not null)
                    {
                        imagePath = mainMat["images"]?["OfferImage"]?.ToString();
                        colors = mainMat["colors"]?.AsObject().SafeDeepClone();
                    }
                }
                else if (offer["newDisplayAsset"]?["renderImages"] is JsonArray renderImages)
                {
                    var mainImage =
                        renderImages.FirstOrDefault(r => r["productTag"].ToString() != "Product.Juno") ??
                        renderImages.FirstOrDefault();
                    imagePath = mainImage["image"]?.ToString();
                }
                else if(allCosmetics.FirstOrDefault() is JsonObject firstItem)
                {
                    imagePath = firstItem["images"]?["large"]?.ToString() ?? firstItem["albumArt"]?.ToString();
                }
                GameItemTemplate newTemplate = new(
                        displayAssetId,
                        offer["devName"].ToString()
                    );
                if (imagePath is not null)
                    newTemplate.rawData["OfferImage"] = imagePath;
                if (colors is not null)
                    newTemplate.rawData["colors"] = colors;

                return newTemplate;
            });
            foreach (var item in allCosmetics)
            {
                if (item.ContainsKey("title"))
                    item["type"] = new JsonObject()
                    {
                        ["backendValue"]= "SparksSong",
                        ["displayValue"] = "Jam Track"
                    };
                var cosmeticTemplateId = $"{item["type"]["backendValue"]}:{item["id"].ToString().ToLower()}";
                var cosmeticTemplate = GameItemTemplate.GetOrCreate(cosmeticTemplateId, () =>
                {
                    return new(
                            cosmeticTemplateId,
                            item["name"]?.ToString() ?? item["title"]?.ToString(),
                            item["description"]?.ToString() ?? (
                                item["artist"]?.ToString() is string artist &&
                                item["album"]?.ToString() is string album &&
                                item["releaseYear"]?.GetValue<int>() is int releaseYear ?
                                $"{artist}\n{album}\n{releaseYear}" : null
                            ),
                            extraData: new()
                            {
                                ["displayType"] = item["type"]["displayValue"].ToString(),
                                ["shopHistory"] = item["shopHistory"].SafeDeepClone()
                            }
                        );
                });
            }
        }
    }

    static async Task<JsonObject> GetJamTrackData()
    {
        return null;
    }
    static async Task<JsonObject> GetShopSectionData()
    {
        return null;
    }

    public static async Task<GameItemTemplate> GetDisplayAssetTemplate(this GameOffer offer)
    {
        return null;
    }

    public static async Task<Texture2D> GetOfferImage(this GameItemTemplate displayAsset)
    {
        return null;
    }
}
