using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static ProfileRequests;

public partial class GameItemSelector : ModalWindow, IRecyclableElementProvider<ProfileItemHandle>
{
    public static GameItemSelector Instance { get; private set; }

    [Signal]
    public delegate void TitleChangedEventHandler(string title);
    [Signal]
    public delegate void ConfirmButtonChangedEventHandler(string buttonText);
    [Signal]
    public delegate void SkipButtonChangedEventHandler(string buttonText);
    [Signal]
    public delegate void AutoselectChangedEventHandler(Texture2D autoselect);
    [Signal]
    public delegate void SortTypeChangedEventHandler(string title);

    [Export]
    Texture2D defaultSelectionMarker;
    [Export]
    Texture2D recycleIcon;
    [Export]
    Texture2D collectionIcon;
    [Export]
    Control autoSelectButton;
    [Export]
    RecycleListContainer container;
    [Export]
    Control multiselectButtons;
    [Export]
    Control confirmButton;
    [Export]
    Control skipButton;

    public override void _Ready()
	{
		base._Ready();
        RestoreDefaults();
        container.SetProvider(this);
        Instance = this;
    }

    bool isSelecting;
    bool isCancelling;
    List<ProfileItemHandle> itemHandles;
    List<ProfileItemHandle> selectedHandles = new();

    public bool multiselectMode;
    public bool allowEmptySelection;
    public bool allowCancel;
    public string overrideSurvivorSquad;
    public ProfileItemPredicate autoselectPredicate;

    public string titleText;
    public string confirmButtonText;
    public string skipButtonText;
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
        allowEmptySelection = false;
        allowCancel = true;
        overrideSurvivorSquad = null;
        autoselectPredicate = null;

