using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class CosmeticShopInterface : Control
{
    [Export]
    Tree testingTree;
    [Export]
    SplitContainer splitContainer;
    [Export]
    Control navContainer;
    [Export]
    Tree navigationPane;
    [Export]
    Texture2D navButtonTexture;
    [Export]
    PackedScene shopHeaderScene;
    [Export]
    PackedScene shopRowScene;
    [Export]
    PackedScene shopEntryScene;
    [Export]
    Control pageParent;
    [Export]
    Control simpleShopParent;
    [Export]
    Label shopRefreshLabel;
    [Export]
    Button sacButton;
    [Export]
    Control filterBlocker;
    [Export]
    CheckButton simpleShopMode;
    [Export]
    int simpleOpsPerFrame = 30;
    [Export]
    ScrollContainer verticalScrollBox;
    [Export]
    CheckButton requireAddedToday;
    [Export]
    CheckButton excludeDiscountBundles;
    [Export]
    Button resetTypeFilters;
    [Export(PropertyHint.ArrayType)]
    CheckButton[] newOrOldFilters = Array.Empty<CheckButton>();
    [Export(PropertyHint.ArrayType)]
    CheckButton[] typeFilters = Array.Empty<CheckButton>();

    public override void _Ready()
    {
        VisibilityChanged += async () =>
        {
            if (IsVisibleInTree())
            {
                //load shop
                sacButton.Text = await ProfileRequests.IsSACExpired() ? "None" : (await ProfileRequests.GetSACCode());
                await LoadShop();
                CurrencyHighlight.Instance.SetCurrencyType("AccountResource:eventcurrency_scaling");
            }
        };

        TitleBarDragger.PerformRefresh += async () =>
        {
            //load shop
            await LoadShop();
        };
        testingTree.CellSelected += () =>
        {
            GD.Print(testingTree.GetSelected().GetMetadata(testingTree.GetSelectedColumn()));
        };
        navigationPane.ButtonClicked += OnNavButton;
        navigationPane.CellSelected += OnNavCell;
        sacButton.Pressed += OpenSACPrompt;

        requireAddedToday.Pressed += ApplyFilters;
        excludeDiscountBundles.Pressed += ApplyFilters;
        foreach (var button in newOrOldFilters)
        {
            button.Pressed += ApplyFilters;
        }
        foreach (var button in typeFilters)
        {
            button.Pressed += ApplyFilters;
        }
        simpleShopMode.Pressed += async () =>
        {
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

    public override void _Process(double delta)
    {
        if (shopRefreshTime != default && shopRefreshLabel is not null)
        {
            var remainingTime = (shopRefreshTime.AddSeconds(3) - DateTime.UtcNow);

            if (remainingTime.TotalMinutes < 1)
                shopRefreshLabel.SelfModulate = Colors.Red;
            else if (remainingTime.TotalHours < 1)
                shopRefreshLabel.SelfModulate = Colors.Orange;
            else
                shopRefreshLabel.SelfModulate = Colors.White;

            shopRefreshLabel.Text = remainingTime.FormatTime();
            if (DateTime.UtcNow.CompareTo(shopRefreshTime) >= 0)
            {
                shopRefreshLabel = default;
                if (Visible)
                    LoadShop().RunSafely();
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Visible && (Engine.GetPhysicsFrames() % 5) == 1)//only do this every 5 physics ticks
            UpdateShopOfferResourceLoading();
    }

    async void OpenSACPrompt()
    {
        string subtext = GD.Randf() > 0.95f ?
            "Your selected code normally disappears after 2 weeks, but PegLeg can automatically re-select the code on launch!" :
            "Whoever you choose to support will recieve 5% of the cost of any Real-Money or VBuck purchases you make";
        var response = await GenericLineEditWindow.OpenLineEdit("Support A Creator!", subtext, sacButton.Text, "Who do you want to support?");
        if (response is null)
            return;

        LoadingOverlay.Instance.AddLoadingKey("setSAC");
        await ProfileRequests.SetSACCode(response);
        sacButton.Text = await ProfileRequests.IsSACExpired() ? "None" : (await ProfileRequests.GetSACCode());
        LoadingOverlay.Instance.RemoveLoadingKey("setSAC");
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
    DateTime shopRefreshTime;
    List<CosmeticShopOfferEntry> activeOffers = new();
    List<CosmeticShopOfferEntry> onScreenOffers = new();
    List<PageGrouping> activePages = new();
    record PageGrouping(Control pageHeader, List<CosmeticShopRow> pageRows);

    public async Task LoadShop(bool force = false)
    {
        if (isLoadingShop || !(CatalogRequests.StorefrontRequiresUpdate() || force || activeOffers.Count == 0) || !await LoginRequests.TryLogin())
            return;
        isLoadingShop = true;
        filterBlocker.Visible = true;

        activeOffers.Clear();
        onScreenOffers.Clear();
        activePages.Clear();
        navigationPane.Clear();

        shopRefreshTime = await CalenderRequests.DailyShopRefreshTime();
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
                var navSec = navCat.CreateChild();
                navSec.SetText(0, section.Key);
                var header = shopHeaderScene.Instantiate<Control>();
                firstHeader ??= header;
                pageParent.AddChild(header);
                if (header.FindChild("HeaderLabel", true) is Label headerLabel)
                    headerLabel.Text = section.Key;
                //navSec.AddButton(1, navButtonTexture);
                navSec.SetMetadata(0, header);
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
        if(requireAddedToday.ButtonPressed && !entry.isAddedToday)
            return false;

        if (!(newOrOldFilterValue switch
        {
            0 => entry.isAddedToday && entry.isRecentlyNew,
            1 => entry.isRecentlyNew,
            3=> entry.isOld,
            4 => entry.isVeryOld,
            _ => true
        }))
            return false;

        var types = entry.itemTypes;

        if(!excludeDiscountBundles.ButtonPressed && entry.isMultiBundle)
            return false;

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
        if (typeMasks[7] && MatchAnyFilterIndex(types, 16, 5))
            return true;
        if (typeMasks[8] && !MatchAnyFilter(types))
            return true;

        if (typeMasks.Any(m => m))
            return false;

        return true;
    }

    /* old testing stuff
    public void BuildTestingTree(TreeItem fromBranch, JsonNode fromNode, string fromKey, int depth)
    {
        if (fromNode is JsonObject fromObject){
            if (fromKey is not null)
                fromBranch.SetMetadata(0, fromKey + " : " + fromObject.Count);
            foreach (var item in fromObject)
            {
                if (depth == 0)
                {
                    var leaf = fromBranch.CreateChild();
                    if (item.Value["bundle"] is not null)
                        FormatBundleName(leaf, item.Value.AsObject());
                    else
                        FormatItemName(leaf, item.Value.AsObject());
                }
                else if (item.Key == "Uncategorised")
                {
                    BuildTestingTree(fromBranch, item.Value, item.Key, depth - 1);
                }
                else if (fromObject.Count == 1 && fromKey == item.Key)
                {
                    BuildTestingTree(fromBranch, item.Value, item.Key, depth - 1);
                }
                else
                {
                    var newBranch = fromBranch.CreateChild();
                    newBranch.SetText(0, item.Key);
                    BuildTestingTree(newBranch, item.Value, item.Key, depth - 1);
                    newBranch.Collapsed = true;
                }
            }
        }
        else if (fromNode is JsonArray fromArray)
        {
            if (fromKey is not null)
                fromBranch.SetMetadata(0, fromKey + " : " + fromArray.Count);
            if (fromArray.Count==1)
            {
                var item = fromArray[0];
                BuildTestingTree(fromBranch, item, fromKey, depth - 1);
            }
            else
            {
                for (int i = 0; i < fromArray.Count; i++)
                {
                    var item = fromArray[i];
                    var newBranch = fromBranch.CreateChild();
                    newBranch.SetText(0, "Page " + (i + 1));
                    BuildTestingTree(newBranch, item, "Page " + (i + 1), depth - 1);
                    newBranch.Collapsed = true;
                }
            }
        }
    }

    public void FormatBundleName(TreeItem leaf, JsonObject offer)
    {
        leaf.SetText(0, offer["bundle"]["name"].ToString());
        leaf.SetMetadata(0, offer.ToString());

        leaf.SetText(1, offer["finalPrice"].ToString()+" VBucks");
        leaf.SetMetadata(1, offer["pricing"]?.ToString() ?? "");

        leaf.SetText(2, "Bundle [" + offer.GetCosmeticItemCounts().ToString()+" items]");
        leaf.SetTooltipText(2, offer.MergeCosmeticItems()
            .GroupBy(i => i["type"]?["displayValue"].ToString())
            .Select(g => g.Key + (g.Count() > 1 ? " x" + g.Count() : ""))
            .ToArray()
            .Join(", "));
        leaf.SetMetadata(2, offer.MergeCosmeticItems()?.ToString() ?? offer.ToString());
    }

    public void FormatItemName(TreeItem leaf, JsonObject offer)
    {
        if(offer.GetFirstCosmeticItem() is not JsonObject firstItem)
        {
            leaf.SetText(0, offer["offerId"].ToString());
            leaf.SetMetadata(0, offer.ToString());
            return;
        }

        leaf.SetText(0, firstItem["name"]?.ToString() ?? offer["offerId"].ToString());
        leaf.SetMetadata(0, offer.ToString());

        leaf.SetText(1, offer["finalPrice"].ToString() + " VBucks");
        leaf.SetMetadata(1, offer["pricing"]?.ToString());

        int totalItemCount = offer.GetCosmeticItemCounts();
        leaf.SetText(2, firstItem["type"]?["displayValue"].ToString() + (totalItemCount > 1 ? $" (+{totalItemCount - 1})" : ""));
        leaf.SetTooltipText(2, offer.MergeCosmeticItems()
            .GroupBy(i => i["type"]?["displayValue"].ToString())
            .Select(g => g.Key + (g.Count()>1?" x" + g.Count():""))
            .ToArray()
            .Join(", "));
        leaf.SetMetadata(2, firstItem.ToString());
    }

    */
}
