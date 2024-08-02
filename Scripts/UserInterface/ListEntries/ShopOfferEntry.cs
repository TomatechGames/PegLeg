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
    public delegate void AmountNeededEventHandler(bool isNeeded);
    [Signal]
    public delegate void IsFreeChangedEventHandler(bool isFree);
    [Signal]
    public delegate void IsLimitedTimeChangedEventHandler(bool isLimitedTime);
    [Signal]
    public delegate void IsBirthdayChangedEventHandler(bool isBirthday);
    [Signal]
    public delegate void PressedEventHandler(string linkedOfferId);

    [Export]
    GameItemEntry grantedItemEntry;
    [Export]
    GameItemEntry priceEntry;
    [Export]
    bool includeAmountInName = false;
    [Export]
    bool requireGreaterThanOne = false;
    [Export]
    Label temporaryTimerLabel;

    string linkedOfferId = "";

    public async void SetOffer(JsonObject shopOffer)
    {
        linkedOfferId = shopOffer["offerId"].ToString();
        string priceType = shopOffer["prices"][0]["currencySubType"].ToString();
        int price = shopOffer["prices"][0]["finalPrice"].GetValue<int>();
        int inInventory = await ProfileRequests.GetSumOfProfileItems(FnProfiles.AccountItems, priceType);


        var grantedItem = shopOffer["itemGrants"][0].AsObject();
        int stockLimit = await shopOffer.GetPurchaseLimitFromOffer();
        if (stockLimit == 999)
            stockLimit = -1;

        string name = grantedItem.GetTemplate()?["DisplayName"]?.ToString();
        if (includeAmountInName)
        {
            if (stockLimit > (requireGreaterThanOne ? 1 : 0))
                name += " (" + stockLimit + " left)";
            else if (stockLimit == 0)
                name += " (Sold Out)";
        }
        EmitSignal(SignalName.NameChanged, name);

        string amountText = "x" + stockLimit;
        if (stockLimit < 0)
            amountText = "";
        else if (stockLimit == 0)
            amountText = "Sold Out";
        var grantedQuantity = grantedItem["quantity"].GetValue<int>();
        if (grantedQuantity > 1 && stockLimit != 0)
            amountText = grantedQuantity + amountText;

        EmitSignal(SignalName.AmountNeeded, amountText!="");
        EmitSignal(SignalName.AmountChanged, amountText);

        grantedItem["shopQuantity"] = stockLimit;

        if (grantedItem["templateId"].ToString().StartsWith("CardPack"))
        {
            int tier = shopOffer["prerollData"]?["attributes"]?["highest_rarity"]?.GetValue<int>() ?? 0;
            if (grantedItem.GetTemplate()?["Rarity"]?.ToString() == "Epic")
                tier = Mathf.Max(1, tier);
            if (grantedItem.GetTemplate()?["Rarity"]?.ToString() == "Legendary")
                tier = 2;
            grantedItem["template"]["Rarity"] = tier switch
            {
                1=>"Epic",
                2=>"Legendary",
                _=>"Rare"
            };
        }

        BanjoAssets.TryGetTemplate(priceType, out var priceTemplate);
        priceTemplate = priceTemplate.Reserialise();
        priceTemplate["Description"] = inInventory + "/" + price;

        EmitSignal(SignalName.IsBirthdayChanged, name.ToLower().Contains("birthday"));

        EmitSignal(SignalName.IsFreeChanged, price == 0);
        skipHourTimer = linkedOfferId != "B9B0CE758A5049F898773C1A47A69ED4";//offerId of random free llamas which only last 1 hour
        EmitSignal(SignalName.IsLimitedTimeChanged, !skipHourTimer);

        if (price == 0)
            priceEntry.ClearItem(null);
        else
            priceEntry.SetItemData(priceTemplate.CreateInstanceOfItem(price));
        grantedItemEntry.SetItemData(grantedItem);
    }

    public void EmitPressedSignal()
    {
        EmitSignal(SignalName.Pressed, linkedOfferId);
    }

    bool skipHourTimer = true;
    public override void _Process(double delta)
    {
        if (skipHourTimer || Engine.GetProcessFrames()%30!=1)
            return;

        DateTime nextHourTime = DateTime.Now.AddHours(1);
        nextHourTime = nextHourTime.AddSeconds(-nextHourTime.Second).AddMinutes(-nextHourTime.Minute);
        var nextHourTimeSpan = nextHourTime - DateTime.Now;

        if (nextHourTimeSpan.TotalMinutes < 1)
            temporaryTimerLabel.SelfModulate = Colors.Red;
        else if (nextHourTimeSpan.TotalMinutes < 15)
            temporaryTimerLabel.SelfModulate = Colors.Orange;
        else
            temporaryTimerLabel.SelfModulate = Colors.White;

        temporaryTimerLabel.Text = nextHourTimeSpan.FormatTime();
    }
}
