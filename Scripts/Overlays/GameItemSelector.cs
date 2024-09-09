using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static ProfileRequests;

public partial class GameItemSelector : ModalWindow, IRecyclableElementProvider<ProfileItemHandle>
{
    public static GameItemSelector Instance { get; private set; }

    [Signal]
    public delegate void TitleChangedEventHandler(string title);
    [Signal]
    public delegate void ButtonChangedEventHandler(string buttonText);
    [Signal]
    public delegate void AutoselectChangedEventHandler(Texture2D autoselect);

    [Export]
    Texture2D defaultSelectionMarker;
    [Export]
    Control autoSelectButton;
    [Export]
    RecycleListContainer container;
    [Export]
    Control multiselectButtons;

    public override void _Ready()
	{
		base._Ready();
        RestoreDefaults();
        container.SetProvider(this);
        Instance = this;
    }

    bool isSelecting;
    List<ProfileItemHandle> itemHandles;
    List<ProfileItemHandle> selectedHandles = new();

    public bool multiselectMode;
    public bool allowDeselect;
    public string overrideSurvivorSquad;
    public ProfileItemPredicate autoselectPredicate;

    public string titleText;
    public string confirmButtonText;
    public Texture2D autoselectButtonTex;
    public Color selectedTintColor;
    public Color collectionTintColor;
    public Texture2D selectedMarkerTex;
    public Texture2D collectionMarkerTex;


    public override void SetWindowOpen(bool openState)
    {
        if (isSelecting && !openState)
        {
            CancelSelection();
        }
    }

    public void RestoreDefaults()
    {
        multiselectMode = false;
        allowDeselect = false;
        overrideSurvivorSquad = null;
        autoselectPredicate = null;

        titleText = "Select an Item";
        confirmButtonText = "Confirm";
        autoselectButtonTex = null;
        selectedTintColor = Colors.Orange;
        selectedMarkerTex = defaultSelectionMarker;
        collectionTintColor = Colors.Green;
        collectionMarkerTex = defaultSelectionMarker;
    }

    public async Task<ProfileItemHandle[]> OpenSelector(ProfileItemPredicate filter)
    {
        var filteredHandles = (await ProfileRequests.GetProfileItems(FnProfiles.AccountItems, filter))
            .OrderBy(kvp => -kvp.Value.AsObject().GetItemRating(overrideSurvivorSquad))
            .Select(kvp => ProfileItemHandle.CreateHandleUnsafe(new(FnProfiles.AccountItems, kvp.Key)));
        var toReturn = await OpenSelector(filteredHandles, null);
        //foreach (var item in filteredHandles)
        //{
        //    if (!toReturn.Contains(item))
        //        item.Free();
        //}
        return toReturn;
    }

    public async Task<ProfileItemHandle[]> OpenSelector(IEnumerable<ProfileItemHandle> profileItems, IEnumerable<ProfileItemHandle> selectedItems = null)
    {
        EmitSignal(SignalName.TitleChanged, titleText);
        EmitSignal(SignalName.ButtonChanged, confirmButtonText);
        EmitSignal(SignalName.AutoselectChanged, autoselectButtonTex);
        multiselectButtons.Visible = multiselectMode;
        autoSelectButton.Visible = autoselectPredicate is not null;
        allowDeselect &= !multiselectMode;
        itemHandles = profileItems.ToList();
        if(allowDeselect)
            itemHandles.Insert(0, new());

        if (multiselectMode)
            selectedHandles = selectedItems?.ToList() ?? 
                (autoselectPredicate is not null ? 
                    itemHandles
                        .Where(handle => autoselectPredicate(handle.CreateKVPUnsafe()))
                        .ToList() : 
                    new());
        else
            selectedHandles = new();

        container.UpdateList(true);
        isSelecting = true;
        base.SetWindowOpen(true);
        await this.WaitForFrame();
        container.UpdateList(true);

        //GD.Print($"opening selector with {itemHandles.Count} items");
        while (isSelecting)
            await this.WaitForFrame();

        base.SetWindowOpen(false);
        //GD.Print($"closing with {selectedHandles.Count} selected items");
        var toReturn = selectedHandles.ToArray();
        selectedHandles.Clear();
        itemHandles.Clear();
        RestoreDefaults();

        return toReturn;
    }

    void AutoMarkSelection()
    {
        if (!multiselectMode || autoselectPredicate is null)
            return;
        selectedHandles = itemHandles.Where(handle => autoselectPredicate(handle.CreateKVPUnsafe())).ToList();
        container.UpdateList(true);
    }

    void ConfirmSelection()
    {
        isSelecting = false;
    }

    void CancelSelection()
    {
        selectedHandles.Clear();
        isSelecting = false;
    }

    void ClearSelection()
    {
        selectedHandles.Clear();
        if (multiselectMode)
            container.UpdateList(true);
    }

    public bool IndexIsSelected(int index) => multiselectMode && selectedHandles.Contains(itemHandles[index]);

    public void OnElementSelected(int index)
    {
        if (isSelecting)
        {
            if (multiselectMode)
            {
                if(selectedHandles.Contains(itemHandles[index]))
                    selectedHandles.Remove(itemHandles[index]);
                else
                    selectedHandles.Add(itemHandles[index]);
            }
            else
            {
                selectedHandles.Clear();
                selectedHandles.Add(itemHandles[index]);
                isSelecting = false;
            }
        }
    }

    public ProfileItemHandle GetRecycleElement(int index) => 
        ((itemHandles?.Count ?? -1) > 0 && index < itemHandles.Count && index >= 0) ? itemHandles[index] : null;

    public int GetRecycleElementCount() => itemHandles?.Count ?? 0;
}
