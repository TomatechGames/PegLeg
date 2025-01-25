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
    string targetProfile;
    [Export(PropertyHint.ArrayType)]
    string[] typeFilters;
    [Export]
    bool sortByName = false;

    public override void _Ready()
    {
        GameAccount.ActiveAccountChanged += OnAccountChanged;
        itemList.SetProvider(this);
        searchBox.TextChanged += ApplyFilters;
        var dev = AppConfig.Get("advanced", "developer", false);
        if (targetUser is not null)
        {
            targetUser.TextSubmitted += t =>
            {
                AppConfig.Set("inventory", "customUser", t);
                OnAccountChanged(null);
            };
            targetUser.Visible = dev;
            targetUser.Text = dev ? AppConfig.Get("inventory", "customUser", "") : "";
        }
        if (devAllButton is not null)
            devAllButton.Visible = dev;
        AppConfig.OnConfigChanged += OnConfigChanged;
        OnAccountChanged(null);
    }

    private void OnConfigChanged(string section, string key, JsonValue val)
    {
        bool dev = AppConfig.Get("advanced", "developer", false);
        if (devAllButton is not null)
            devAllButton.Visible = dev;
        if (targetUser is not null)
        {
            targetUser.Visible = dev;
            targetUser.Text = dev ? AppConfig.Get("inventory", "customUser", "") : "";
        }
    }

    public override void _ExitTree()
    {
        GameAccount.ActiveAccountChanged -= OnAccountChanged;
    }

    public void SetFilter(int index)
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

    async void OnAccountChanged(GameAccount _)
    {
        allItems=Array.Empty<GameItem>();
        ApplyFilters();
        var account = GameAccount.activeAccount;
        if (!string.IsNullOrEmpty(targetUser?.Text))
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

        currentItems = resultItems
            .ThenBy(i => i.template.Category)
            .ThenBy(i => -i.Rating)
            .ThenBy(i => -i.template.RarityLevel)
            .ThenBy(i => i.template.Type == "Ingredient" ? -i.TotalQuantity : 1)
            .ThenBy(i => -i.quantity)
            .ToArray();
        itemList.UpdateList(true);
    }

    public void OnElementSelected(int index)
    {
        GameItemViewer.Instance.ShowItem(currentItems[index]);
    }
}
