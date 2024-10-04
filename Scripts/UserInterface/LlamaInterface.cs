using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class LlamaInterface : Control
{
    [Export]
    PackedScene catalogLlamaEntryScene;
    [Export]
    PackedScene cardpackLlamaEntryScene;
    [Export]
    PackedScene itemEntryScene;

    [Export]
    NodePath loadingIconPath;
    Control loadingIcon;

    [Export]
    Control llamaScrollArea;

    [Export]
    NodePath catalogLlamaParentPath;
    Control catalogLlamaParent;

    [Export]
    NodePath cardpackLlamaPanelPath;
    Control cardpackLlamaPanel;

    [Export]
    NodePath cardpackLlamaParentPath;
    Control cardpackLlamaParent;

    [ExportGroup("Selected")]
    [Export]
    Control selectedLlamaPanel;

    [Export]
    NodePath selectedLlamaEntryPath;
    LlamaEntry selectedCardPackEntry;

    [Export]
    NodePath selectedOpenPanelPath;
    Control selectedOpenPanel;

    [Export]
    NodePath selectedPurchasePanelPath;
    Control selectedPurchasePanel;

    [Export]
    NodePath selectedBrokePanelPath;
    Control selectedBrokePanel;

    [Export]
    NodePath selectedPriceIconPath;
    TextureRect selectedPriceIcon;

    [Export]
    NodePath selectedPriceLabelPath;
    Label selectedPriceLabel;

    [Export]
    SpinBox selectedPurchaseCountSpinner;

    [Export]
    NodePath selectedItemEntryParentPath;
    Control selectedItemEntryParent;

    [Export]
    NodePath selectedSurprisePanelPath;
    Control selectedSurprisePanel;

    [Export]
    NodePath selectedNoStockPanelPath;
    Control selectedNoStockPanel;


    List<ShopOfferEntry> pooledCatalogLlamas = new();
    Queue<LlamaEntry> pooledCardpackLlamas = new();
    List<GameItemEntry> pooledGameItems = new();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
        this.GetNodeOrNull(catalogLlamaParentPath, out catalogLlamaParent);
        this.GetNodeOrNull(cardpackLlamaParentPath, out cardpackLlamaParent);
        this.GetNodeOrNull(loadingIconPath, out loadingIcon);
        this.GetNodeOrNull(selectedLlamaEntryPath, out selectedCardPackEntry);
        this.GetNodeOrNull(selectedOpenPanelPath, out selectedOpenPanel);
        this.GetNodeOrNull(selectedPurchasePanelPath, out selectedPurchasePanel);
        this.GetNodeOrNull(selectedBrokePanelPath, out selectedBrokePanel);
        this.GetNodeOrNull(selectedPriceLabelPath, out selectedPriceLabel);
        this.GetNodeOrNull(selectedPriceIconPath, out selectedPriceIcon);
        this.GetNodeOrNull(selectedItemEntryParentPath, out selectedItemEntryParent);
        this.GetNodeOrNull(selectedSurprisePanelPath, out selectedSurprisePanel);
        this.GetNodeOrNull(selectedNoStockPanelPath, out selectedNoStockPanel);
        this.GetNodeOrNull(cardpackLlamaPanelPath, out cardpackLlamaPanel);

        availableCardPacks.Clear();

        VisibilityChanged += OnVisibilityChanged;
        OnVisibilityChanged();

        RefreshTimerController.OnHourChanged += OnHourChanged;
    }

    async void OnVisibilityChanged()
    {
        if (!IsVisibleInTree())
            return;
        await LoadLlamas();
        if (!string.IsNullOrWhiteSpace(currentPurchaseSelection) && activeOffers.ContainsKey(currentPurchaseSelection))
        {
            var offer = activeOffers[currentPurchaseSelection];
            string priceType = offer["prices"][0]["currencySubType"].ToString();
            CurrencyHighlight.Instance.SetCurrencyType(priceType);
        }
        if (cardPackListener is null)
        {
            cardPackListener = await ProfileListener.CreateListener(FnProfiles.AccountItems, "CardPack");
            cardPackListener.OnAdded += AddCardPackEntry;
            cardPackListener.OnRemoved += RemoveCardPackEntry;
            foreach (var item in cardPackListener.Items)
            {
                AddCardPackEntry(item);
            }
        }
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnHourChanged -= OnHourChanged;
        //cardPackListener?.Free();
    }

    private async void OnHourChanged()
    {
        if (IsVisibleInTree())
            await LoadLlamas();
    }

    ProfileListener cardPackListener;
    List<CardPackGroup> availableCardPacks = new();

    class CardPackGroup
    {
        public string PressData => linkedHandles.First().itemID.uuid;

        public readonly string type;
        public readonly string customType;
        public readonly bool isKnown;
        public readonly List<ProfileItemHandle> linkedHandles = new();
        public readonly LlamaEntry llamaEntry;

        public CardPackGroup(ProfileItemHandle firstHandle, LlamaEntry linkedEntry)
        {
            llamaEntry = linkedEntry;
            var firstItem = firstHandle.GetItemUnsafe();

            type = firstItem["templateId"].ToString();

            if (firstItem.GetTemplate()["DisplayName"].ToString().Contains("Accolade"))
                customType = "Accolade";

            isKnown = firstItem["attributes"].AsObject().ContainsKey("options");
            AddHandle(firstHandle);
        }

        public bool Has(ProfileItemHandle handle) => linkedHandles.Contains(handle);
        public bool Has(string uuid) => linkedHandles.Any(val => val.itemID.uuid == uuid);

        public int DisplayAmount => isKnown ? -1 : linkedHandles.Count;

        public bool IsStackable(ProfileItemHandle handle)
        {
            var item = handle.GetItemUnsafe();
            if (item["attributes"].AsObject().ContainsKey("options"))
                return false;
            if (type == item["templateId"].ToString())
                return true;
            if (item.GetTemplate()["DisplayName"].ToString().Contains("Accolade") && customType== "Accolade")
                return true;
            return false;
        }

        void OverridePackDisplay(ref JsonObject baseObject)
        {
            if (customType == "Accolade")
            {
                baseObject["template"]["DisplayName"] = "Battle Royale Xp Bundle";
                baseObject["template"]["ImagePaths"] ??= new JsonObject();
                baseObject["template"]["ImagePaths"]["SmallPreview"] = "ExportedImages\\T_UI_FNBR_XPeverywhere_S.png";
            }
        }

        public void AddHandle(ProfileItemHandle handle)
        {
            linkedHandles.Add(handle);
            UpdateEntry();
        }

        public void RemoveHandle(ProfileItemHandle handle)
        {
            linkedHandles.Remove(handle);
            if (linkedHandles.Count > 0)
                UpdateEntry();
        }

        public JsonObject GetDisplayCardpack()
        {
            var nextHandle = linkedHandles.Last();
            var nextPack = nextHandle.GetItemUnsafe()?.Reserialise();
            if(nextPack is null)
                return null;
            nextPack["quantity"] = isKnown ? -1 : linkedHandles.Count;
            nextPack.GetTemplate();
            OverridePackDisplay(ref nextPack);
            return nextPack;
        }

        void UpdateEntry()
        {
            var nextHandle = linkedHandles.Last();
            llamaEntry.SetItemData(GetDisplayCardpack());
            llamaEntry.SetLinkedItemId(nextHandle.itemID.uuid);
        }
    }

    void AddCardPackEntry(ProfileItemHandle handle)
    {
        cardpackLlamaPanel.Visible = true;
        var stackableGroup = availableCardPacks.FirstOrDefault(val=>val.IsStackable(handle));

        if (stackableGroup is not null)
        {
            stackableGroup.AddHandle(handle);
            return;
        }

        LlamaEntry newEntry;
        if (pooledCardpackLlamas.Count > 0)
        {
            //pull from queue
            newEntry = pooledCardpackLlamas.Dequeue();
            cardpackLlamaParent.MoveChild(newEntry, 0);
            newEntry.Visible = true;
        }
        else
        {
            //spawn new
            newEntry = cardpackLlamaEntryScene.Instantiate<LlamaEntry>();
            cardpackLlamaParent.AddChild(newEntry);
            newEntry.LlamaPressed += SetCardPackLlama;
        }

        CardPackGroup newGroup = new(handle, newEntry); 
        availableCardPacks.Add(newGroup);
    }

    void RemoveCardPackEntry(ProfileItemHandle handle)
    {
        var stackableGroup = availableCardPacks.FirstOrDefault(val => val.Has(handle));
        if (stackableGroup is not null)
        {
            GD.Print("found stack");
            stackableGroup.RemoveHandle(handle);
            if (stackableGroup.linkedHandles.Count == 0)
            {
                GD.Print("stack depleted");
                if (stackableGroup.llamaEntry.IsInsideTree())
                {
                    stackableGroup.llamaEntry.Visible = false;
                    //pooledCardpackLlamas.Enqueue(stackableGroup.llamaEntry);
                }
                availableCardPacks.Remove(stackableGroup);
                if (availableCardPacks.Count == 0)
                {
                    cardpackLlamaPanel.Visible = false;
                }
            }
        }
    }

    bool isLoadingLlamas = false;
    Dictionary<string, JsonObject> activeOffers = new();
    async Task LoadLlamas(bool force = false, bool clearSelection = true)
    {
        if (isLoadingLlamas || !(CatalogRequests.StorefrontRequiresUpdate() || force || activeOffers.Count==0) || !await LoginRequests.TryLogin())
            return;
        isLoadingLlamas = true;

        loadingIcon.Visible = true;
        llamaScrollArea.Visible = false;
        activeOffers.Clear();
        if (clearSelection)
            ClearSelection();

        for (int i = 0; i < pooledCatalogLlamas.Count; i++)
        {
            pooledCatalogLlamas[i].Visible = false;
        }

        var llamaCatalog = await CatalogRequests.GetLlamaShop(force);
        var filteredLlamaCatalog = await FilterLlamas(llamaCatalog);

        while (pooledCatalogLlamas.Count <= filteredLlamaCatalog.Length)
        {
            var newEntry = catalogLlamaEntryScene.Instantiate<ShopOfferEntry>();
            catalogLlamaParent.AddChild(newEntry);
            newEntry.Pressed += SetCatalogLlama;
            pooledCatalogLlamas.Add(newEntry);
        }

        int catalogEntryIndex = 0;
        foreach (var offer in filteredLlamaCatalog)
        {
            if (offer is null)
                continue;
            activeOffers.Add(offer["offerId"].ToString(), offer);
            var thisEntry = pooledCatalogLlamas[catalogEntryIndex];
            thisEntry.Visible = true;
            thisEntry.SetOffer(offer);
            catalogEntryIndex++;
        }
        for (int i = catalogEntryIndex; i < pooledCatalogLlamas.Count; i++)
        {
            pooledCatalogLlamas[i].Visible = false;
        }

        loadingIcon.Visible = false;
        llamaScrollArea.Visible = true;
        isLoadingLlamas = false;
    }

    static async Task<bool> LlamaFilterFunc(JsonObject offer)
    {
        //get rid of weird mini llama and free llama
        if (offer["devName"].ToString() == "Mini Llama Manual Tutorial - high SharedDisplayPriority")
            return false;
        if (offer["devName"].ToString() == "Always.UpgradePack.03")
            return false;

        
        string priceType = offer["prices"][0]["currencySubType"].ToString();
        int price = offer["prices"][0]["finalPrice"].GetValue<int>();
        int inInventory = await ProfileRequests.GetSumOfProfileItems(FnProfiles.AccountItems, priceType);

        return price != 1 || inInventory >= 1;
    }

    async Task<JsonObject[]> FilterLlamas(JsonObject[] llamaCatalog)
    {
        List<JsonObject> matchedOffers = new();
        foreach (var offer in llamaCatalog)
        {
            if(await LlamaFilterFunc(offer))
                matchedOffers.Add(offer);
        }
        return matchedOffers.ToArray();
    }
    async Task<JsonObject[]> GroupAndFilterLlamas(JsonObject[] llamaCatalog)
    {
        var groupedLlamaCatalog = llamaCatalog.GroupBy(val => val["catalogGroup"].ToString());

        var degrouped = groupedLlamaCatalog
            .Select(
                grouping => grouping
                .OrderBy(val => val["sortPriority"].GetValue<int>())
                .LastOrDefault(item =>
                {
                    if (grouping.Key == "")
                        return false;
                    return true;
                })
            )
            .Union(groupedLlamaCatalog.FirstOrDefault(grouping => grouping.Key == "")?.ToArray() ?? Array.Empty<JsonObject>())
            .ToArray();

        return await FilterLlamas(degrouped);
    }

    public void ClearSelection()
    {
        selectedOpenPanel.Visible = false;
        selectedPurchasePanel.Visible = false;
        selectedBrokePanel.Visible = false;
        selectedSurprisePanel.Visible = false;
        selectedItemEntryParent.Visible = false;
        selectedPurchaseCountSpinner.Visible = false;

        selectedCardPackEntry.ClearItem();

        currentPurchaseSelection = "";
        currentCardpackSelection = null;
    }

    string currentPurchaseSelection = "";
    JsonObject currentPurchaseItem;
    public async void SetCatalogLlama(string offerId)
    {
        //TODO: if offerid isnt in active offers, reset purchase section

        if (!activeOffers.ContainsKey(offerId))
        {
            ClearSelection();
            return;
        }
        currentCardpackSelection = null;

        var offer = activeOffers[offerId];
        var items = offer["prerollData"]?["attributes"]["items"].AsArray() ?? null;
        SetSelectedLlamaItems(items);

        currentPurchaseItem = offer["itemGrants"][0].AsObject();
        selectedCardPackEntry.SetItemData(currentPurchaseItem);

        //maybe replace this stuff with a ShopOfferEntry?
        string priceType = offer["prices"][0]["currencySubType"].ToString();
        int price = offer["prices"][0]["finalPrice"].GetValue<int>();
        var inInventory = await ProfileRequests.GetSumOfProfileItems(FnProfiles.AccountItems, priceType);

        int maxAffordable = price == 0 ? 999 : Mathf.FloorToInt(inInventory / price);
        int maxInStock = await offer.GetPurchaseLimitFromOffer();
        int maxAmount = Mathf.Min(maxAffordable, maxInStock);

        int maxSimultaniousAmount = Mathf.Min(int.Parse(
            offer["metaInfo"]?
                .AsArray()
                .FirstOrDefault(val => val["key"].ToString() == "MaxConcurrentPurchases")?
                ["value"]
                .ToString() 
            ??
            maxAmount.ToString()
        ), maxAmount);

        CurrencyHighlight.Instance.SetCurrencyType(priceType);

        selectedOpenPanel.Visible = false;
        selectedPurchasePanel.Visible = false;
        selectedBrokePanel.Visible = false;
        selectedNoStockPanel.Visible = false;
        selectedPurchaseCountSpinner.Visible = false;

        if (maxAmount > 0)
        {
            currentPurchaseSelection = offerId;
            selectedPurchasePanel.Visible = true;
            selectedPurchaseCountSpinner.MaxValue = maxSimultaniousAmount;
            selectedPurchaseCountSpinner.Visible = maxSimultaniousAmount > 1;
            latestPurchasablePrice = price;
            SpinnerChanged(selectedPurchaseCountSpinner.Value);
            BanjoAssets.TryGetTemplate(priceType, out var priceTemplate);
            selectedPriceIcon.Texture = priceTemplate.GetItemTexture();
        }
        else if (maxInStock <= 0)
        {
            //out of stock
            selectedSurprisePanel.Visible = false;
            selectedItemEntryParent.Visible = false;

            selectedNoStockPanel.Visible = true;
        }
        else
        {
            //can't afford
            selectedBrokePanel.Visible = true;
        }
    }

    void SetSelectedLlamaItems(JsonArray items)
    {
        if (items is not null)
        {
            var sortedItems = items.OrderBy(var => -var.AsObject().GetTemplate().GetItemRarity()).ToArray();
            //fill out item list
            selectedSurprisePanel.Visible = false;
            selectedItemEntryParent.Visible = true;

            while (pooledGameItems.Count <= items.Count)
            {
                var newEntry = itemEntryScene.Instantiate<GameItemEntry>();
                selectedItemEntryParent.AddChild(newEntry);
                pooledGameItems.Add(newEntry);
            }
            for (int i = 0; i < items.Count; i++)
            {
                string templateId = sortedItems[i]?["itemType"]?.ToString() ?? sortedItems[i]["templateId"].ToString();
                if (templateId.StartsWith("ConsumableAccountItem"))
                {
                    pooledGameItems[i].Visible = false;
                    continue;
                }
                pooledGameItems[i].Visible = true;
                pooledGameItems[i].SetItemData(sortedItems[i].AsObject());
                pooledGameItems[i].SetRewardNotification();
                pooledGameItems[i].SetInteractableSmart();
            }
            for (int i = items.Count; i < pooledGameItems.Count; i++)
            {
                pooledGameItems[i].Visible = false;
            }
        }
        else
        {
            //it's a surprise
            selectedSurprisePanel.Visible = true;
            selectedItemEntryParent.Visible = false;
        }
    }

    int latestPurchasablePrice = 0;

    public void SpinnerChanged(double newValue)
    {
        selectedPriceLabel.Text = (latestPurchasablePrice * newValue).ToString();
    }

    public async void PurchaseLlama()
    {
        if (string.IsNullOrWhiteSpace(currentPurchaseSelection))
            return;
        GD.Print("attempting to purchase offer: "+ currentPurchaseSelection);
        var offer = (await CatalogRequests.GetLlamaShop()).FirstOrDefault(var => var["offerId"].ToString() == currentPurchaseSelection);
        JsonObject shopRequestBody = new()
        {
            ["offerId"] = currentPurchaseSelection,
            ["purchaseQuantity"] = selectedPurchaseCountSpinner.Value,
            ["currency"] = offer["prices"][0]["currencyType"].ToString(),
            ["currencySubType"] = offer["prices"][0]["currencySubType"].ToString(),
            ["expectedTotalPrice"] = offer["prices"][0]["finalPrice"].GetValue<int>() * selectedPurchaseCountSpinner.Value,
            ["gameContext"] = "Pegleg",
        };
        var itemsKnown = offer["prerollData"]?["attributes"]["items"].AsArray() is not null;
        await CardPackOpener.Instance.StartOpening(null, selectedLlamaPanel, currentPurchaseItem, shopRequestBody, itemsKnown);
        await LoadLlamas();
        SetCatalogLlama(currentPurchaseSelection);
    }

    CardPackGroup currentCardpackSelection = null;
    public void SetCardPackLlama(string itemId)
    {
        //GD.Print("selecting cardpack: " + itemId);

        //TODO: get a cardpack with prerolled items
        currentPurchaseSelection = "";
        currentCardpackSelection = availableCardPacks.FirstOrDefault(val=>val.Has(itemId));

        if(currentCardpackSelection is null)
        {
            ClearSelection();
            return;
        }

        var cardPackItem = currentCardpackSelection.GetDisplayCardpack();

        if (cardPackItem is null)
        {
            ClearSelection();
            return;
        }

        selectedPurchasePanel.Visible = false;
        selectedBrokePanel.Visible = false;
        selectedNoStockPanel.Visible = false;

        var maxAmount = currentCardpackSelection.DisplayAmount;
        cardPackItem["quantity"] = currentCardpackSelection.DisplayAmount;
        if (maxAmount > 1)
        {
            selectedPurchaseCountSpinner.MaxValue = maxAmount;
            selectedPurchaseCountSpinner.Visible = true;
        }
        else
        {
            selectedPurchaseCountSpinner.MaxValue = 1;
            selectedPurchaseCountSpinner.Visible = false;
        }

        bool allowOpen = true;

        JsonArray items = null;

        if (cardPackItem["attributes"].AsObject().ContainsKey("options"))
        {
            items = new()
            {
                cardPackItem.Reserialise()
            };
            //allowOpen = false;
        }

        SetSelectedLlamaItems(items);
        selectedCardPackEntry.SetItemData(cardPackItem);
        selectedOpenPanel.Visible = allowOpen;
    }

    public async void OpenSelectedCardpack()
    {
        if (currentCardpackSelection is null)
            return;
        int amount = (int)selectedPurchaseCountSpinner.Value;
        var handles = currentCardpackSelection.linkedHandles.ToArray()[^amount..];
        bool depletesSelected = currentCardpackSelection.linkedHandles.Count <= amount;

        //await BulkOpenCardpacks(handles.Select(val=>val.uuid).ToArray());
        await CardPackOpener.Instance.StartOpening(handles, selectedLlamaPanel, currentCardpackSelection.GetDisplayCardpack());

        if (depletesSelected)
            ClearSelection();
        else
            SetCardPackLlama(currentCardpackSelection.linkedHandles.Last().itemID.uuid);
    }

    public async void BulkOpenAllCardpacks()
    {
        List<ProfileItemHandle> handles = new();
        bool includesSelected = false;
        foreach (var group in availableCardPacks)
        {
            //if (group.isKnown)
            //    continue;
            if (currentCardpackSelection == group)
                includesSelected = true;
            foreach (var handle in group.linkedHandles)
            {
                handles.Add(handle);
            }
        }
        await CardPackOpener.Instance.StartOpening(handles.ToArray(), selectedLlamaPanel);
        //await BulkOpenCardpacks(itemIds.ToArray());
        if (includesSelected)
            ClearSelection();
    }
}
