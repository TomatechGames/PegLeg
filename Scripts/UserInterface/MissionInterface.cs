using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public partial class MissionInterface : Control, IRecyclableElementProvider<FnMission>
{
    [Export]
    VirtualTabBar zoneFilterTabBar;
    [Export]
	LineEdit searchBar;

    [Export]
    CheckButton[] itemFilterButtons = Array.Empty<CheckButton>();

    [Export]
    RecycleListContainer missionList;

    [Export]
	TextureProgressBar loadingIcon;

    static readonly string[] theaterFilters = new string[]
	{
		"spct",
        "spctv",
        "s",
        "p",
        "c",
        "t",
        "v",
    };

    static readonly string[][] itemFilters = new string[][]
    {
        new string[]
        {
            "Worker:*",
        },
        new string[]
        {
            "Schematic:*",
        },
        new string[]
        {
            "Hero:*",
            "Defender:*",
        },
        new string[] {
            "AccountResource:reagent_c_t01",
            "AccountResource:reagent_c_t02",
            "AccountResource:reagent_c_t03",
            "AccountResource:reagent_c_t04",
        },
        new string[] {
            "AccountResource:reagent_alteration_generic",
            "AccountResource:reagent_alteration_upgrade_uc", 
            "AccountResource:reagent_alteration_upgrade_r",
            "AccountResource:reagent_alteration_upgrade_vr",
            "AccountResource:reagent_alteration_upgrade_sr",
        },
        new string[] {
            "AccountResource:currency_mtxswap",
        },
    };


    public override void _Ready()
	{
        VisibilityChanged += () =>
        {
			if (IsVisibleInTree())
				LoadMissions();
            StartUpdateCheckTimer();
        };

        zoneFilterTabBar.TabChanged += e => FilterAllMissions();
        searchBar.TextSubmitted += e => FilterAllMissions();
		searchBar.TextChanged += e =>
		{
            //if (string.IsNullOrWhiteSpace(e))
            PrepareMissionFilter();
            FilterAllMissions();
		};
        foreach (var button in itemFilterButtons)
        {
            button.Pressed += () =>
            {
                PrepareMissionFilter();
                FilterAllMissions();
            };
        }
        PrepareMissionFilter();

        missionList.SetProvider(this);

        RefreshTimerController.OnDayChanged += OnDayChanged;
        if (IsVisibleInTree())
            LoadMissions();
        StartUpdateCheckTimer();
    }

    SceneTreeTimer refreshCooldown;
    void StartUpdateCheckTimer(bool force = false)
    {
        //GD.Print($"HOUR IS {DateTime.UtcNow.Hour}");
        if (force)
        {
            if (refreshCooldown is not null)
                refreshCooldown.Timeout -= CheckForUpdate;
            refreshCooldown = null;
        }
        var now = DateTime.UtcNow;
        if (now.Hour < 1 && (refreshCooldown?.TimeLeft ?? 0) <= 0)
        {
            refreshCooldown = GetTree().CreateTimer(now.Minute < 10 ? 15 : 90);
            refreshCooldown.Timeout += CheckForUpdate;
        }
    }

    public async void CheckForUpdate()
    {
        StartUpdateCheckTimer();
        if (!await MissionRequests.CheckForMissionChanges())
            return;
        GD.Print("DOUBLE RESET");
        if (IsVisibleInTree())
            LoadMissions(true);
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnDayChanged -= OnDayChanged;
    }

    private void OnDayChanged()
    {
        if (IsVisibleInTree())
            LoadMissions();
        StartUpdateCheckTimer();
    }

    List<FnMission> allMissions = new();
    List<FnMission> activeMissions = new();

    public FnMission GetRecycleElement(int index) => index >= 0 && index < activeMissions.Count ? activeMissions[index] : null;
    public int GetRecycleElementCount() => activeMissions.Count;

    public void ForceReloadMissions()
    {
        LoadMissions(true);
        StartUpdateCheckTimer(true);
    }

    static SemaphoreSlim missionInterfaceSephamore = new(1);
    async void LoadMissions(bool force = false)
    {
        await missionInterfaceSephamore.WaitAsync();
        try
        {
            missionList.Visible = false;
            loadingIcon.Visible = true;

            var account = GameAccount.activeAccount;
            if (!await account.Authenticate())
                return;

            if (MissionRequests.MissionsEmptyOrOutdated() || force || allMissions.Count == 0)
            {
                allMissions = (await MissionRequests.GetMissions(force)).ToList();
                allMissions = allMissions
                    .OrderBy(m => m.powerLevel)
                    .ThenBy(m => m.theaterIdx)
                    .ThenBy(m => m.missionGenerator.template?.DisplayName ?? "AAAAA")
                    .ToList();
            }

            FilterAllMissions();
        }
        finally
        {
            missionList.Visible = true;
            loadingIcon.Visible = false;
            missionInterfaceSephamore.Release();
        }
        if(MissionRequests.MissionsEmptyOrOutdated())
            LoadMissions();
    }

    string theaterFilter => theaterFilters[zoneFilterTabBar.CurrentTab];
    PLSearch.Instruction[] missionSearchInstructions = Array.Empty<PLSearch.Instruction>();
    PLSearch.Instruction[] itemSearchInstructions = Array.Empty<PLSearch.Instruction>();
    string[] extraItemFilters = Array.Empty<string>();
    void PrepareMissionFilter()
    {
        var searchText = searchBar.Text;
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

        List<string> extraItemFilterList = new();
        for (int i = 0; i < itemFilters.Length; i++)
        {
            if (itemFilterButtons.Length > i && itemFilterButtons[i].ButtonPressed)
            {
                extraItemFilterList.AddRange(itemFilters[i]);
            }
        }
        extraItemFilters = extraItemFilterList.ToArray();
    }

    static bool MatchItemOrEquivelent(GameItem item, PLSearch.Instruction[] itemInstructions, string[] extraItemFilters) =>
        MatchItem(item, itemInstructions, extraItemFilters) ||
        (
            item.zcpEquivelent is not null &&
            MatchItem(item.zcpEquivelent, itemInstructions, extraItemFilters)
        );
    static bool MatchItem(GameItem item, PLSearch.Instruction[] itemInstructions, string[] extraItemFilters)
    {
        bool matchesItemFilters = extraItemFilters.Length == 0;
        foreach (var itemFilter in extraItemFilters)
        {
            matchesItemFilters = item.templateId.Contains(itemFilter);
            if (itemFilter.EndsWith("*"))
                matchesItemFilters = item.templateId.StartsWith(itemFilter[..^1]);

            if (itemFilter == "MYTHICLEAD")
                matchesItemFilters = Regex.Match(item.templateId, "Worker:manager\\w+_sr_\\w*").Success;

            if (matchesItemFilters)
                break;
        }
        if (!matchesItemFilters)
            return false;
        matchesItemFilters = PLSearch.EvaluateInstructions(itemInstructions, item.RawData); //search item instructions
        return matchesItemFilters;
    }

    bool MissionFilter(FnMission mission)
    {
        if (!theaterFilter.Contains(mission.theaterCat[0]))
            return false;

        var currentItemInstructions = itemSearchInstructions;
        if (!PLSearch.EvaluateInstructions(missionSearchInstructions, mission.missionData))
        {
            if (currentItemInstructions.Length == 0)
                currentItemInstructions = missionSearchInstructions;
            else
                return false;
        }

        return mission.allItems.Any(item => MatchItemOrEquivelent(item, currentItemInstructions, extraItemFilters));
    }

    void FilterAllMissions()
	{
        activeMissions = allMissions
            .Where(MissionFilter).
            ToList();
        missionList.UpdateList(true);
    }
}

