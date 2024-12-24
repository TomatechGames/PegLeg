using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

public partial class LlamaInterface : Control
{
    [ExportGroup("Scenes")]
    [Export]
    PackedScene catalogLlamaEntryScene;

    [Export]
    PackedScene cardpackLlamaEntryScene;

    [Export]
    PackedScene itemEntryScene;

    [ExportGroup("List")]
    [Export]
    Control offerListLoadingIcon;

    [Export]
    Control offerListErrorIcon;

    [Export]
    Control llamaScrollArea;

    [Export]
    Control llamaOfferParent;

    [Export]
    Control llamaItemEntryPanel;

    [Export]
    Control llamaItemEntryParent;

    [ExportGroup("Selected")]
    [Export]
    Control selectedLlamaPanel;

    [Export]
    GameOfferEntry currentOfferEntry;

    [Export]
    CardPackEntry currentCardpackEntry;

    [Export]
    Control purchaseButton;

    [Export]
    Control openButton;

    [Export]
    SpinBox quantitySpinner;

    [Export]
    Control resultEntriesParent;

    [Export]
    Control surpriseResultPanel;

    [Export]
    Control soldOutResultPanel;


    List<GameOfferEntry> llamaOfferEntries = new();
    Queue<CardPackEntry> llamaItemEntries = new();
    List<GameItemEntry> llamaResultEntries = new();

    GameProfile llamaItemProfile;
    List<LlamaItemStack> llamaItemStacks = new();
    LlamaItemStack currentCardpackSelection = null;

    Dictionary<string, GameOffer> activeOffers = new();
    GameOffer currentOfferSelection;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
        llamaItemEntryPanel.Visible = false;
        VisibilityChanged += LoadShopLlamas;
        LoadShopLlamas();

