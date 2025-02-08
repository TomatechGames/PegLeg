using Godot;
using System;
using System.Linq;
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

    public override void _Ready()
    {
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
        UpdateAccount();
    }

    private void OnConfigChanged(string section, string key, JsonValue val)
    {
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

    async void UpdateAccount()
    {
        allItems = Array.Empty<GameItem>();
        ApplyFilters();
        var account = GameAccount.activeAccount;
        if (!string.IsNullOrEmpty(targetUser?.Text) && allowDevMode)
            account = (await GameAccount.SearchForAccount(targetUser?.Text)) ?? account;
        GD.Print(account?.accountId);
        if (targetProfile != FnProfileTypes.AccountItems && !await account.Authenticate())
            return;
        var itemProfile = await account.GetProfile(targetProfile).Query();
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