        titleText = "Select an Item";
        confirmButtonText = "Confirm";
        skipButtonText = "Continue";
        autoselectButtonTex = null;
        selectedTintColor = Colors.Orange;
        selectedMarkerTex = defaultSelectionMarker;
        collectionTintColor = Colors.Green;
        collectionMarkerTex = defaultSelectionMarker;
    }

    public void SetRecycleDefaults()
    {
        var instructions = PLSearch.GenerateSearchInstructions("template.RarityLv=..3 | (template.RarityLv=..4 !templateId=\"Worker\"..)", out var _);
        ProfileItemPredicate autoRecycleFilter = kvp => PLSearch.EvaluateInstructions(instructions, kvp.Value);
        RestoreDefaults();
        titleText = "Recycle";
        confirmButtonText = "Confirm Recycle";
        multiselectMode = true;
        selectedMarkerTex = recycleIcon;
        selectedTintColor = Colors.Red;
        collectionMarkerTex = collectionIcon;
        autoselectButtonTex = recycleIcon;

        autoselectPredicate = autoRecycleFilter;
    }

    public async Task<ProfileItemHandle[]> OpenSelector(ProfileItemPredicate filter)
    {
        var filteredHandles = (await GetProfileItems(FnProfileTypes.AccountItems, filter))
            .Select(kvp => ProfileItemHandle.CreateHandleUnsafe(new(FnProfileTypes.AccountItems, kvp.Key)));
        return await OpenSelector(filteredHandles, null);
    }

    ProfileItemHandle emptyHandle = new();
    public async Task<ProfileItemHandle[]> OpenSelector(IEnumerable<ProfileItemHandle> profileItems, IEnumerable<ProfileItemHandle> selectedItems = null)
    {
        EmitSignal(SignalName.TitleChanged, titleText);
        EmitSignal(SignalName.ConfirmButtonChanged, confirmButtonText);
        EmitSignal(SignalName.SkipButtonChanged, skipButtonText);
        EmitSignal(SignalName.AutoselectChanged, autoselectButtonTex);

        multiselectButtons.Visible = multiselectMode;
        autoSelectButton.Visible = autoselectPredicate is not null;

        itemHandles = profileItems.ToList();
        if(allowEmptySelection && !multiselectMode)
            itemHandles.Insert(0, emptyHandle);

        if (multiselectMode)
        {
            selectedHandles = selectedItems?.ToList() ??
                (autoselectPredicate is not null ?
                    itemHandles
                        .Where(handle => autoselectPredicate(handle.CreateKVPUnsafe()))
                        .ToList() :
                    new());
        }
        else
            selectedHandles = new();

        confirmButton.Visible = selectedHandles?.Any() ?? false;
        skipButton.Visible = !confirmButton.Visible && allowEmptySelection;

        SetSort(0);
        SortItems();
        container.UpdateList(true);
        isSelecting = true;
        isCancelling = false;
        base.SetWindowOpen(true);
        await this.WaitForFrame();
        container.UpdateList(true);

        //GD.Print($"opening selector with {itemHandles.Count} items");
        while (isSelecting)
            await this.WaitForFrame();

        base.SetWindowOpen(false);
        //GD.Print($"closing with {selectedHandles.Count} selected items");
        if(selectedHandles.Contains(emptyHandle))
            selectedHandles.Remove(emptyHandle);
        var toReturn = selectedHandles.ToArray();
        selectedHandles.Clear();
        itemHandles.Clear();
        RestoreDefaults();

        return isCancelling ? null : toReturn;
    }

    void AutoMarkSelection()
    {
        if (!multiselectMode || autoselectPredicate is null)
            return;
        selectedHandles = itemHandles.Where(handle => autoselectPredicate(handle.CreateKVPUnsafe())).Union(selectedHandles).ToList();
        SortItems();
        container.UpdateList(true);
    }

    int currentSortingIndex = 0;
    bool sortingDirty = false;
    delegate IOrderedEnumerable<ProfileItemHandle> SortingFunc(IOrderedEnumerable<ProfileItemHandle> handles);
    SortingFunc[] sortingFunctions;
    string[] sortingFunctionNames = new string[]
    {
        "By Power",
        "By Power (rev)",
        "By Name"
    };
    void CycleSort()
    {
        if (!sortingDirty)
        {
            currentSortingIndex++;
            if (currentSortingIndex == sortingFunctions.Length)
                currentSortingIndex = 0;
            SetSort(currentSortingIndex);
        }
        sortingDirty = false;
        SortItems();
        container.UpdateList(true);
    }

    void SetSort(int newIndex)
    {
        sortingFunctions ??= new SortingFunc[]
        {
            SortByPower,
            SortByPowerAsc,
            SortByName
        };
        currentSortingIndex = newIndex % Mathf.Max(sortingFunctions.Length, sortingFunctionNames.Length);
        currentSortingFunction = sortingFunctions[currentSortingIndex];
        EmitSignal(SignalName.SortTypeChanged, sortingFunctionNames[currentSortingIndex]);
    }

    IOrderedEnumerable<ProfileItemHandle> SortByPower(IOrderedEnumerable<ProfileItemHandle> handles) => 
        handles.ThenBy(handle => -handle.GetItemUnsafe().GetItemRating(overrideSurvivorSquad));
    IOrderedEnumerable<ProfileItemHandle> SortByPowerAsc(IOrderedEnumerable<ProfileItemHandle> handles) =>
        handles.ThenBy(handle => handle.GetItemUnsafe().GetItemRating(overrideSurvivorSquad));
    IOrderedEnumerable<ProfileItemHandle> SortByName(IOrderedEnumerable<ProfileItemHandle> handles) => 
        handles.ThenBy(handle => handle.GetItemUnsafe().GetTemplate()["DisplayName"].ToString());

    SortingFunc currentSortingFunction;

    void SortItems()
    {
        var presortedItemHandles = itemHandles.OrderBy(handle => handle.isValid ? 1 : 0).ThenBy(handle => selectedHandles.Contains(handle) ? 0 : 1);
        itemHandles = currentSortingFunction(presortedItemHandles).ToList();
    }

    void ConfirmSelection()
    {
        isSelecting = false;
    }

    void CancelSelection()
    {
        if (!allowCancel)
            return;
        selectedHandles.Clear();
        isCancelling = true;
        isSelecting = false;
    }

    void ClearSelection()
    {
        selectedHandles.Clear();
        SortItems();
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
                sortingDirty = true;
                bool empty = selectedHandles.Count == 0;
                confirmButton.Visible = !empty;
                skipButton.Visible = empty && allowEmptySelection;
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
