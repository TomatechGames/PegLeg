using Godot;
using System;
using System.Linq;
using System.Security.Principal;
using System.Text.Json.Nodes;

public partial class InventoryInterface : Control, IRecyclableElementProvider<GameItem>
{
    [Export]
    RecycleListContainer itemList;
    [Export]
    LineEdit searchBox;
    [Export]
    LineEdit targetUser;
    [Export]
    Control devAllButton;
    [Export]
    VirtualTabBar tabBar;
    [Export]
    string targetProfile;
    [Export(PropertyHint.ArrayType)]
    string[] typeFilters;
    [Export]
    bool sortByName = false;
    [Export]
    bool allowDevMode = true;
    [Export]
    Control creatorImageParent;
    Control[] creatorImages;

    public override void _Ready()
    {
        creatorImages = creatorImageParent.GetChildren().Select(c => (Control)c).ToArray();
        GameAccount.ActiveAccountChanged += UpdateAccount;
        itemList.SetProvider(this);
        searchBox.TextChanged += ApplyFilters;
        var dev = AppConfig.Get("advanced", "developer", false) && allowDevMode;
        if (targetUser is not null)
        {
            targetUser.TextSubmitted += t =>
            {
                AppConfig.Set("inventory", "customUser", t);
                UpdateAccount();
            };
            targetUser.Visible = dev;
            targetUser.Text = dev ? AppConfig.Get("inventory", "customUser", "") : "";
        }
        if (devAllButton is not null)
            devAllButton.Visible = dev;
        currentTypeFilter = typeFilters[0];
        tabBar.CurrentTab = 0;
        tabBar.TabChanged += SetTypeFilter;
        AppConfig.OnConfigChanged += OnConfigChanged;
        VisibilityChanged += TryUpdateAccount;
        UpdateAccount();
    }

    private void OnConfigChanged(string section, string key, JsonValue val)
    {
        if (!(section == "advanced" && key == "developer") && !(section == "inventory" && key == "customUser"))
            return;

        bool dev = AppConfig.Get("advanced", "developer", false) && allowDevMode;
        if (devAllButton is not null)
            devAllButton.Visible = dev;
        if (targetUser is not null)
        {
            targetUser.Visible = dev;
            targetUser.Text = dev ? AppConfig.Get("inventory", "customUser", "") : "";
            if (!dev && string.IsNullOrEmpty(currentTypeFilter))
            {
                currentTypeFilter = typeFilters[0];
                tabBar.CurrentTab = 0;
            }
            UpdateAccount();
        }
    }

    public override void _ExitTree()
    {
        GameAccount.ActiveAccountChanged -= UpdateAccount;
    }

    bool filterNew;
    public void SetNewFilter(bool value)
    {
        filterNew = value;
        ApplyFilters();
    }

    bool filterFavorite;
    public void SetFavoriteFilter(bool value)
    {
        filterFavorite = value;
        ApplyFilters();
    }

    void SetTypeFilter(int index)
    {
        if (index < 0 || index >= typeFilters.Length)
            return;
        currentTypeFilter = typeFilters[index];
        ApplyFilters();
    }

    public void ToggleSortMode() => SetSortMode(!sortByName);
    public void SetSortMode(bool sortByName)
    {
        if (sortByName == this.sortByName)
            return;
        this.sortByName = sortByName;
        ApplySorting();
    }

    GameItem[] allItems;
    GameItem[] filteredItems;
    GameItem[] currentItems;
    string currentTypeFilter = "";
    public int GetRecycleElementCount() => currentItems?.Length ?? 0;
    public GameItem GetRecycleElement(int index) => currentItems?[index];
    GameAccount displayedAccount;
    bool needsUpdate = false;

    void TryUpdateAccount()
    {
        if (needsUpdate)
            UpdateAccount();
    }

    async void UpdateAccount()
    {
        if (!IsVisibleInTree())
        {
            needsUpdate = true;
            return;
        }
        needsUpdate = false;

        allItems = Array.Empty<GameItem>();
        ApplyFilters();
        displayedAccount = GameAccount.activeAccount;
        if (!string.IsNullOrEmpty(targetUser?.Text) && allowDevMode)
            displayedAccount = (await GameAccount.SearchForAccount(targetUser?.Text)) ?? displayedAccount;
        GD.Print("Inventory: "+displayedAccount?.accountId);
        if (targetProfile != FnProfileTypes.AccountItems && !await displayedAccount.Authenticate())
            return;

        foreach (var image in creatorImages)
        {
            image.Visible = displayedAccount.accountId == image.Name;
        }

        var itemProfile = await displayedAccount.GetProfile(targetProfile).Query();
        allItems = itemProfile
            .GetItems()
            .Where(i => i.template is not null)
            .ToArray();
        foreach (var item in allItems)
        {
            item.GetSearchTags();
        }
        ApplyFilters();
    }

