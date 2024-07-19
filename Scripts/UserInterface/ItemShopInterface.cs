using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class ItemShopInterface : Control
{
    [Export]
    PackedScene regularShopElement;
    [Export]
    PackedScene highlightShopElement;

    [ExportGroup("Weekly")]
    [Export]
    Label weeklyTimer;
    [Export]
    Control weeklyHighlightParent;
    [Export]
    Control weeklyRegularParent;

    [ExportGroup("Event")]
    [Export]
    Label eventTimer;
    [Export]
    Control eventHighlightParent;
    [Export]
    Control eventRegularParent;


    public override void _Ready()
    {
        VisibilityChanged += async () =>
        {
            if (Visible)
            {
                //load shop
                await LoadShop();
                CurrencyHighlight.Instance.SetCurrencyType("AccountResource:eventcurrency_scaling");
            }
        };

        TitleBarDragger.PerformRefresh += async () =>
        {
            //load shop
            await LoadShop();
        };
    }

    bool isLoadingShop = false;
    List<ShopOfferEntry> weeklyHighlightPool = new();
    List<ShopOfferEntry> weeklyRegularPool = new();
    List<ShopOfferEntry> eventHighlightPool = new();
    List<ShopOfferEntry> eventRegularPool = new();
    Dictionary<string, JsonObject> activeOffers = new();

    DateTime weeklyRefreshTime;
    DateTime eventRefreshTime;
    public async Task LoadShop(bool force = false)
    {
        if (isLoadingShop || !(CatalogRequests.StorefrontRequiresUpdate() || force || activeOffers.Count == 0) || !await LoginRequests.TryLogin())
            return;
        activeOffers.Clear();
        isLoadingShop = true;

        weeklyRefreshTime = await CalenderRequests.WeeklyShopRefreshTime();
        eventRefreshTime = await CalenderRequests.EventShopRefreshTime();

        var weeklyShop = await CatalogRequests.GetWeeklyShop();
        var eventShop = await CatalogRequests.GetEventShop();

        PopulateShopSubsection(weeklyShop["highlights"].AsArray(), weeklyHighlightParent, highlightShopElement, weeklyHighlightPool);
        PopulateShopSubsection(weeklyShop["regular"].AsArray(), weeklyRegularParent, regularShopElement, weeklyRegularPool);
        PopulateShopSubsection(eventShop["highlights"].AsArray(), eventHighlightParent, highlightShopElement, eventHighlightPool);
        PopulateShopSubsection(eventShop["regular"].AsArray(), eventRegularParent, regularShopElement, eventRegularPool);

        isLoadingShop = false;
    }

    void PopulateShopSubsection(JsonArray offers, Control parent, PackedScene scene, List<ShopOfferEntry> pool)
    {
        for (int i = 0; i < offers.Count; i++)
        {
            if (pool.Count <= i)
            {
                var newElement = scene.Instantiate<ShopOfferEntry>();
                newElement.Pressed += SelectShopItem;
                parent.AddChild(newElement);
                pool.Add(newElement);
            }
            pool[i].SetOffer(offers[i].AsObject());
            pool[i].Visible = true;
            pool[i].MoveToFront();
            activeOffers.Add(offers[i]["offerId"].ToString(), offers[i].AsObject());
        }
        for (int i = offers.Count; i < pool.Count; i++)
        {
            pool[i].Visible = false;
        }
    }

    async void SelectShopItem(string offerId)
    {
        var offerItem = activeOffers[offerId]["itemGrants"][0].AsObject();
        await GameItemViewer.Instance.SetItem(offerItem);
        await GameItemViewer.Instance.LinkShopOffer(activeOffers[offerId].AsObject(), async ()=> await LoadShop(true));
    }

    public override void _Process(double delta)
    {
        bool refresh = false;

        if (weeklyRefreshTime != default)
        {
            var remainingTime = (weeklyRefreshTime - DateTime.UtcNow);

            if (remainingTime.TotalHours < 1)
                weeklyTimer.SelfModulate = Colors.Red;
            else if (remainingTime.TotalDays < 1)
                weeklyTimer.SelfModulate = Colors.Orange;
            else
                weeklyTimer.SelfModulate = Colors.White;

            weeklyTimer.Text = remainingTime.FormatTime();
            if (DateTime.UtcNow.CompareTo(weeklyRefreshTime) >= 0)
            {
                weeklyRefreshTime = default;
                refresh = true;
            }
        }

        if (eventRefreshTime != default)
        {
            var remainingTime = (eventRefreshTime - DateTime.UtcNow);

            if (remainingTime.TotalDays < 1)
                eventTimer.SelfModulate = Colors.Red;
            else if (remainingTime.TotalDays < 7)
                eventTimer.SelfModulate = Colors.Orange;
            else
                eventTimer.SelfModulate = Colors.White;

            eventTimer.Text = remainingTime.FormatTime();
            if (DateTime.UtcNow.CompareTo(eventRefreshTime) >= 0)
            {
                eventRefreshTime = default;
                refresh = true;
            }
        }

        if (Visible && refresh)
            LoadShop().RunSafely();
    }
}
