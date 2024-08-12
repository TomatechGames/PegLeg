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
    Control weeklyHighlightParent;
    [Export]
    Control weeklyRegularParent;

    [ExportGroup("Event")]
    [Export]
    Control eventHighlightParent;
    [Export]
    Control eventRegularParent;


    public override void _Ready()
    {
        VisibilityChanged += async () =>
        {
            if (IsVisibleInTree())
            {
                await LoadShop();
                CurrencyHighlight.Instance.SetCurrencyType("AccountResource:eventcurrency_scaling");
            }
        };

        RefreshTimerController.OnDayChanged += async () =>
        {
            if (IsVisibleInTree())
                await LoadShop();
        };
    }

    bool isLoadingShop = false;
    List<ShopOfferEntry> weeklyHighlightPool = new();
    List<ShopOfferEntry> weeklyRegularPool = new();
    List<ShopOfferEntry> eventHighlightPool = new();
    List<ShopOfferEntry> eventRegularPool = new();
    Dictionary<string, JsonObject> activeOffers = new();

    public async Task LoadShop(bool force = false)
    {
        if (isLoadingShop || !(CatalogRequests.StorefrontRequiresUpdate() || force || activeOffers.Count == 0) || !await LoginRequests.TryLogin())
            return;
        activeOffers.Clear();
        isLoadingShop = true;

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
}
