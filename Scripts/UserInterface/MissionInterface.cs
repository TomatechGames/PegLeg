using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

public partial class MissionInterface : Control
{
	[Export]
	int operationsPerFrame = 10;

    [Export] NodePath missionViewOptionsPath;
    OptionButton missionViewOptions;

    [Export] NodePath zoneFilterTabBarPath;
	TabBar zoneFilterTabBar;

	[Export] NodePath searchBarPath;
	LineEdit searchBar;

    [Export] NodePath loadingIconPath;
	Control loadingIcon;

	[Export] NodePath forceRefreshButtonPath;
    BaseButton forceRefreshButton;

    [Export]
    PackedScene listEntryScene;
    [Export]
	PackedScene smallGridEntryScene;
    [Export]
    PackedScene largeGridEntryScene;

    [Export] NodePath missionGridPath;
	DynamicGridContainer missionGrid;

    [Export] NodePath missionListPath;
    VBoxContainer missionList;

    List<MissionEntry> prewarmedMissions = new();
	const int maxMissionCount = 370;

	static readonly string[] filters = new string[]
	{
		"spct",
        "spctv",
        "s",
        "p",
        "c",
        "t",
        "v",
    };

	//[Export] string testJSONPath;
	//[Export] string testAltJSONPath;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		this.GetNodeOrNull(forceRefreshButtonPath, out forceRefreshButton);

		missionViewOptions = GetNode<OptionButton>(missionViewOptionsPath);
        zoneFilterTabBar = GetNode<TabBar>(zoneFilterTabBarPath);
		searchBar = GetNode<LineEdit>(searchBarPath);
		loadingIcon = GetNode<Control>(loadingIconPath);

        missionGrid = GetNode<DynamicGridContainer>(missionGridPath);
		missionList = GetNode<VBoxContainer>(missionListPath);

		VisibilityChanged += () =>
        {
			if (Visible)
				LoadMissions();
        };

        TitleBarDragger.PerformRefresh += () =>
        {
            LoadMissions();
        };
        missionViewOptions.ItemSelected += e =>
        {
			cellsRequireRebuild = true;
			LoadMissions();
        };
        zoneFilterTabBar.TabChanged += e => LoadMissions();
        searchBar.TextSubmitted += e => LoadMissions();
		searchBar.TextChanged += e =>
		{
			if (string.IsNullOrWhiteSpace(e))
				LoadMissions();
		};
		forceRefreshButton.Pressed += () => LoadMissions(true);
    }

	bool listMode = false;
	async Task BuildCells()
    {
        for (int i = 0; i < prewarmedMissions.Count; i++)
        {
			prewarmedMissions[i].QueueFree();
            if (i % (operationsPerFrame/2) == 0)
                await this.WaitForFrame();
        }
        prewarmedMissions.Clear();
        await this.WaitForFrame();

        PackedScene missionEntry = missionViewOptions.Selected switch
		{
			0 => listEntryScene,
			1 => smallGridEntryScene,
			2 => largeGridEntryScene,
            _ => listEntryScene,
        };
		listMode = missionEntry == listEntryScene;
        Control destinationParent = listMode ? missionList : missionGrid;

        for (int i = 0; i < maxMissionCount; i++)
        {
            var missionTile = missionEntry.Instantiate<MissionEntry>();
            missionTile.Visible = false;
            destinationParent.AddChild(missionTile);
            prewarmedMissions.Add(missionTile);
			if (i % (operationsPerFrame / 2) == 0)
				await this.WaitForFrame();
        }
        cellsRequireRebuild = false;
    }

	bool cellsRequireRebuild = true;
	bool isLoadingMissions = false;
	async void LoadMissions(bool force = false)
	{
		if (isLoadingMissions)
            return;
        isLoadingMissions = true;
		if (MissionRequests.MissionsRequireUpdate() || cellsRequireRebuild || force)
        {
            loadingIcon.Visible = true;
            Control destinationParent = listMode ? missionList : missionGrid;
            destinationParent.Visible = false;

            if (cellsRequireRebuild)
				await BuildCells();
			ClearMissionGrid();
            await PopulateMissionGrid(await MissionRequests.GetMissions(force));

            destinationParent.Visible = false;
            loadingIcon.Visible = false;
        }
        isLoadingMissions = false;
        //searchBar.Text, filters[zoneFilterTabBar.CurrentTab], requireAllTermsButton.ButtonPressed
		FilterMissionGrid();
    }

	void ClearMissionGrid()
    {
        for (int i = 0; i < maxMissionCount; i++)
        {
			prewarmedMissions[i].Visible = false;
        }
    }

	async Task PopulateMissionGrid(JsonObject simplifiedMissions)
	{
		//ClearMissionGrid();
		if (simplifiedMissions is null)
			return;
		var missionArray = simplifiedMissions["missions"].AsArray();

		if (prewarmedMissions.Count < missionArray.Count)
			Debug.WriteLine(prewarmedMissions.Count + "<" + missionArray.Count);

        for (int i = 0; i < missionArray.Count; i++)
        {
            var mission = missionArray[i];
            var missionTile = prewarmedMissions[i];
            missionTile.ActivateEntry(mission.AsObject());
			if (i % operationsPerFrame == 0)
				await this.WaitForFrame();
        }

        for (int i = missionArray.Count; i < maxMissionCount; i++)
        {
            var missionTile = prewarmedMissions[i];
			missionTile.DeactivateEntry();
			//missionGrid.RemoveChild(missionTile);
        }
    }

	void FilterMissionGrid()
	{
		char[] theatreCatFilter = filters[zoneFilterTabBar.CurrentTab].ToCharArray();
		List<string> searchTerms = new(searchBar.Text.ToLower().Split());
		if (string.IsNullOrWhiteSpace(searchBar.Text))
			searchTerms.Clear();
		int minPowerLevel = 0;
		int maxPowerLevel = 999;

		string minPLTerm = searchTerms.FirstOrDefault(t=>t.StartsWith("min:"));
		if (minPLTerm is not null)
		{
			searchTerms.Remove(minPLTerm);
            if(!int.TryParse(minPLTerm.Split(":")[1], out minPowerLevel))
                minPowerLevel = 0;
        }

        string maxPLTerm = searchTerms.FirstOrDefault(t => t.StartsWith("max:"));
        if (maxPLTerm is not null)
        {
            searchTerms.Remove(maxPLTerm);
            if (!int.TryParse(maxPLTerm.Split(":")[1], out maxPowerLevel))
                maxPowerLevel = 999;
        }

		var searchTermsArray = searchTerms.ToArray();

        Control destinationParent = listMode ? missionList : missionGrid;
        missionGrid.SetDisableSort(true);
        destinationParent.Visible = false;
        for (int i = 0; i < maxMissionCount; i++)
        {
            prewarmedMissions[i].FilterEntry(theatreCatFilter, searchTermsArray, false, minPowerLevel, maxPowerLevel);
        }
        destinationParent.Visible = true;
        missionGrid.SetDisableSort(false);
    }
}