    public async void BulkRecycle()
    {
        if (targetProfile != FnProfileTypes.AccountItems || displayedAccount is null || !await displayedAccount.Authenticate())
            return;

        if (filteredItems.Any())
        {
            //foreach (var item in filteredItems)
            //{
            //    item.GetSearchTags();
            //    item.GenerateRawData();
            //}
            var profile = await displayedAccount.GetProfile(targetProfile).Query();
            var loadoutHeroes = profile
                .GetItems("CampaignHeroLoadout")
                .SelectMany(loadout => 
                    loadout.attributes["crew_members"]
                    .AsObject()
                    .Select(kvp=>kvp.Value.ToString())
                )
                .Distinct()
                .ToList();
            GameItemSelector.Instance.SetRecycleDefaults();
            GameItemSelector.Instance.selectablePredicate = item =>
            {
                if (!GameItemSelector.RecyclablePredicate(item))
                    return false;
                if (loadoutHeroes.Contains(item.uuid))
                    return false;
                return true;
            };
            var toRecycle = await GameItemSelector.Instance.OpenSelector(filteredItems, null);
            if ((toRecycle?.Length ?? 0) > 0 && await displayedAccount.Authenticate())
            {
                JsonObject content = new()
                {
                    ["targetItemIds"] = new JsonArray(toRecycle.Select(item => (JsonNode)item.uuid).ToArray())
                };
                using var _ = LoadingOverlay.CreateToken();
                await displayedAccount.GetProfile(FnProfileTypes.AccountItems).PerformOperation("RecycleItemBatch", content);
            }
        }
    }

    //public async void BulkDismantle()
    //{
    //    //implement item amount selection in recycling
    //    return;

    //    if (targetProfile != FnProfileTypes.Backpack || displayedAccount is null || !await displayedAccount.Authenticate())
    //        return;

    //    if (filteredItems.Any())
    //    {
    //        //foreach (var item in filteredItems)
    //        //{
    //        //    item.GetSearchTags();
    //        //    item.GenerateRawData();
    //        //}
    //        GameItemSelector.Instance.SetDismantleDefaults();
    //        var toRecycle = await GameItemSelector.Instance.OpenSelector(filteredItems, null);
    //        if ((toRecycle?.Length ?? 0) > 0 && await displayedAccount.Authenticate())
    //        {
    //            JsonObject content = new()
    //            {
    //                ["targetItemIds"] = new JsonArray(toRecycle.Select(item => (JsonNode)item.uuid).ToArray())
    //            };
    //            using var _ = LoadingOverlay.CreateToken();
    //            await displayedAccount.GetProfile(FnProfileTypes.AccountItems).PerformOperation("RecycleItemBatch", content);
    //        }
    //    }
    //}

    void ApplyFilters(string _) => ApplyFilters();
    void ApplyFilters()
    {
        var possibleTypes = 
            currentTypeFilter
            .Split(',')
            .Select(s => s.Trim())
            .Where(s=>!string.IsNullOrEmpty(s));
        if (!possibleTypes.Any())
            possibleTypes = null;

        var instructions = PLSearch.GenerateSearchInstructions(searchBox.Text);

        filteredItems = allItems
            .Where(item =>
                    (!filterNew || !item.IsSeen) &&
                    (!filterFavorite || item.IsFavourited) &&
                    (possibleTypes?.Contains(item.template.Type) ?? true) &&
                    PLSearch.EvaluateInstructions(instructions, item.RawData) 
                )
            .ToArray();
        ApplySorting();
    }

    public void ApplySorting()
    {
        var resultItems = filteredItems
            .OrderBy(i => !i.template.CanBeLeveled)
            .ThenBy(i => i.template.Type);

        if (sortByName)
            resultItems = resultItems.ThenBy(i => i.template.SortingDisplayName);

        resultItems = resultItems
            //.ThenBy(i => i.template.Category)
            .ThenBy(i => -i.Rating)
            .ThenBy(i => -i.template.RarityLevel)
            .ThenBy(i => i.template.Type == "Ingredient" ? -i.TotalQuantity : 1)
            .ThenBy(i => -i.quantity);

        if (!sortByName)
            resultItems = resultItems.ThenBy(i => i.template.SortingDisplayName);


        currentItems = resultItems.ToArray();
        itemList.UpdateList(true);
    }

    public void OnElementSelected(int index)
    {
        GameItemViewer.Instance.ShowItem(currentItems[index]);
    }
}
