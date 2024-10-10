using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class CosmeticShopInterface : Control
{
    [Export]
    Button sacButton;
    [Export]
    SplitContainer splitContainer;
    [Export]
    ScrollContainer verticalScrollBox;
    [Export]
    Control navContainer;
    [Export]
    Tree navigationPane;
    [Export]
    Control pageParent;
    [Export]
    Control simpleShopParent;
    [ExportGroup("Scenes")]
    [Export]
    PackedScene shopHeaderScene;
    [Export]
    PackedScene shopRowScene;
    [Export]
    PackedScene shopEntryScene;
    [ExportGroup("Filter Bar")]
    [Export]
    Control filterBlocker;
    [Export]
    CheckButton simpleShopMode;
    [Export]
    int simpleOpsPerFrame = 30;
    [Export]
    CheckButton requireLeavingSoon;
    [Export]
    CheckButton requireAddedToday;
    [Export]
    CheckButton includeDiscountBundles;
    [Export(PropertyHint.ArrayType)]
    CheckButton[] newOrOldFilters = System.Array.Empty<CheckButton>();
    [Export(PropertyHint.ArrayType)]
    CheckButton[] typeFilters = System.Array.Empty<CheckButton>();
    [Export]
    Button resetTypeFilters;

    public override void _Ready()
    {
        VisibilityChanged += async () =>
        {
            if (IsVisibleInTree())
            {
                //load shop
                await LoadShop();
                CurrencyHighlight.Instance.SetCurrencyType("AccountResource:eventcurrency_scaling");
            }
        };

        RefreshTimerController.OnHourChanged += OnHourChanged;
        navigationPane.ButtonClicked += OnNavButton;
        navigationPane.CellSelected += OnNavCell;
        sacButton.Pressed += OpenSACPrompt;

        requireAddedToday.Pressed += ApplyFilters;
        requireLeavingSoon.Pressed += ApplyFilters;
        includeDiscountBundles.Pressed += ApplyFilters;
        foreach (var button in newOrOldFilters)
        {
            button.Pressed += ApplyFilters;
        }
        foreach (var button in typeFilters)
        {
            button.Pressed += ApplyFilters;
        }
        //simpleShopMode.ButtonPressed = AppConfig.Get("item_shop", "simple_cosmetics", false);
        simpleShopMode.Pressed += async () =>
        {
           // AppConfig.Set("item_shop", "simple_cosmetics", simpleShopMode.ButtonPressed);
            await LoadShop(true);
        };
        resetTypeFilters.Pressed += () =>
        {
            foreach (var item in typeFilters)
            {
                item.ButtonPressed = false;
            }
            ApplyFilters();
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Visible && (Engine.GetPhysicsFrames() % 5) == 1)//only do this every 5 physics ticks
            UpdateShopOfferResourceLoading();
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnHourChanged -= OnHourChanged;
    }

    private async void OnHourChanged()
    {
        if (IsVisibleInTree())
            await LoadShop();
    }

    async void OpenSACPrompt()
    {
        string subtext = GD.Randf() > 0.85f ?
            "Your selected code normally disappears after 2 weeks, but PegLeg can automatically re-select the code on launch!" :
            "Whoever you choose to support will recieve 5% of the cost of any Real-Money or VBuck purchases you make";
        var response = await GenericLineEditWindow.OpenLineEdit("Support A Creator!", subtext, sacButton.Text, "Who do you want to support?");
        if (response is null)
            return;

        LoadingOverlay.AddLoadingKey("setSAC");
        await ProfileRequests.SetSACCode(response);
        sacButton.Text = await ProfileRequests.IsSACExpired() ? "None" : (await ProfileRequests.GetSACCode());
        LoadingOverlay.RemoveLoadingKey("setSAC");
    }

    private void OnNavCell()
    {
        TreeItem item = navigationPane.GetSelected();
        if (item.GetChildCount() > 0 && item.GetParent() is not null)
        {
            item.Collapsed = !item.Collapsed;
            if (!item.Collapsed)
            {
                navigationPane.CallDeferred("set_selected", item.GetFirstChild(), 0);
            }
            else
            {
                navigationPane.CallDeferred("set_selected", navigationPane.GetRoot(), 0);
            }
            //navigationPane.SetSelected(item.GetFirstChild(), 0);
            return;
        }
        Control metaControl = (Control)item.GetMetadata(0);
        if (item.GetParent() is null)
            return;
        int scrollLevel = (int)metaControl.Position.Y;
        var scrollTween = GetTree().CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        scrollTween.TweenProperty(verticalScrollBox, "scroll_vertical", scrollLevel, 0.3f);
    }

    private void OnNavButton(TreeItem item, long column, long id, long mouseButtonIndex)
    {
        int scrollLevel = (int)((Control)item.GetMetadata(1)).Position.Y;
        var scrollTween = GetTree().CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        scrollTween.TweenProperty(verticalScrollBox, "scroll_vertical", scrollLevel, 0.3f);
    }


    private void UpdateShopOfferResourceLoading()
    {
        var compareRect = verticalScrollBox.GetGlobalRect();
        compareRect.Size *= new Vector2(1.5f,3);
        foreach (var item in activeOffers)
        {
            if (item.GetGlobalRect().Intersects(compareRect))
            {
                if (!onScreenOffers.Contains(item))
                {
                    onScreenOffers.Add(item);
                    item.StartResourceLoadTimer();
                }
            }
            else
            {
                if (onScreenOffers.Contains(item))
                {
                    onScreenOffers.Remove(item);
                    item.CancelResourceLoadTimer();
                }
            }
        }
    }

    public void RegisterOffer(CosmeticShopOfferEntry newOffer) => activeOffers.Add(newOffer);

    bool isLoadingShop = false;
    List<CosmeticShopOfferEntry> activeOffers = new();
    List<CosmeticShopOfferEntry> onScreenOffers = new();
    List<PageGrouping> activePages = new();
    record PageGrouping(Control pageHeader, List<CosmeticShopRow> pageRows);

    public async Task LoadShop(bool force = false)
    {
        if (isLoadingShop || !(CatalogRequests.StorefrontRequiresUpdate() || force || activeOffers.Count == 0) || !await LoginRequests.TryLogin())
            return;

        string currentSACCode = await ProfileRequests.GetSACCode(false);
        if (await ProfileRequests.GetSACTime() > 1 && AppConfig.Get("automation", "creatorcode", false))
        {
            //GD.Print(currentSACCode);
            await ProfileRequests.SetSACCode(currentSACCode);
        }
        sacButton.Text = await ProfileRequests.IsSACExpired() ? "None" : currentSACCode;

        isLoadingShop = true;
        filterBlocker.Visible = true;

        activeOffers.Clear();
        onScreenOffers.Clear();
        activePages.Clear();
        navigationPane.Clear();

        var cosmeticShop = await CatalogRequests.GetCosmeticShop();

        foreach (var pageChild in pageParent.GetChildren())
        {
            pageChild.QueueFree();
        }
        foreach (var entryChild in simpleShopParent.GetChildren())
        {
            entryChild.QueueFree();
        }
        await this.WaitForFrame();

        PrepareFilters();

        if (simpleShopMode.ButtonPressed)
        {
            await GenerateSimpleShop(cosmeticShop);
        }
        else
        {
            await GenerateComplexShop(cosmeticShop);
        }

        await this.WaitForFrame();
        await this.WaitForFrame();
        CatalogRequests.CleanCosmeticResourceCache();

        isLoadingShop = false;
        filterBlocker.Visible = false;
    }

    
    async Task GenerateSimpleShop(JsonObject cosmeticShop)
    {
        navContainer.Visible = false;
        splitContainer.Collapsed = true;
        int opCount = 0;
        foreach (var category in cosmeticShop)
        {
            foreach (var section in category.Value.AsObject())
            {
                foreach (var page in section.Value.AsArray())
                {
                    foreach (var entryData in page.AsObject())
                    {
                        var entry = shopEntryScene.Instantiate<CosmeticShopOfferEntry>();
                        entry.CustomMinimumSize = new(150, 150);
                        entry.PopulateEntry(entryData.Value.AsObject(), Vector2.One);
                        simpleShopParent.AddChild(entry);
                        RegisterOffer(entry);
                        entry.Visible = IsValidEntry(entry);
                        if (opCount > simpleOpsPerFrame)
                        {
                            UpdateShopOfferResourceLoading();
                            await this.WaitForFrame();
                            opCount = 0;
                        }
                        opCount++;
                    }
                }
            }
        }
    }

    async Task GenerateComplexShop(JsonObject cosmeticShop)
    {
        navContainer.Visible = true;
        splitContainer.Collapsed = false;
        var navRoot = navigationPane.CreateItem();
        List<CosmeticShopRow> rowsToPopulate = new();
        foreach (var category in cosmeticShop)
        {
            var navCat = navRoot;
            if (category.Key != "Uncategorised")
            {
                navCat = navRoot.CreateChild();
                navCat.SetText(0, category.Key);
                navCat.Collapsed = true;
            }
            Control firstHeader = null;
            foreach (var section in category.Value.AsObject())
            {
                //spawn shop header
                var navSection = navCat.CreateChild();
                navSection.SetText(0, section.Key);
                var header = shopHeaderScene.Instantiate<Control>();
                firstHeader ??= header;
                pageParent.AddChild(header);
                if (header.FindChild("HeaderLabel", true) is Label headerLabel)
                    headerLabel.Text = section.Key;
                //navSec.AddButton(1, navButtonTexture);
                navSection.SetMetadata(0, header);
                List<CosmeticShopRow> pageRows = new();
                //spawn shop pages
                foreach (var page in section.Value.AsArray())
                {
                    var row = shopRowScene.Instantiate<CosmeticShopRow>();
                    row.PageData = page.AsObject();
                    pageParent.AddChild(row);
                    rowsToPopulate.Add(row);
                    pageRows.Add(row);
                }
                activePages.Add(new(header, pageRows));
            }
            if (firstHeader is not null)
            {
                navCat.SetMetadata(0, firstHeader);
            }
        }
        await this.WaitForFrame();
        foreach (var page in activePages)
        {
            page.pageHeader.Visible = false;
            foreach (var row in page.pageRows)
            {
                await row.PopulatePage(this);
                UpdateShopOfferResourceLoading();
                if (row.FilterPage(IsValidEntry))
                    page.pageHeader.Visible = true;
                await this.WaitForFrame();
            }
        }
    }
    void PrepareFilters()
    {
        newOrOldFilterValue = 2;
        for (int i = 0; i < newOrOldFilters.Length; i++)
        {
            if (newOrOldFilters[i].ButtonPressed)
            {
                newOrOldFilterValue = i;
                break;
            }
        }
        typeMasks = new bool[Mathf.Max(9, typeFilters.Length)];
        for (int i = 0; i < typeFilters.Length; i++)
        {
            typeMasks[i] = typeFilters[i].ButtonPressed;
        }
    }
    void ApplyFilters()
    {
        if (isLoadingShop)
            return;

        PrepareFilters();
        resetTypeFilters.Disabled = typeFilters.All(b => !b.ButtonPressed);
        if (simpleShopMode.ButtonPressed)
        {
            foreach (var entry in activeOffers)
            {
                entry.Visible = IsValidEntry(entry);
            }
        }
        else
        {
            //TODO: apply filters to nav panel
            foreach (var page in activePages)
            {
                page.pageHeader.Visible = false;
                foreach (var row in page.pageRows)
                {
                    if (row.FilterPage(IsValidEntry))
                        page.pageHeader.Visible = true;
                }
            }
        }
    }
    int newOrOldFilterValue = 0;
    bool[] typeMasks;
    static readonly string[] filterTypes = new string[]
    {
        "Outfit",
        "Character",
        "Backpack",
        "Back Bling",
        "Pickaxe",
        "Glider",
        "Contrail",
        "Emote",
        "Wrap",
        "Music Pack",
        "Jam Track",
        "Guitar",
        "Microphone",
        "Drums",
        "Bass",
        "Keytar",
        "Wheels",
        "Car Body",
        "Body",
        "Skin",
        "Decal",
        "Boost",
        "Trail",
    };

    static bool MatchAnyFilterIndex(List<string> toCheck, int startIndex, int length = 1)
    {
        for (int i = startIndex; i < startIndex+length; i++)
        {
            if (toCheck.Contains(filterTypes[i]))
                return true;
        }
        return false;
    }

    static bool MatchAnyFilter(List<string> toCheck)
    {
        foreach (var filter in filterTypes)
        {
            if (toCheck.Contains(filter))
                return true;
        }
        return false;
    }

    bool IsValidEntry(CosmeticShopOfferEntry entry)
    {
        if(requireLeavingSoon.ButtonPressed && !entry.metadata.isLeavingSoon)
            return false;
        if (requireAddedToday.ButtonPressed && !entry.metadata.isAddedToday)
            return false;
        if (!includeDiscountBundles.ButtonPressed && entry.isDiscountBundle)
            return false;

        if (!(newOrOldFilterValue switch
        {
            0 => entry.metadata.isAddedToday && entry.metadata.isRecentlyNew,
            1 => entry.metadata.isRecentlyNew,
            3=> entry.metadata.isOld,
            4 => entry.metadata.isVeryOld,
            _ => true
        }))
            return false;

        var types = entry.itemTypes;


        if (typeMasks[0] && MatchAnyFilterIndex(types, 0, 4))
            return true;
        if (typeMasks[1] && MatchAnyFilterIndex(types, 4))
            return true;
        if (typeMasks[2] && MatchAnyFilterIndex(types, 5, 2))
            return true;
        if (typeMasks[3] && MatchAnyFilterIndex(types, 7))
            return true;
        if (typeMasks[4] && MatchAnyFilterIndex(types, 8))
            return true;
        if (typeMasks[5] && MatchAnyFilterIndex(types, 9, 2))
            return true;
        if (typeMasks[6] && MatchAnyFilterIndex(types, 11, 5))
            return true;
        if (typeMasks[7] && MatchAnyFilterIndex(types, 16, 7))
            return true;
        if (typeMasks[8] && !MatchAnyFilter(types))
            return true;

        if (typeMasks.Any(m => m))
            return false;

        return true;
    }

}
public class CosmeticShopOfferData
{
    JsonObject entryData;
    JsonObject dynBundleInfo;
    string resourceUrl = null;

