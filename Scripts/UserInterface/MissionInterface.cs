using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

public partial class MissionInterface : Control, IRecyclableElementProvider<GameMission>
{
    #region Statics
    static MissionInterface instance;

    static NotificationData _unexpectedResetNotif;
    static NotificationData unexpectedResetNotif => _unexpectedResetNotif ??= new()
    {
        header = "Unexpected Reset Detected",
        icon = instance?.unexpectedResetNotifIcon,
        sound = instance?.unexpectedResetSound,
        color = Color.FromHtml("#ff5555"),
    };

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
            "AccountResource:voucher_cardpack_bronze",
        },
    };
    #endregion

    public static void SearchInMissions(string searchText, bool showMissionTab = false)
    {
        instance.searchBar.Text = searchText;
        instance.UpdateFilters();
        if (showMissionTab)
        {
            instance.Visible = true;
            instance.FilterMissions();
        }
    }

    [Export]
    Texture2D unexpectedResetNotifIcon;
    [Export]
    AudioStream unexpectedResetSound;

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

    List<GameMission> filteredMissions = [];
    public GameMission GetRecycleElement(int index) => index >= 0 && index < filteredMissions.Count ? filteredMissions[index] : null;
    public int GetRecycleElementCount() => filteredMissions.Count;

    public override void _Ready()
    {
        instance = this;
        missionList.Visible = false;
        loadingIcon.Visible = true;
        searchBar.Text = AppConfig.Get("missions", "default_search", "");

        VisibilityChanged += () =>
        {
            if (needsFilter)
                FilterMissions();
        };

        zoneFilterTabBar.TabChanged += e => FilterMissions();
        searchBar.TextSubmitted += e => UpdateFilters();
		searchBar.TextChanged += e => UpdateFilters();
        foreach (var button in itemFilterButtons)
        {
            button.Pressed += UpdateFilters;
        }
        UpdateFilters();

        missionList.SetProvider(this);

        RefreshTimerController.OnDayChanged += ForceReloadMissions;
        RefreshTimerController.OnDayChanged += StartUpdateCheckTimer;

        GameMission.OnMissionsUpdated += OnMissionsUpdated;
        GameMission.OnMissionsInvalidated += OnMissionsInvalidated;

        GameAccount.ActiveAccountChanged += ForceReloadMissions;

        GameMission.CheckMissions().StartTask();
        StartUpdateCheckTimer();
    }

    public override void _ExitTree()
    {
        RefreshTimerController.OnDayChanged -= ForceReloadMissions;
        RefreshTimerController.OnDayChanged -= StartUpdateCheckTimer;

        GameMission.OnMissionsUpdated -= OnMissionsUpdated;
        GameMission.OnMissionsInvalidated -= OnMissionsInvalidated;
    }

    CancellationTokenSource updateCheckCTS = new();
    async void StartUpdateCheckTimer()
    {
        if (!AppConfig.Get("missions", "reset_detection", true))
            return;
        GD.Print("Starting update check timer");
        updateCheckCTS.CancelAndRegenerate(out var ct);
        while (true)
        {
            var now = DateTime.UtcNow;
            if (now.Hour > 1 || !AppConfig.Get("missions", "reset_detection", true))
            {
                GD.Print("Ending update check timer");
                return;
            }
            int duration = now.Minute < 10 ? 15 : 90;
            while (duration > 0)
            {
                await Helpers.WaitForTimer(1);
                if (ct.IsCancellationRequested || !AppConfig.Get("missions", "reset_detection", true))
                    return;
                duration--;
            }
            if (await GameMission.MissionsNeedUpdate() && GameMission.currentMissions is not null)
            {
                GD.Print("Unexpected reset detected");
                NotificationManager.PushNotification(unexpectedResetNotif);
                await GameMission.UpdateMissions();
            }
        }
    }

    public async void FakeUnexpectedReset()
    {
        await Helpers.WaitForTimer(1);
        NotificationManager.PushNotification(unexpectedResetNotif);
    }

    void OnMissionsInvalidated()
    {
        missionList.Visible = false;
        loadingIcon.Visible = true;
    }

    void OnMissionsUpdated()
    {
        missionList.Visible = true;
        loadingIcon.Visible = false;
        FilterMissions();
    }

    public void ForceReloadMissions() => GameMission.UpdateMissions().StartTask();
    public void ReloadMissions() => GameMission.CheckMissions().StartTask();

    public async void LoadMissions(bool force = false)
    {

        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return;

        if (force || await GameMission.MissionsNeedUpdate(true))
            await GameMission.UpdateMissions();
    }

    PLSearch.Instruction[] missionSearchInstructions = Array.Empty<PLSearch.Instruction>();
    PLSearch.Instruction[] itemSearchInstructions = Array.Empty<PLSearch.Instruction>();
    string[] extraItemFilters = Array.Empty<string>();
    void UpdateFilters()
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

        List<string> extraItemFilterList = [];
        for (int i = 0; i < itemFilters.Length; i++)
        {
            if (itemFilterButtons.Length > i && itemFilterButtons[i].ButtonPressed)
            {
                extraItemFilterList.AddRange(itemFilters[i]);
            }
        }
        extraItemFilters = extraItemFilterList.ToArray();
        FilterMissions();
    }

    public static bool MatchItemOrEquivelent(GameItem item, PLSearch.Instruction[] itemInstructions, string[] extraItemFilters = null) =>
        MatchItem(item, itemInstructions, extraItemFilters) ||
        (
            item.zcpEquivelent is not null &&
            MatchItem(item.zcpEquivelent, itemInstructions, extraItemFilters)
        );

    public static bool MatchItem(GameItem item, PLSearch.Instruction[] itemInstructions, string[] extraItemFilters = null)
    {
        extraItemFilters ??= Array.Empty<string>();
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

    string theaterFilter => theaterFilters[zoneFilterTabBar.CurrentTab];
    bool MissionFilter(GameMission mission)
    {
        if (!theaterFilter.Contains(mission.TheaterCat[0]))
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

    bool needsFilter = false;
    void FilterMissions()
    {
        needsFilter = true;
        if (!IsVisibleInTree())
            return;
        needsFilter = false;
        filteredMissions = GameMission.currentMissions?
            .Where(MissionFilter)
            .ToList() ?? [];
        missionList.UpdateList(true);
    }
}

