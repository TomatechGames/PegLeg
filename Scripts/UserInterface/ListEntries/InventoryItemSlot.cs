using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

public partial class InventoryItemSlot : Node
{
    [Export]
    GameItemEntry entry;

    [Export]
    Control inspectArea;

    [Export]
    Control open;

    [Export]
    Control locked;

    [Export]
    Control buttonControl;

    [Export]
    bool showInspector = false;

    [Export]
    bool useActiveAccount;


    public event Action<InventoryItemSlot> OnItemChangeRequested;
    public event Action<InventoryItemSlot> OnSlotItemChanged;

    GameAccount currentAcount;
    public GameProfile currentProfile { get; private set; }
    public GameItem slottedItem { get; private set; }
    string profileType;
    string itemTypeFilter;
    GameItemPredicate predicateFilter = FallbackPredicate;
    bool isEmpty = true;

    public override void _Ready()
    {
        SetLocked(true);
        GameAccount.ActiveAccountChanged += OnActiveAccountChanged;
    }

    void OnActiveAccountChanged(GameAccount account)
    {
        if (useActiveAccount)
            SetAccountInternal(account);
    }

    public void UseActiveAccount()
    {
        useActiveAccount = false;
        SetAccountInternal(GameAccount.activeAccount);
    }
    
    public void SetAccount(GameAccount account)
    {
        useActiveAccount = false;
        SetAccountInternal(account);
    }

    CancellationTokenSource setAccountCts;
    async void SetAccountInternal(GameAccount account)
    {
        profileUpdateCts?.Cancel();
        setAccountCts?.Cancel();

        setAccountCts = new();
        var ct = setAccountCts.Token;

        bool isAuthed = await account.Authenticate();
        if (ct.IsCancellationRequested)
            return;

        if (!isAuthed)
        {
            currentAcount = null;
            SetProfile(null);
            return;
        }
        currentAcount = account;
        SetProfile(profileType is not null ? currentAcount?.GetProfile(profileType) : null);

    }

    void SetProfile(GameProfile newProfile)
    {
        bool hadProfile = profileType is not null;
        if (hadProfile)
        {
            currentProfile.OnItemUpdated -= UpdateSlot;
            currentProfile.OnItemRemoved -= UpdateSlot;
        }

        currentProfile = newProfile;

        if (currentProfile is null)
        {
            if (slottedItem is not null)
            {
                entry.ClearItem();
                slottedItem = null;
                isEmpty = true;
                OnSlotItemChanged?.Invoke(this);
            }
            SetLocked(true);
            return;
        }

        currentProfile.OnItemUpdated += UpdateSlot;
        currentProfile.OnItemRemoved += UpdateSlot;

        if (!hadProfile)
            SetLocked(false);

        UpdateProfile();
    }


    CancellationTokenSource profileUpdateCts;
    public async void UpdateProfile()
    {
        profileUpdateCts?.Cancel();

        if (currentProfile is null)
            return;

        profileUpdateCts = new();
        var ct = profileUpdateCts.Token;

        if (await currentAcount.Authenticate() || ct.IsCancellationRequested)
            return;

        await currentProfile.Query();
        if (ct.IsCancellationRequested)
            return;

        var foundItem = currentProfile.GetItems(itemTypeFilter, predicateFilter).FirstOrDefault();
        ValidateItem(foundItem, true);
    }

    void SetLocked(bool value)
    {
        locked.Visible = value;
        buttonControl.Visible = !value;
        if (value)
        {
            entry.Visible = false;
            inspectArea.Visible = false;
            open.Visible = false;
            isEmpty = true;
        }
        else
            SetEmpty(isEmpty);
    }

    void SetEmpty(bool value)
    {
        isEmpty = value;
        entry.Visible = !isEmpty;
        inspectArea.Visible = !isEmpty && showInspector;
        open.Visible = isEmpty;
    }

    static bool FallbackPredicate(GameItem _) => true;
    public async Task SetSlotData(string profileId, string itemType, GameItemPredicate predicate)
    {
        profileType = profileId;
        itemTypeFilter = itemType;
        predicateFilter = predicate ?? FallbackPredicate;

        if (currentAcount is null || !await currentAcount.Authenticate())
            return;

        SetProfile(profileType is not null ? currentAcount?.GetProfile(profileType) : null);
    }

    void UpdateSlot(GameItem newItem)
    {
        if(predicateFilter is null)
            return;
        ValidateItem(newItem);
    }

    public void Inspect() => GameItemViewer.Instance.ShowItem(slottedItem);

    public void UpdateItem() => entry.UpdateItem();

    public void RequestChange() => OnItemChangeRequested?.Invoke(this);

    void ValidateItem(GameItem newItem, bool knownMatch = false)
    {
        bool isMatch = newItem is not null && newItem.profile is not null && (knownMatch || ((itemTypeFilter is null || newItem.template.Type == itemTypeFilter) && predicateFilter(newItem)));
        bool wasSlotted = newItem == slottedItem;

        if (isMatch)
        {
            if (newItem == slottedItem)
                return;
            slottedItem = newItem;
            entry.SetItem(slottedItem);
            SetEmpty(false);
            OnSlotItemChanged?.Invoke(this);
        }
        else if (wasSlotted) // item was slotted, but no longer matches
        {
            entry.ClearItem();
            slottedItem = null;
            SetEmpty(true);
            OnSlotItemChanged?.Invoke(this);
        }
    }

    public override void _ExitTree()
    {
        if (currentProfile is not null)
        {
            currentProfile.OnItemUpdated -= UpdateSlot;
            currentProfile.OnItemRemoved -= UpdateSlot;
        }
        base._ExitTree();
    }

}