        RefreshTimerController.OnHourChanged += LoadShopLlamas;
        GameAccount.ActiveAccountChanged += OnAccountChanged;
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnHourChanged -= LoadShopLlamas;
        GameAccount.ActiveAccountChanged -= OnAccountChanged;
        if(llamaItemProfile is not null)
        {
            llamaItemProfile.OnItemAdded -= AddLlamaItem;
            llamaItemProfile.OnItemRemoved -= RemoveLlamaItem;
        }
    }

    class LlamaItemStack
    {
        public readonly string templateId;
        public readonly string customType;
        public readonly bool isKnown;
        public readonly List<GameItem> items;
        public CardPackEntry llamaEntry { get; private set; }

        public LlamaItemStack(GameItem firstItem, CardPackEntry linkedEntry)
        {
            llamaEntry = linkedEntry;

            templateId = firstItem.templateId;

            if (firstItem.template.DisplayName.Contains("Accolade"))
                customType = "Accolade";

            isKnown = firstItem.attributes.ContainsKey("options");

            items = new() { firstItem };
            UpdateEntry();
        }

        public bool Has(GameItem item) => items.Contains(item);
        public bool Has(string uuid) => items.Any(val => val.uuid == uuid);
        public int DisplayAmount => isKnown ? -1 : items.Count;

        public bool IsStackable(GameItem item)
        {
            if (item.attributes.ContainsKey("options"))
                return false;
            if (templateId == item.templateId)
                return true;
            if (item.template.DisplayName.Contains("Accolade") && customType == "Accolade")
                return true;
            return false;
        }

        public void AddItem(GameItem item)
        {
            items.Add(item);
            UpdateEntry();
        }

        public void RemoveItem(GameItem item)
        {
            items.Remove(item);
            if (items.Count > 0)
                UpdateEntry();
        }

        public GameItem GetDisplayItem()
        {
            var nextItem = items.Last();
            nextItem.attributes["stackQuantity"] = isKnown ? -1 : items.Count;
            //OverridePackDisplay(ref nextPack);
            return nextItem;
        }

        void UpdateEntry() => llamaEntry?.SetItem(GetDisplayItem());

        public CardPackEntry DetachLlamaEntry()
        {
            items.Clear();
            llamaEntry.Visible = false;
            var oldEntry = llamaEntry;
            llamaEntry = null;
            return oldEntry;
        }
    }

    void AddLlamaItem(GameItem item)
    {
        if (item.template.Type != "CardPack")
            return;
        llamaItemEntryPanel.Visible = true;
        var stackableGroup = llamaItemStacks.FirstOrDefault(val=>val.IsStackable(item));

        if (stackableGroup is not null)
        {
            stackableGroup.AddItem(item);
            return;
        }

        CardPackEntry newEntry;
        if (llamaItemEntries.Count > 0)
        {
            //pull from queue
            newEntry = llamaItemEntries.Dequeue();
            llamaItemEntryParent.MoveChild(newEntry, 0);
            newEntry.Visible = true;
        }
        else
        {
            //spawn new
            newEntry = cardpackLlamaEntryScene.Instantiate<CardPackEntry>();
            llamaItemEntryParent.AddChild(newEntry);
            newEntry.LlamaPressed += SetCardPackLlama;
        }

        LlamaItemStack llamaStack = new(item, newEntry);
        llamaItemStacks.Add(llamaStack);
    }

    void RemoveLlamaItem(GameItem item)
    {
        if (item.template.Type != "CardPack")
            return;
        var llamaStack = llamaItemStacks.FirstOrDefault(val => val.Has(item));
        if (llamaStack is not null)
        {
            llamaStack.RemoveItem(item);
            if (llamaStack.items.Count == 0)
            {
                llamaItemEntries.Enqueue(llamaStack.DetachLlamaEntry());
                llamaItemStacks.Remove(llamaStack);
                if (llamaItemStacks.Count == 0)
                {
                    llamaItemEntryPanel.Visible = false;
                }
            }
        }
    }

    CancellationTokenSource accountChangeCTS;
    private async void OnAccountChanged(GameAccount _)
    {
        accountChangeCTS?.Cancel();
        accountChangeCTS = new();
        var ct = accountChangeCTS.Token;

        //filter offers
        if (activeOffers.Count > 0)
        {
            foreach (var offerEntry in llamaOfferEntries)
            {
                var isMatch = await LlamaOfferFilter(offerEntry.currentOffer);
                if (ct.IsCancellationRequested)
                    return;
                offerEntry.SetMeta("llamaFilter", isMatch);
            }
        }

        //refresh cardpacks
        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return;
        if (ct.IsCancellationRequested)
            return;

        GameProfile newLlamaItemProfile = null;
        if(llamaItemProfile is null || llamaItemProfile.account != account)
        {
            //get replacement items
            newLlamaItemProfile = await account.GetProfile(FnProfileTypes.AccountItems).Query();
            if (ct.IsCancellationRequested)
                return;
        }

        //apply new data synchronously
        foreach (var offerEntry in llamaOfferEntries)
        {
            offerEntry.Visible = offerEntry.GetMeta("llamaFilter", false).AsBool();
        }
        if (newLlamaItemProfile is not null)
        {
            //disconnect prev profile
            llamaItemProfile.OnItemAdded -= AddLlamaItem;
            llamaItemProfile.OnItemRemoved -= RemoveLlamaItem;

            //load new items
            var newLlamaItems = llamaItemProfile.GetItems("CardPack");
            foreach (var llamaStack in llamaItemStacks)
            {
                llamaItemEntries.Enqueue(llamaStack.DetachLlamaEntry());
            }
            llamaItemStacks.Clear();
            llamaItemEntryPanel.Visible = false;
            foreach (var item in newLlamaItems)
            {
                AddLlamaItem(item);
            }

            //connect new profile
            llamaItemProfile = newLlamaItemProfile;
            llamaItemProfile.OnItemAdded += AddLlamaItem;
            llamaItemProfile.OnItemRemoved += RemoveLlamaItem;
        }
    }

    CancellationTokenSource llamaShopCTS;
    SemaphoreSlim llamaShopSephamore = new(1);
    async void LoadShopLlamas() => await LoadShopLlamasAsync();
    async void ForceLoadShopLlamas() => await LoadShopLlamasAsync(true);
    async Task LoadShopLlamasAsync(bool force = false)
    {
        if (!IsVisibleInTree() || (!force && activeOffers.Count >= 0))
            return;
        llamaShopCTS?.Cancel();
        llamaShopCTS = new();
        var ct = llamaShopCTS.Token;

        offerListLoadingIcon.Visible = true;
        llamaScrollArea.Visible = false;
        offerListErrorIcon.Visible = false;

        activeOffers.Clear();
        ClearSelection();

        bool success = false;
        try
        {
            await llamaShopSephamore.WaitAsync(ct);
            if (ct.IsCancellationRequested)
                return;

            var prevSelectedOffer = currentOfferSelection;

            var storefront = await GameStorefront.GetStorefront(FnStorefrontTypes.XRayLlamaCatalog, force ? null : RefreshTimeType.Hourly);
            if (ct.IsCancellationRequested)
                return;

            if (storefront?.isValid ?? false)
                return;

            int catalogEntryIndex = 0;
            List<GameOffer> filteredOffers = new();
            foreach (var offer in storefront.Offers)
            {
                if (await LlamaOfferFilter(offer))
                    filteredOffers.Add(offer);
                if (ct.IsCancellationRequested)
                    return;
            }

            foreach (var offer in filteredOffers)
            {
                if (llamaOfferEntries.Count <= catalogEntryIndex)
                {
                    var newEntry = catalogLlamaEntryScene.Instantiate<GameOfferEntry>();
                    llamaOfferParent.AddChild(newEntry);
                    newEntry.Pressed += SetLlamaOffer;
                    llamaOfferEntries.Add(newEntry);
                }
                activeOffers.Add(offer.OfferId, offer);
                var thisEntry = llamaOfferEntries[catalogEntryIndex];
                thisEntry.Visible = true;
                thisEntry.SetOffer(offer).Start();
                catalogEntryIndex++;
            }

            for (int i = catalogEntryIndex; i < llamaOfferEntries.Count; i++)
            {
                llamaOfferEntries[i].Visible = false;
            }

            if (prevSelectedOffer is not null)
                SetLlamaOffer(prevSelectedOffer);
            success = true;
        }
        finally
        {
            llamaShopSephamore.Release();
            offerListLoadingIcon.Visible = false;
            llamaScrollArea.Visible = true;
            offerListErrorIcon.Visible = !success;
        }

    }

    static async Task<bool> LlamaOfferFilter(GameOffer offer)
    {
        //get rid of weird mini llama and free llama
        //if (offer["devName"].ToString() == "Mini Llama Manual Tutorial - high SharedDisplayPriority")
        //    return false;
        //if (offer["devName"].ToString() == "Always.UpgradePack.03")
        //    return false;

        string priceTemplateId = offer.RegularPrice.templateId;
        int price = offer.RegularPrice.quantity;
        if (price != 1)
            return true;

        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return false;

        return (await account.GetProfile(FnProfileTypes.AccountItems).Query()).GetTemplateItems(priceTemplateId).Select(item => item.quantity).Sum() >= 1;
    }

    public void ClearSelection()
    {
        offerCts?.Cancel();

        openButton.Visible = false;
        purchaseButton.Visible = false;
        quantitySpinner.Visible = false;

        resultEntriesParent.Visible = false;
        surpriseResultPanel.Visible = false;
        soldOutResultPanel.Visible = false;

        currentOfferEntry.ClearOffer();
        currentCardpackEntry.ClearItem();

        currentOfferSelection = null;
        currentCardpackSelection = null;
    }

    CancellationTokenSource offerCts;

    public void SetLlamaOffer(string offerId)
    {
        if (!activeOffers.ContainsKey(offerId))
        {
            ClearSelection();
            return;
        }
        SetLlamaOffer(activeOffers[offerId]);
    }

    public async void SetLlamaOffer(GameOffer offer)
    {
        ClearSelection();
        offerCts = new();
        var ct = offerCts.Token;

        //show loading icon
        
        var prerollData = await offer.GetPrerollData();
        if (ct.IsCancellationRequested)
            return;

        await currentOfferEntry.SetOffer(offer);
        if (ct.IsCancellationRequested)
            return;

        purchaseButton.Visible = true;
        currentOfferSelection = offer;
        CurrencyHighlight.Instance.SetCurrencyTemplate(offer.RegularPrice.template);

        var resultItems = prerollData?.attributes?["items"].AsArray().Select(node => new GameItem(null, null, node.AsObject())).ToArray() ?? null;
        SetSelectedLlamaResults(resultItems);
    }

    public void SetCardPackLlama(string uuid)
    {
        ClearSelection();

        var cardPackStack = llamaItemStacks.FirstOrDefault(val => val.Has(uuid));
        var cardPackItem = cardPackStack?.GetDisplayItem();

        if (cardPackItem is null)
            return;

        currentCardpackSelection = cardPackStack;
        purchaseButton.Visible = false;
        openButton.Visible = true;
        soldOutResultPanel.Visible = false;

        var maxAmount = currentCardpackSelection.DisplayAmount;
        quantitySpinner.MaxValue = Mathf.Max(maxAmount, 1);
        quantitySpinner.Visible = maxAmount > 1;

        GameItem[] items = null;
        if (cardPackItem.attributes.ContainsKey("options"))
        {
            items = new GameItem[] { cardPackItem };
        }
        items ??= Array.Empty<GameItem>();

        SetSelectedLlamaResults(items);
        currentCardpackEntry.SetItem(cardPackItem);
    }

    void SetSelectedLlamaResults(GameItem[] items)
    {
        if (items is not null)
        {
            var sortedItems = items.OrderBy(item => -item.template.RarityLevel).ThenBy(item => item.template.Type).ToArray();
            //fill out item list
            surpriseResultPanel.Visible = false;
            resultEntriesParent.Visible = true;

            while (llamaResultEntries.Count <= items.Length)
            {
                var newEntry = itemEntryScene.Instantiate<GameItemEntry>();
                resultEntriesParent.AddChild(newEntry);
                llamaResultEntries.Add(newEntry);
            }
            for (int i = 0; i < items.Length; i++)
            {
                string templateId = sortedItems[i].templateId;
                llamaResultEntries[i].Visible = true;
                llamaResultEntries[i].SetItem(sortedItems[i]);
                llamaResultEntries[i].SetRewardNotification();
                llamaResultEntries[i].SetInteractableSmart();
            }
            for (int i = items.Length; i < llamaResultEntries.Count; i++)
            {
                llamaResultEntries[i].Visible = false;
            }
        }
        else
        {
            //it's a surprise
            surpriseResultPanel.Visible = true;
            resultEntriesParent.Visible = false;
        }
    }

    public async void PurchaseLlama()
    {
        if (currentOfferSelection is null)
            return;

        GD.Print("attempting to purchase offer: "+ currentOfferSelection.OfferId);

        var itemsKnown = await currentOfferSelection.GetPrerollData() is not null;
        await CardPackOpener.Instance.StartOpening(null, selectedLlamaPanel, currentOfferSelection, currentOfferEntry.currentPurchaseQuantity, itemsKnown);

        SetLlamaOffer(currentOfferSelection);
    }

    public async void OpenSelectedCardpack()
    {
        if (currentCardpackSelection is null)
            return;
        int amount = (int)quantitySpinner.Value;
        var items = currentCardpackSelection.items.ToArray()[^amount..];
        bool depletesSelected = currentCardpackSelection.items.Count <= amount;

        //await BulkOpenCardpacks(handles.Select(val=>val.uuid).ToArray());
        await CardPackOpener.Instance.StartOpening(items, selectedLlamaPanel, currentCardpackSelection.GetDisplayItem());

        if (depletesSelected)
            ClearSelection();
        else
            SetCardPackLlama(currentCardpackSelection.items.Last().uuid);
    }

    static GameItem allTheLlamas;
    public async void BulkOpenAllCardpacks()
    {
        List<GameItem> allCardpacks = new();
        bool includesSelected = false;
        foreach (var group in llamaItemStacks)
        {
            //if (group.isKnown)
            //    continue;
            if (currentCardpackSelection == group)
                includesSelected = true;
            foreach (var item in group.items)
            {
                allCardpacks.Add(item);
            }
        }
        allTheLlamas ??= GameItemTemplate.Get("CardPack:cardpack_bronze_10x").CreateInstance();
        await CardPackOpener.Instance.StartOpening(allCardpacks.ToArray(), selectedLlamaPanel, allTheLlamas);
        //await BulkOpenCardpacks(itemIds.ToArray());
        if (includesSelected)
            ClearSelection();
    }
}
