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
    Control open;

    [Export]
    Control locked;

    [Export]
    bool useSurvivorBoosts = false;

    public event Action<ProfileItemHandle> OnSlottedItemChanged;

    public override void _Ready()
    {
        entry.useSurvivorBoosts = useSurvivorBoosts;
        ProfileRequests.OnItemUpdated += UpdateSlot;
    }

    ProfileItemPredicate predicate;
    public async void SetPredicate(ProfileItemPredicate newPredicate)
    {
        predicate = newPredicate;
        var foundItem = (await ProfileRequests.GetProfileItems(FnProfiles.AccountItems, predicate)).FirstOrDefault();
        locked.Visible = false;
        ValidateItem(new(FnProfiles.AccountItems, foundItem.Key), foundItem.Value?.AsObject());
    }

    ProfileItemPredicate filter;
    public void SetFilter(ProfileItemPredicate newFilter) => filter = newFilter;

    ProfileItemHandle slottedItem;

    public async Task<JsonObject> GetSlottedItem() => slottedItem?.isValid ?? false ? await slottedItem?.GetItem() : null;

    public async void UpdateSlot(ProfileItemId profileItem)
    {
        if(predicate is null)
        {
            entry.Visible = false;
            open.Visible = false;
            locked.Visible = true;
            return;
        }
        locked.Visible = false;

        ValidateItem(profileItem, await ProfileRequests.GetProfileItemInstance(profileItem));
    }

    public async void Inspect()
    {
        if(slottedItem?.isValid ?? false)
        {
            await GameItemViewer.Instance.LinkItem(slottedItem);
        }
    }

    public void Relink()
    {
        if (slottedItem?.isValid ?? false)
        {
            entry.LinkProfileItem(slottedItem);
        }
    }

    public void SetChangeAction(Action<ProfileItemHandle, ProfileItemHandle> newAction) => onChangeRequested = newAction;
    Action<ProfileItemHandle, ProfileItemHandle> onChangeRequested;
    public async void ChangeItem()
    {
        if (filter == null)
            return;
        var result = await GameItemSelector.Instance.OpenSelector(filter);
        if (result is null)
            return;
        GD.Print("changing slotted item to "+result.profileItem.uuid);
        onChangeRequested?.Invoke(slottedItem, result);
    }

    void ValidateItem(ProfileItemId profileItem, JsonObject item)
    {
        bool isMatch = item is not null && predicate(new(profileItem.uuid, item));
        bool wasSlotted = slottedItem?.profileItem == profileItem;

        if (isMatch)
        {
            if (wasSlotted)
                return;
            slottedItem?.Unlink();
            slottedItem = ProfileItemHandle.CreateHandleUnsafe(profileItem);
            entry.LinkProfileItem(slottedItem);
            entry.Visible = true;
            open.Visible = false;
        }
        else if (wasSlotted)
        {
            entry.UnlinkProfileItem();
            slottedItem = null;
            entry.Visible = false;
            open.Visible = true;
        }
        OnSlottedItemChanged?.Invoke(slottedItem);
    }

    public override void _ExitTree()
    {
        slottedItem?.Unlink();
        base._ExitTree();
    }

}
