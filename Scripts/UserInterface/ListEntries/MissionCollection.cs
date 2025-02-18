using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionCollection : Control, IMissionHighlightProvider, IRecyclableElementProvider<GameMission>
{
    public event Action OnHighlightedItemFilterChanged;
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Export]
	string testName;
	[Export]
	string testSearch;
    [Export]
    RecycleListContainer missionList;
    [Export]
    Control loadingIcon;
    [Export]
    bool sortByPower;
    [Export]
    bool sortByZoneCat;

    List<GameMission> filteredMissions = new();

    public GameMission GetRecycleElement(int index) => (index >= 0 && index < filteredMissions.Count) ? filteredMissions[index] : null;
    public int GetRecycleElementCount() => filteredMissions.Count;

    PLSearch.Instruction[] missionSearchInstructions = Array.Empty<PLSearch.Instruction>();
    PLSearch.Instruction[] itemSearchInstructions = Array.Empty<PLSearch.Instruction>();

    public override async void _Ready()
    {
        missionList.SetProvider(this);
        GameMission.OnMissionsUpdated += FilterMissions;
        GameMission.OnMissionsInvalidated += ClearMissions;
        VisibilityChanged += TryFilterMissions;
        EmitSignal(SignalName.NameChanged, testName);
        UpdateFilters();
        if (!await GameAccount.activeAccount.Authenticate())
            return;
        ClearMissions();
        await GameMission.UpdateMissions();
    }

    public override void _ExitTree()
    {
        GameMission.OnMissionsUpdated -= FilterMissions;
        GameMission.OnMissionsInvalidated -= ClearMissions;
    }

    public void GoToSearch()
    {
        MissionInterface.SearchInMissions(testSearch, true);
    }

    public void SetSortByPower(bool val)
    {
        sortByPower = val;
        FilterMissions();
    }

    public void OnElementSpawned(IRecyclableEntry entry)
    {
        if (entry is not MissionEntry missionEntry) 
            return;
        missionEntry.SetHighlightProvider(this);
    }

    public void UpdateFilters()
	{
        var searchText = testSearch;
        if (searchText.Contains("///"))
        {
            string[] splitSearchText = searchText.Split("///");
            missionSearchInstructions = PLSearch.GenerateSearchInstructions(splitSearchText[0]) ?? Array.Empty<PLSearch.Instruction>();
            itemSearchInstructions = PLSearch.GenerateSearchInstructions(splitSearchText[1..].Join()) ?? Array.Empty<PLSearch.Instruction>();
        }
        else
        {
            missionSearchInstructions = PLSearch.GenerateSearchInstructions(searchText) ?? Array.Empty<PLSearch.Instruction>();
            itemSearchInstructions = Array.Empty<PLSearch.Instruction>();
        }
        OnHighlightedItemFilterChanged?.Invoke();
    }

    bool MissionFilter(GameMission mission)
    {
        if (!PLSearch.EvaluateInstructions(missionSearchInstructions, mission.missionData))
            return false;

        return mission.allItems.Any(item => MissionInterface.MatchItemOrEquivelent(item, itemSearchInstructions));
    }

    public Predicate<GameItem> HighlightedItemFilter => ItemFilter;
    bool ItemFilter(GameItem item) => itemSearchInstructions.Length > 0 && MissionInterface.MatchItemOrEquivelent(item, itemSearchInstructions);
    public void ClearMissions()
    {
        loadingIcon.Visible = true;
        missionList.Visible = false;
    }

    void TryFilterMissions()
    {
        if (needsUpdate)
            FilterMissions();
    }

    bool needsUpdate = false;

    public void FilterMissions()
    {
        loadingIcon.Visible = false;
        missionList.Visible = true;

        if (!IsVisibleInTree())
        {
            needsUpdate = true;
            return;
        }
        needsUpdate = false;

        var sortedMissions =
            GameMission.currentMissions?
            .Where(MissionFilter).OrderBy(m=>1) ?? default;

        if (sortByZoneCat)
        {
            sortedMissions = sortedMissions
                .ThenBy(m => m.PowerLevel != 160)
                .ThenBy(m => m.TheaterCat switch
                {
                    "v" => -5,
                    "t" => -4,
                    "c" => -3,
                    "p" => -2,
                    "s" => -1,
                    _ => 0
                })
                .ThenBy(m => -m.PowerLevel);
        }
        else if (sortByPower)
        {
            sortedMissions = sortedMissions
                .ThenBy(m => -m.PowerLevel);
        }
        else
        {
            sortedMissions = sortedMissions
                .ThenBy(m => m.allItems
                    .Where(ItemFilter)
                    .Select(i => -i.sortingTemplate.RarityLevel * i.quantity)
                    .Sum()
                );
        }

        filteredMissions = sortedMissions.ToList();

        missionList.UpdateList(true);
    }
}
