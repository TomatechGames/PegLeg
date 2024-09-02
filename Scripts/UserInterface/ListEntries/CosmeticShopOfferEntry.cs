using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

public partial class CosmeticShopOfferEntry : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void TypeChangedEventHandler(string type);
    [Signal]
    public delegate void TooltipChangedEventHandler(string tooltip);

    [Signal]
    public delegate void OwnedVisibilityEventHandler(bool visible);

    [Signal]
    public delegate void ReminingTimeVisibilityEventHandler(bool visible);

    [Signal]
    public delegate void BonusTextVisibilityEventHandler(bool visible);
    [Signal]
    public delegate void BonusTextChangedEventHandler(string name);

    [Signal]
    public delegate void PriceAmountEventHandler(string amount);
    [Signal]
    public delegate void PriceVisibilityEventHandler(bool visible);

    [Signal]
    public delegate void OldPriceAmountEventHandler(string amount);
    [Signal]
    public delegate void OldPriceVisibilityEventHandler(bool visible);

    [Signal]
    public delegate void LastSeenTextEventHandler(string amount);
    [Signal]
    public delegate void LastSeenVisibilityEventHandler(bool visible);
    [Signal]
    public delegate void LastSeenAlertVisibilityEventHandler(bool visible);
    [Signal]
    public delegate void AlmostAYearVisibilityEventHandler(bool visible);


    [Export]
    Label expiryTimerText;
    [Export]
    Control loadingCubes;
    [Export]
    float waitTimeToLoadResources = 0.5f;
    [Export]
    ShaderHook resourceTarget;
    [Export]
    Texture2D fallbackTexture;
    Timer resourceLoadTimer;

    CosmeticShopOfferData currentOfferData;

    public override void _Ready()
    {
        base._Ready();

        resourceLoadTimer = new() {WaitTime = waitTimeToLoadResources};
        AddChild(resourceLoadTimer);
        resourceLoadTimer.Timeout += StartResourceLoadSequence;

        //resourceRequest = new();
        //AddChild(resourceRequest);
        //resourceRequest.RequestCompleted += CompleteResourceLoadSequence;
        RefreshTimerController.OnSecondChanged += UpdateOutTimer;
    }

    public override void _ExitTree()
    {
        ClearCosmeticOfferData();
        RefreshTimerController.OnSecondChanged -= UpdateOutTimer;
    }

    DateTime? outDate;
    private void UpdateOutTimer()
    {
        if (!outDate.HasValue)
            return;
            var remainingTime = (outDate.Value - DateTime.UtcNow);
        if (remainingTime.TotalDays > 2)
        {
            expiryTimerText.Text = Mathf.Floor(remainingTime.TotalDays) + " Days";
        }
        else if (remainingTime.TotalHours > 10)
        {
            expiryTimerText.Text = Mathf.Floor(remainingTime.TotalHours) + " Hours";
        }
        else if (remainingTime.TotalHours > 1.1)
        {
            string timerText = Mathf.FloorToInt(remainingTime.TotalHours * 10).ToString();
            expiryTimerText.Text = $"{timerText[..^1]}.{timerText[^1]} Hours";
        }
        else
        {
            expiryTimerText.Text = Mathf.Floor(remainingTime.TotalMinutes) + " Mins";
        }
    }

    public void StartResourceLoadTimer() =>
        resourceLoadTimer.Start();

    public void CancelResourceLoadTimer() =>
        resourceLoadTimer.Stop();

    bool resourceLoadStarted;
    private async void StartResourceLoadSequence()
    {
        resourceLoadTimer.Stop();
        if (resourceLoadStarted)
            return;
        resourceLoadStarted = true;
        if(currentOfferData is not null)
        {
            if (!currentOfferData.resourceLoadComplete)
                ApplyOfferResource();
            return;
        }

        if (resourceUrl == null)
        {
            resourceTarget.SetShaderTexture(fallbackTexture, "Cosmetic");
            GD.PushWarning("No resource URL");
            loadingCubes.Visible = false;
            return;
        }
        var tex = await CatalogRequests.GetCosmeticResource(resourceUrl);
        if (tex is not null)
            ApplyResource(tex);
        else
        {
            resourceTarget.SetShaderTexture(fallbackTexture, "Cosmetic");
            GD.PushWarning("Image request failed");
            loadingCubes.Visible = false;
            resourceTarget.Visible = true;
        }
    }

    void ApplyResource() => 
        ApplyResource(null);
    void ApplyResource(Texture2D tex)
    {
        Vector2 shift = tex is null ? new(0.5f, 0.5f) : resourceShift;
        tex ??= fallbackTexture;
        resourceTarget.SetShaderTexture(tex, "Cosmetic");
        resourceTarget.SetShaderVector(shift, "ShiftDirection");
        resourceLoadStarted = true;
        loadingCubes.Visible = false;
        resourceTarget.Visible = true;
    }

    void ApplyOfferResource() => 
        ApplyResource(currentOfferData.resourceTex);

    void ApplyOfferOwnership()
    {
        EmitSignal(SignalName.PriceVisibility, !currentOfferData.isOwned);
        EmitSignal(SignalName.OldPriceVisibility, !currentOfferData.isOwned && currentOfferData.isDiscountBundle);
        EmitSignal(SignalName.OldPriceAmount, (currentOfferData.price - currentOfferData.discountAmount).ToString());
        EmitSignal(SignalName.OwnedVisibility, currentOfferData.isOwned);
    }

    public void SetCosmeticOfferData(CosmeticShopOfferData offerData)
    {
        ClearCosmeticOfferData();
        currentOfferData = offerData;
        currentOfferData.OnResourceLoaded += ApplyOfferResource;
        currentOfferData.OnOwnershipLoaded += ApplyOfferOwnership;

        outDate = offerData.outDate;
        UpdateOutTimer();

        EmitSignal(SignalName.NameChanged, offerData.displayName);
        EmitSignal(SignalName.TypeChanged, offerData.displayType);
        EmitSignal(SignalName.TooltipChanged, offerData.displayType);

        EmitSignal(SignalName.PriceAmount, offerData.price.ToString());
        EmitSignal(SignalName.PriceVisibility, true);
        EmitSignal(SignalName.OldPriceAmount, (offerData.price-offerData.discountAmount).ToString());
        EmitSignal(SignalName.OldPriceVisibility, offerData.isDiscountBundle);
        EmitSignal(SignalName.OwnedVisibility, false);
        if (offerData.ownershipLoadComplete)
            ApplyOfferOwnership();

        loadingCubes.Visible = true;
        resourceTarget.Visible = false;
        resourceShift = offerData.resourceShift;
        if (offerData.resourceLoadComplete)
            ApplyResource();

        EmitSignal(SignalName.BonusTextVisibility, !string.IsNullOrWhiteSpace(offerData.bonusText));
        EmitSignal(SignalName.BonusTextChanged, offerData.bonusText);

        EmitSignal(SignalName.LastSeenVisibility, offerData.lastSeenDaysAgo > 1);
        EmitSignal(SignalName.LastSeenText, $"{offerData.lastSeenDaysAgo}");
        EmitSignal(SignalName.LastSeenAlertVisibility, offerData.isOld);
        EmitSignal(SignalName.AlmostAYearVisibility, offerData.isVeryOld);
    }

    void ClearCosmeticOfferData()
    {

        if (currentOfferData is null)
            return;
        currentOfferData.OnResourceLoaded -= ApplyOfferResource;

        currentOfferData = null;
    }

    string resourceUrl = null;
    Vector2 resourceShift = new(0.5f, 0.5f);

    public string offerId { get; private set; }
    public List<string> itemTypes { get; private set; } = new();
    int cellWidth = 0;
    public void PopulateEntry(JsonObject entryData, Vector2 cellSize)
    {
        offerId = entryData["offerId"].ToString();
        cellWidth = (int)cellSize.X;

        int oldPrice = entryData["regularPrice"].GetValue<int>();
        int newPrice = entryData["finalPrice"].GetValue<int>();
        isDiscountBundle = newPrice != oldPrice;
        if (isDiscountBundle)
            discountAmount = oldPrice - newPrice;
        else
            discountAmount = 0;

        JsonArray allItems = entryData.MergeCosmeticItems();
        if (entryData["bundle"] is not null)
            PopulateAsBundle(entryData, allItems);
        else
            PopulateAsItem(entryData, allItems);

        foreach (var item in allItems)
        {
            string type = item["type"]["displayValue"].ToString();
            if (!itemTypes.Contains(type))
                itemTypes.Add(type);
        }

        outDate = DateTime.Parse(entryData["outDate"].ToString()).ToUniversalTime();
        var resourceMat = entryData["newDisplayAsset"]?["materialInstances"]?[0]?.AsObject();

        if (cellSize.X == 4)
            resourceShift = new Vector2(0.5f, 0.125f);
        else if (cellSize.X == 3)
            resourceShift = new Vector2(0.5f, 0f);
        else
            resourceShift = new Vector2(0.5f, 0f);

        if (resourceMat is null)
        {
            resourceUrl =
                entryData.GetFirstCosmeticItem()["images"]?["featured"]?.ToString() ??
                entryData.GetFirstCosmeticItem()["images"]?["large"]?.ToString() ??
                entryData.GetFirstCosmeticItem()["images"]?["small"]?.ToString() ??
                entryData.GetFirstCosmeticItem()["images"]?["icon"]?.ToString() ??
                entryData.GetFirstCosmeticItem()["images"]?["smallIcon"]?.ToString();
            resourceShift = new Vector2(0.5f, 0.5f);
        }
        else if (resourceMat["images"]?["CarTexture"] is not null)
        {
            resourceUrl =
                resourceMat["images"]?["CarTexture"]?.ToString();
            resourceShift = new Vector2(0.5f, 0.5f);
        }
        else
        {
            resourceUrl =
                resourceMat["images"]["Background"]?.ToString() ??
                resourceMat["images"]["OfferImage"]?.ToString();
        }

        if (resourceUrl is not null && CatalogRequests.GetLocalCosmeticResource(resourceUrl) is Texture2D tex)
            ApplyResource(tex);
    }

    public int discountAmount { get; private set; }
    public int lastSeenDaysAgo { get; private set; }
    public bool isRecentlyNew { get; private set; }
    public bool isAddedToday { get; private set; }
    public bool isLeavingSoon { get; private set; }
    public bool isDiscountBundle { get; private set; }
    public bool isOld { get; private set; }
    public bool isVeryOld { get; private set; }

    void PopulateAsItem(JsonObject entryData, JsonArray allItems)
    {
        JsonObject firstItem = allItems[0].AsObject();

        int totalItemCount = allItems.Count;
        string extraItemsText = null;
        string fullExtraItemsText = null;
        if (totalItemCount > 1)
        {
            extraItemsText = $" (+{totalItemCount - 1})";
            fullExtraItemsText = allItems
                .GroupBy(i => i["type"]?["displayValue"].ToString())
                .Select(g => g.Key + (g.Count() > 1 ? " x" + g.Count() : ""))
                .ToArray()
                .Join(", ");
        }

        string mainName = firstItem["name"]?.ToString();
        string mainType = firstItem["type"]?["displayValue"].ToString();
        Name = mainName;

        EmitSignal(SignalName.NameChanged, firstItem["name"].ToString());
        EmitSignal(SignalName.TypeChanged, mainType + (extraItemsText ?? ""));

        int oldPrice = entryData["regularPrice"].GetValue<int>();
        int newPrice = entryData["finalPrice"].GetValue<int>();

        EmitSignal(SignalName.PriceAmount, newPrice.ToString());
        EmitSignal(SignalName.OldPriceAmount, oldPrice.ToString());

        EmitSignal(SignalName.OwnedVisibility, false);
        EmitSignal(SignalName.OldPriceVisibility, newPrice < oldPrice);

        SetMetadata(firstItem, entryData);
        SetMetaVisuals();


        string tooltip = mainName + " - " + mainType;
        if (fullExtraItemsText is not null)
            tooltip += "\nContents include: " + fullExtraItemsText;

        EmitSignal(SignalName.TooltipChanged, tooltip);
    }

    void PopulateAsBundle(JsonObject entryData, JsonArray allItems)
    {
        string mainName = entryData["bundle"]["name"].ToString();

        EmitSignal(SignalName.NameChanged, mainName);
        EmitSignal(SignalName.TypeChanged, $"Bundle [{allItems.Count} items]");

        int oldPrice = entryData["regularPrice"].GetValue<int>();
        int newPrice = entryData["finalPrice"].GetValue<int>();

        EmitSignal(SignalName.PriceAmount, newPrice.ToString());
        EmitSignal(SignalName.OldPriceAmount, oldPrice.ToString());

        EmitSignal(SignalName.OwnedVisibility, false);
        EmitSignal(SignalName.OldPriceVisibility, newPrice < oldPrice);

        if (!isDiscountBundle)
        {
            JsonObject firstItem = allItems[0].AsObject();
            SetMetadata(firstItem, entryData);
        }
        SetMetaVisuals();

        string tooltip = mainName + " - Bundle";
        tooltip += "\nContents include: " + allItems
                .GroupBy(i => i["type"]?["displayValue"].ToString())
                .Select(g => g.Key + (g.Count() > 1 ? " x" + g.Count() : ""))
                .ToArray()
                .Join(", ");
        EmitSignal(SignalName.TooltipChanged, tooltip);
    }

    void SetMetadata(JsonObject firstItem, JsonObject entryData)
    {
        string firstAddedDateText = firstItem["shopHistory"]?[0]?.ToString();
        DateTime firstAddedDate = firstAddedDateText is not null ? DateTime.Parse(firstAddedDateText).ToUniversalTime() : DateTime.UtcNow.Date;
        DateTime inDate = DateTime.Parse(entryData["inDate"].ToString()).ToUniversalTime();
        DateTime outDate = DateTime.Parse(entryData["outDate"].ToString()).ToUniversalTime();
        DateTime? lastAddedDate = null;
        var shopHistory = firstItem["shopHistory"]?.AsArray();
        if (shopHistory is not null)
        {
            for (int i = shopHistory.Count - 1; i >= 0; i--)
            {
                DateTime shopDate = DateTime.Parse(shopHistory[i].ToString()).ToUniversalTime();
                if (shopDate.CompareTo(inDate) == -1)
                {
                    lastAddedDate = shopDate;
                    break;
                }
            }
        }

        lastSeenDaysAgo = lastAddedDate.HasValue ? (int)(DateTime.UtcNow.Date - lastAddedDate.Value).TotalDays : 0;
        isAddedToday = inDate == DateTime.UtcNow.Date && (lastSeenDaysAgo > 1 || DateTime.UtcNow.Date == firstAddedDate);
        isRecentlyNew = (DateTime.UtcNow.Date - firstAddedDate).TotalDays < 7;
        isLeavingSoon = (outDate - DateTime.UtcNow.Date).TotalHours < 24;
        isOld = lastSeenDaysAgo > 500;
        isVeryOld = lastSeenDaysAgo > 1000;


    }

    void SetMetaVisuals()
    {
        if (discountAmount > 0)
        {
            EmitSignal(SignalName.BonusTextChanged, cellWidth > 1 ? $"Save {discountAmount} VBucks!" : $"-{discountAmount}");
            EmitSignal(SignalName.BonusTextVisibility, true);
            EmitSignal(SignalName.LastSeenVisibility, false);
            EmitSignal(SignalName.AlmostAYearVisibility, false);
            return;
        }

        if (isRecentlyNew && isAddedToday)
        {
            //GD.Print($"New {firstAddedDate}");
            EmitSignal(SignalName.BonusTextChanged, "# NEW");
        }
        else if (isRecentlyNew)
        {
            //GD.Print($"Recent {firstAddedDate} ({DateTime.UtcNow.Date - firstAddedDate})");
            EmitSignal(SignalName.BonusTextChanged, "NEW");
        }
        else if (isAddedToday)
        {
            //GD.Print($"Back {lastAddedDate} ({DateTime.UtcNow.Date - firstAddedDate})");
            EmitSignal(SignalName.BonusTextChanged, $" # ");
        }

        EmitSignal(SignalName.BonusTextVisibility, isRecentlyNew || isAddedToday);
        EmitSignal(SignalName.LastSeenVisibility, lastSeenDaysAgo > 1);
        EmitSignal(SignalName.LastSeenText, $"{lastSeenDaysAgo}");
        EmitSignal(SignalName.LastSeenAlertVisibility, isOld);
        EmitSignal(SignalName.AlmostAYearVisibility, isVeryOld);
    }
}
