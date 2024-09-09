using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class ItemShopInterface : Control
{
    [Export]
    bool useEventShop = false;

    [Export]
    PackedScene regularShopElement;
    [Export]
    PackedScene highlightShopElement;

    [Export]
    Control highlightParent;
    [Export]
    Control regularParent;


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

        RefreshTimerController.OnDayChanged += OnDayChanged;
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnDayChanged -= OnDayChanged;
    }

    private async void OnDayChanged()
    {
        activeOffers.Clear();
        if (IsVisibleInTree())
            await LoadShop();
    }

    bool isLoadingShop = false;
    List<ShopOfferEntry> highlightPool = new();
    List<ShopOfferEntry> regularPool = new();
    Dictionary<string, JsonObject> activeOffers = new();

    public async Task LoadShop(bool force = false)
    {
        if (isLoadingShop || !(CatalogRequests.StorefrontRequiresUpdate() || force || activeOffers.Count == 0) || !await LoginRequests.TryLogin())
            return;
        activeOffers.Clear();
        isLoadingShop = true;

        var thisShop = useEventShop ? await CatalogRequests.GetEventShop() : await CatalogRequests.GetWeeklyShop();
        PopulateShopSubsection(thisShop["highlights"].AsArray(), highlightParent, highlightShopElement, highlightPool);
        PopulateShopSubsection(thisShop["regular"].AsArray(), regularParent, regularShopElement, regularPool);

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
        await GameItemViewer.Instance.ShowItem(offerItem);
        await GameItemViewer.Instance.LinkShopOffer(activeOffers[offerId].AsObject(), async ()=> await LoadShop(true));
    }
}
