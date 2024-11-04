using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ProfileItemPredicate = ProfileRequests.ProfileItemPredicate;

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

    public event Action<InventoryItemSlot> OnItemChangeRequested;
    public event Action<InventoryItemSlot> OnItemChanged;

    public ProfileItemHandle slottedItem { get; private set; }
    ProfileItemPredicate predicate;
    bool isEmpty = true;

    public override void _Ready()
    {
        locked.Visible = true;
        entry.Visible = false;
        inspectArea.Visible = false;
        open.Visible = false;
        buttonControl.Visible = false;
        ProfileRequests.OnItemUpdated += UpdateSlot;
    }

    void SetEmpty(bool value)
    {
        isEmpty = value;
        entry.Visible = !isEmpty;
        inspectArea.Visible = !isEmpty && showInspector;
        open.Visible = isEmpty;
    }

    public async Task SetPredicate(ProfileItemPredicate newPredicate)
    {
        predicate = newPredicate;
        locked.Visible = false;
        open.Visible = isEmpty;
        buttonControl.Visible = true;

        var foundItem = (await ProfileRequests.GetProfileItems(FnProfileTypes.AccountItems, predicate)).FirstOrDefault();
        ValidateItem(new(FnProfileTypes.AccountItems, foundItem.Key), foundItem.Value?.AsObject());
    }

    public async void UpdateSlot(ProfileItemId profileItem)
    {
        if(predicate is null)
            return;
        ValidateItem(profileItem, await ProfileRequests.GetProfileItemInstance(profileItem));
    }

    public async void Inspect()
    {
        if(slottedItem?.isValid ?? false)
        {
            await GameItemViewer.Instance.ShowItemHandle(slottedItem);
        }
    }

    public void Refresh()
    {
        entry.RefreshProfileItem();
    }

    public void RequestChange()
    {
        OnItemChangeRequested?.Invoke(this);
        /*
        if (filter == null)
            return;
        var result = await GameItemSelector.Instance.OpenSelector(filter);
        var resultItem = result.FirstOrDefault();
        if (resultItem is null)
            return;
        GD.Print("changing slotted item to "+ resultItem.itemID.uuid);
        onChangeRequested?.Invoke(slottedItem, resultItem);
        */
    }

    void ValidateItem(ProfileItemId profileItem, JsonObject item)
    {
        bool isMatch = item is not null && predicate(new(profileItem.uuid, item));
        bool wasSlotted = slottedItem?.itemID == profileItem;

        bool changed = false;
        if (isMatch)
        {
            if (wasSlotted)
                return;
            //slottedItem?.Free();
            slottedItem = ProfileItemHandle.CreateHandleUnsafe(profileItem);
            entry.LinkProfileItem(slottedItem);
            SetEmpty(false);
            changed = true;
        }
        else if (wasSlotted)
        {
            entry.UnlinkProfileItem();
            //slottedItem.Free();
            slottedItem = null;
            SetEmpty(true);
            changed = true;
        }
        if (changed)
            OnItemChanged?.Invoke(this);
    }

    public override void _ExitTree()
    {
        //slottedItem?.Free();
        ProfileRequests.OnItemUpdated -= UpdateSlot;
        base._ExitTree();
    }

}
