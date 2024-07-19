using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ProfileItemPredicate = ProfileRequests.ProfileItemPredicate;

public partial class GameItemSelector : ModalWindow, IRecyclableElementProvider<ProfileItemHandle>
{
    public static GameItemSelector Instance { get; private set; }

    [Export]
    protected RecycleListContainer container;

    public override void _Ready()
	{
		base._Ready();
        container.SetProvider(this);
		LinkInstance();
	}

	protected virtual void LinkInstance() => Instance = this;

    List<ProfileItemHandle> filteredHandles = null;
    bool isSelecting = false;
    ProfileItemHandle selectedChoice;

    public override void SetWindowOpen(bool openState)
    {
        base.SetWindowOpen(openState);
        isSelecting &= openState;
    }

    public async Task<ProfileItemHandle> OpenSelector(ProfileItemPredicate filter)
    {
        filteredHandles = (await ProfileRequests.GetProfileItems(FnProfiles.AccountItems, filter))
            .OrderBy(kvp=>-kvp.Value.AsObject().GetItemRating())
            .Select(kvp=>ProfileItemHandle.CreateHandleUnsafe(new(FnProfiles.AccountItems, kvp.Key))).ToList();
        container.UpdateList(true);
        selectedChoice?.Unlink();
        selectedChoice = null;
        isSelecting = true;
        SetWindowOpen(true);
        GD.Print($"opening with {filteredHandles.Count} items");
        while (isSelecting)
            await this.WaitForFrame();
        foreach (var item in filteredHandles)
        {
            if (item != selectedChoice)
                item.Unlink();
        }
        SetWindowOpen(false);
        GD.Print("closing");
        return selectedChoice;
    }

    public void SelectEmpty()
    {
        if (isSelecting)
        {
            selectedChoice = new();
            isSelecting = false;
        }
    }

    public void OnElementSelected(int index)
    {
        if (isSelecting)
        {
            selectedChoice = filteredHandles[index];
            isSelecting = false;
        }
    }

    public ProfileItemHandle GetRecycleElement(int index) => 
        ((filteredHandles?.Count ?? -1) > 0 && index < filteredHandles.Count && index >= 0) ? filteredHandles[index] : null;

    public int GetRecycleElementCount() => filteredHandles?.Count ?? 0;
}