    public string offerId { get; private set; }
    public List<string> itemTypes { get; private set; } = new();
    public string displayName { get; private set; }
    public string displayType { get; private set; }
    public string tooltip { get; private set; }
    public string bonusText { get; private set; }
    public int price { get; private set; }
    public int lastSeenDaysAgo { get; private set; }
    public bool isRecentlyNew { get; private set; }
    public bool isAddedToday { get; private set; }
    public bool isLeavingSoon { get; private set; }
    public bool isOld { get; private set; }
    public bool isVeryOld { get; private set; }
    public bool isDiscountBundle { get; private set; }
    public DateTime outDate { get; private set; }
    public int cellWidth { get; private set; }
    public Vector2 resourceShift { get; private set; } = new(0.5f, 0.5f);
    public bool isOwned { get; private set; }
    public int discountAmount { get; private set; }
    public Texture2D resourceTex { get; private set; }

    public CosmeticShopOfferData(JsonObject entryData, Vector2 cellSize)
    {
        this.entryData = entryData;
        offerId = entryData["offerId"].ToString();
        outDate = DateTime.Parse(entryData["outDate"].ToString()).ToUniversalTime();
        cellWidth = (int)cellSize.X;
        dynBundleInfo = entryData["dynamicBundleInfo"]?.AsObject();

        price = entryData["regularPrice"].GetValue<int>();
        isDiscountBundle = entryData["finalPrice"].GetValue<int>() != price;

        JsonArray allItems = entryData.MergeCosmeticItems();
        JsonObject firstItem = allItems[0].AsObject();
        displayName =
            entryData["bundle"]?["name"].ToString() ??
            firstItem["name"]?.ToString();

        displayType = isDiscountBundle ?
            $"Bundle [{allItems.Count} items]" :
            (firstItem["type"]?["displayValue"].ToString() + (allItems.Count > 1 ? $" (+{allItems.Count - 1})" : ""));

        string mainType = entryData["bundle"] is not null ?
            "Bundle" :
            firstItem["type"]?["displayValue"].ToString();

        foreach (var item in allItems)
        {
            string type = item["type"]["displayValue"].ToString();
            if (!itemTypes.Contains(type))
                itemTypes.Add(type);
        }

        tooltip = $"{displayName} - {mainType}";
        if (allItems.Count > 1)
        {
            tooltip += "\nContents include: " + allItems
                .GroupBy(i => i["type"]?["displayValue"].ToString())
                .Select(g => g.Key + (g.Count() > 1 ? " x" + g.Count() : ""))
                .ToArray()
                .Join(", ");
        }

        if (cellSize.X == 4)
            resourceShift = new Vector2(0.5f, 0.125f);
        else if (cellSize.X == 3)
            resourceShift = new Vector2(0.5f, 0f);
        else
            resourceShift = new Vector2(0.5f, 0f);

        var resourceRender = entryData["newDisplayAsset"]?["renderImages"]?[0]?.AsObject();
        var resourceMat = entryData["newDisplayAsset"]?["materialInstances"]?[0]?.AsObject();
        if (resourceRender is not null)
        {
            resourceUrl =
                resourceRender["image"]?.ToString();
        }
        else if (resourceMat["images"]?["CarTexture"] is not null)
        {
            resourceUrl =
                resourceMat["images"]?["CarTexture"]?.ToString();
            resourceShift = new Vector2(0.5f, 0.5f);
        }
        else if(resourceMat is not null)
        {
            resourceUrl =
                resourceMat["images"]["Background"]?.ToString() ??
                resourceMat["images"]["OfferImage"]?.ToString();
        }
        else
        {
            resourceUrl =
                firstItem["images"]?["featured"]?.ToString() ??
                firstItem["images"]?["large"]?.ToString() ??
                firstItem["images"]?["small"]?.ToString() ??
                firstItem["images"]?["icon"]?.ToString() ??
                firstItem["images"]?["smallIcon"]?.ToString();
            resourceShift = new Vector2(0.5f, 0.5f);
        }

        //TODO: handle background colors

        if (resourceUrl is not null && CatalogRequests.GetLocalCosmeticResource(resourceUrl) is Texture2D tex)
        {
            resourceTex = tex;
            resourceLoadStarted = true;
            resourceLoadComplete = true;
        }

        discountAmount = -(dynBundleInfo?["discountedBasePrice"]?.GetValue<int>() ?? 0);
        if (!isDiscountBundle)
        {
            string firstAddedDateText = firstItem["shopHistory"]?[0]?.ToString();
            DateTime firstAddedDate = firstAddedDateText is not null ? DateTime.Parse(firstAddedDateText).ToUniversalTime() : DateTime.UtcNow.Date;
            DateTime inDate = DateTime.Parse(entryData["inDate"].ToString()).ToUniversalTime();
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

            if (isRecentlyNew && isAddedToday)
                bonusText = " # NEW";
            else if (isRecentlyNew)
                bonusText = "NEW";
            else if (isAddedToday)
                bonusText = " # ";
        }
        else
        {
            bonusText = cellWidth > 1 ? $"Save {discountAmount} VBucks!" : $"-{discountAmount}";
        }
    }

    bool ownershipLoadStarted;
    public bool ownershipLoadComplete { get; private set; }
    public event Action OnOwnershipLoaded;
    public async void LoadOwnership()
    {
        if (ownershipLoadStarted)
        {
            if (ownershipLoadComplete)
                OnOwnershipLoaded?.Invoke();
            return;
        }
        ownershipLoadStarted = true;
        if (isDiscountBundle)
        {
            //TODO: determine discount amount based on dynamic bundle data
            discountAmount = price - entryData["finalPrice"].GetValue<int>();
        }
        isOwned = false;
        OnOwnershipLoaded?.Invoke();
        ownershipLoadComplete = true;
    }

    bool resourceLoadStarted;
    public bool resourceLoadComplete { get; private set; }
    public event Action OnResourceLoaded;
    public async void LoadResource()
    {
        if (resourceLoadStarted)
        {
            if (resourceLoadComplete)
                OnResourceLoaded?.Invoke();
            return;
        }
        resourceLoadStarted = true;

        if (resourceUrl != null)
        {
            var tex = await CatalogRequests.GetCosmeticResource(resourceUrl);
            if (tex is not null)
                resourceTex = tex;
        }

        resourceLoadComplete = true;

        OnResourceLoaded?.Invoke();
    }
}
