using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionCollection : Node, IMissionHighlightProvider, IRecyclableElementProvider<GameMission>
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

    List<GameMission> filteredMissions = new();

    public GameMission GetRecycleElement(int index) => (index >= 0 && index < filteredMissions.Count) ? filteredMissions[index] : null;
    public int GetRecycleElementCount() => filteredMissions.Count;

    PLSearch.Instruction[] missionSearchInstructions = Array.Empty<PLSearch.Instruction>();
    PLSearch.Instruction[] itemSearchInstructions = Array.Empty<PLSearch.Instruction>();

    public override async void _Ready()
    {
        missionList.SetProvider(this);
        GameMission.OnMissionsUpdated += FilterMissions;
        GameMission.OnMissionsInvalidated += FilterMissions;
        EmitSignal(SignalName.NameChanged, testName);
        UpdateFilters();
        if (!await GameAccount.activeAccount.Authenticate())
            return;
        await GameMission.UpdateMissions();
    }

    public override void _ExitTree()
    {
        GameMission.OnMissionsUpdated -= FilterMissions;
        GameMission.OnMissionsInvalidated -= FilterMissions;
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

    public void FilterMissions()
	{
        filteredMissions =
            GameMission.currentMissions?
            .Where(MissionFilter)
            .ToList() ?? new();
        missionList.UpdateList(true);
    }
}
