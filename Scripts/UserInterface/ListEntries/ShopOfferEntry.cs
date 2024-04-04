using Godot;
using System;
using System.Text.Json.Nodes;
using System.Xml.Linq;

public partial class ShopOfferEntry : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void AmountChangedEventHandler(string amount);
    [Signal]
    public delegate void IsFreeChangedEventHandler(bool isFree);
    [Signal]
    public delegate void LlamaColorChangedEventHandler(Color tierColor);
    [Signal]
    public delegate void PressedEventHandler();

    [Export]
    GameItemEntry grantedItemEntry;
    [Export]
    GameItemEntry priceEntry;
    [Export]
    bool includeAmountInName = false;

    public string linkedPressData;

    public static readonly Color[] llamaTierColors = new Color[]
    {
        Color.FromString("#0064ff", Colors.White),
        Color.FromString("#999999", Colors.White),
        Color.FromString("#bfbf00", Colors.White),
    };

    public async void SetOffer(JsonObject shopOffer)
    {
        string priceType = shopOffer["prices"][0]["currencySubType"].ToString();
        int price = shopOffer["prices"][0]["finalPrice"].GetValue<int>();
        int inInventory = await ProfileRequests.GetSumOfProfileItems(FnProfiles.AccountItems, priceType);


        var grantedItem = shopOffer["itemGrants"][0].AsObject();
        int stockLimit = await shopOffer.GetPurchaseLimitFromOffer();
        if (stockLimit == 999)
            stockLimit = -1;

        string name = grantedItem.GetTemplate()?["ItemName"]?.ToString();
        if (stockLimit > 0)
            name += " (" + stockLimit + " left)";
        else if (stockLimit == 0)
            name += " (Sold Out)";
        EmitSignal(SignalName.NameChanged, name);

        string amountText = "x" + stockLimit;
        if (stockLimit < 0)
            amountText = "";
        else if (stockLimit == 0)
            amountText = "Sold Out";
        EmitSignal(SignalName.AmountChanged, amountText);

        //todo: move tier color logic into cardPackEntry, and set rarity of grantedItem to its tier if its a cardpack
        int tier = shopOffer["prerollData"]?["attributes"]?["highest_rarity"]?.GetValue<int>() ?? 0;
        if (grantedItem.GetTemplate()?["Rarity"]?.ToString() == "Legendary")
            tier = 2;
        EmitSignal(SignalName.LlamaColorChanged, llamaTierColors[tier]);

        BanjoAssets.TryGetTemplate(priceType, out var priceTemplate);
        priceTemplate = priceTemplate.Reserialise();
        priceTemplate["ItemDescription"] = inInventory + "/" + price;
        if (price == 0)
            priceTemplate["ImagePaths"] = null;

        EmitSignal(SignalName.IsFreeChanged, price == 0);


        priceEntry.SetItemData(priceTemplate.CreateInstanceOfItem(price));
        grantedItemEntry.SetItemData(new(grantedItem));
    }
}
