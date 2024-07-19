using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

public partial class MissionInterface : Control
{
    [Export]
    VirtualTabBar zoneFilterTabBar;
    [Export]
	LineEdit searchBar;
    [Export]
    Button itemSearchToggle;

    [Export]
    CheckButton[] itemFilterButtons = Array.Empty<CheckButton>();

    [Export]
    PackedScene missionRowScene;
    [Export]
    Control missionRowParent;

    [Export]
    PackedScene missionEntryScene;

    [Export]
	Control loadingIcon;

    string currentZoneFilter => theaterFilters[zoneFilterTabBar.CurrentTab];

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
        new string[] {
            "AccountResource:reagent_alteration_generic",
            "AccountResource:reagent_alteration_upgrade_uc", 
            "AccountResource:reagent_alteration_upgrade_r",
            "AccountResource:reagent_alteration_upgrade_vr",
            "AccountResource:reagent_alteration_upgrade_sr",
        },
        new string[] {
            "AccountResource:reagent_c_t01",
            "AccountResource:reagent_c_t02",
            "AccountResource:reagent_c_t03",
            "AccountResource:reagent_c_t04",
        },
        new string[] {
            "Ingredient:ingredient_ore_copper",
            "Ingredient:ingredient_ore_silver",
            "Ingredient:ingredient_ore_malachite",
            "Ingredient:ingredient_ore_obsidian",
            "Ingredient:ingredient_ore_brightcore",

            "Ingredient:ingredient_crystal_quartz",
            "Ingredient:Ingredient:ingredient_crystal_shadowshard",
            "Ingredient:ingredient_crystal_sunbeam",
        },
        new string[]
        {
            "Schematic:*",
        },
        new string[]
        {
            "Hero:*",
        },
        new string[]
        {
            "Worker:workerbasic_sr_t01",
            "MYTHICLEAD",
        }
    };


    public override void _Ready()
	{
		VisibilityChanged += () =>
        {
			if (Visible)
				LoadMissions();
        };

        zoneFilterTabBar.TabChanged += e => FilterMissionGrid();
        searchBar.TextSubmitted += e => FilterMissionGrid();
		searchBar.TextChanged += e =>
		{
            //if (string.IsNullOrWhiteSpace(e))
            //GenerateSearchInstructions(searchBar.Text);
            FilterMissionGrid();
		};
        foreach (var button in itemFilterButtons)
        {
            button.Pressed += () =>
            {
                GenerateSearchFilters();
                FilterMissionGrid();
            };
        }
        //GenerateSearchInstructions(searchBar.Text);
        GenerateSearchFilters();

        if (Visible)
            LoadMissions();
    }

    List<MissionRowGroup> activeRowGroups = new();
    record MissionRowGroup(MissionRow row, MissionEntry[] entries);
    PLSearch.Instruction[] currentMissionSearchInstructions;
    PLSearch.Instruction[] currentItemSearchInstructions;
    List<string> activeItemFilters = new();

    bool isLoadingMissions = false;
    async void LoadMissions(bool force = false)
    {
        if (isLoadingMissions || !await LoginRequests.TryLogin())
            return;
        try
        {
            isLoadingMissions = true;

            missionRowParent.Visible = false;
            if (MissionRequests.MissionsRequireUpdate() || force)
            {
                loadingIcon.Visible = true;
                foreach (var rowGroup in activeRowGroups)
                {
                    rowGroup.row.QueueFree();
                }
                activeRowGroups.Clear();
                await this.WaitForFrame();

                var allMissionData = await MissionRequests.GetMissions(force);
                loadingIcon.Visible = false;
                missionRowParent.Visible = true;
                var instructions = PLSearch.GenerateSearchInstructions(searchBar.Text, out var _);

                List<MissionEntry> currentMissions = new();
                foreach (var rowData in allMissionData)
                {
                    int i = 0;
                    var thisRow = missionRowScene.Instantiate<MissionRow>();
                    missionRowParent.AddChild(thisRow);
                    thisRow.SetName("Power Level "+rowData.Key);
                    thisRow.Visible = false;
                    foreach (var missionData in rowData.Value.AsArray())
                    {
                        var thisMission = missionEntryScene.Instantiate<MissionEntry>();
                        thisMission.SetMissionData(missionData.AsObject());
                        thisRow.missionParent.AddChild(thisMission);
                        currentMissions.Add(thisMission);
                        i++;
                        if (i % 10 == 9)
                            await this.WaitForFrame();
                    }
                    thisRow.Visible = false;
                    foreach (var missionEntry in currentMissions)
                    {
                        missionEntry.Visible = missionEntry.Filter(currentMissionSearchInstructions, currentItemSearchInstructions, itemSearchToggle.ButtonPressed, currentZoneFilter, activeItemFilters.ToArray());
                        thisRow.Visible |= missionEntry.Visible;
                    }

                    activeRowGroups.Add(new(thisRow, currentMissions.ToArray()));
                    currentMissions.Clear();
                    await this.WaitForFrame();
                }
            }
        }
        finally
        {
            loadingIcon.Visible = false;
            isLoadingMissions = false;
        }

        //FilterMissionGrid();
    }

    void GenerateSearchInstructions(string searchText)
    {
        if (searchText.Contains(" i/: "))
        {
            string[] splitSearchText = searchText.Split("i/:");
            currentMissionSearchInstructions = PLSearch.GenerateSearchInstructions(splitSearchText[0], out var _);
            currentItemSearchInstructions = PLSearch.GenerateSearchInstructions(splitSearchText[1..].Join(), out var _);
        }
        else
        {
            currentMissionSearchInstructions = PLSearch.GenerateSearchInstructions(searchText, out var _);
            currentItemSearchInstructions = Array.Empty<PLSearch.Instruction>();
        }
    }

    void GenerateSearchFilters()
    {
        activeItemFilters.Clear();
        for (int i = 0; i < itemFilters.Length; i++)
        {
            if (itemFilterButtons.Length>i && itemFilterButtons[i].ButtonPressed)
            {
                activeItemFilters.AddRange(itemFilters[i]);
            }
        }
    }

    void FilterMissionGrid()
	{
        foreach (var rowGroup in activeRowGroups)
        {
            rowGroup.row.Visible = false;
            foreach (var missionEntry in rowGroup.entries)
            {
                missionEntry.Visible = missionEntry.Filter(currentMissionSearchInstructions, currentItemSearchInstructions, itemSearchToggle.ButtonPressed, currentZoneFilter, activeItemFilters.ToArray());
                rowGroup.row.Visible |= missionEntry.Visible;
            }
        }
    }

}

/*
public class MissionData
{
    public JsonObject missionInstance;
    public JsonObject missionGen;
    public JsonObject missionTile;
    public int powerLevel;
    public char theatreCategory;
    public bool hasMissionAlert;
    public List<JsonObject> missionRewards = new();
    public List<string> alertModifiers = new();
    public List<JsonObject> alertRewards = new();

    //used to keep WeakRefs alive in the BanjoAssets texture cache
    List<Texture2D> textureDependancies = new();

    public MissionData(JsonObject missionInstance)
    {
        this.missionInstance = missionInstance;
        powerLevel = missionInstance["missionDifficultyInfo"]["RecommendedRating"].GetValue<int>();
        theatreCategory = missionInstance["theatreCat"].GetValue<char>();
        missionRewards = missionInstance["rewards"].AsArray().Select(val=>val.AsObject()).ToList();

        textureDependancies.AddRange(missionRewards.Select(r=>r.GetTemplate().GetItemTexture()));

        if (missionInstance.ContainsKey("missionAlert"))
        {
            hasMissionAlert = true;

            alertModifiers = missionInstance["missionAlert"]["modifiers"].Deserialize<List<string>>();
            textureDependancies.AddRange(alertModifiers.Select(m => BanjoAssets.TryGetTemplate(m).GetItemTexture()));

            alertRewards = missionInstance["missionAlert"]["rewards"].AsArray().Select(r=>r.AsObject()).ToList();
            textureDependancies.AddRange(alertRewards.Select(r => r.GetTemplate().GetItemTexture()));
        }
    }

    static readonly Dictionary<string, string> typeKeywordAliases = new()
    {
        ["survivor"] = "worker"
    };

    public bool FilterElement(char[] theatreFilters, string[] searchTerms, bool requireAll)
    {
        if (!theatreFilters.Contains(theatreCategory))
            return false;

        if (minPowerLevel > powerLevel)
            return false;

        if (maxPowerLevel < powerLevel)
            return false;

        if (searchTerms.Length == 0)
            return true;

        bool matchFound = requireAll;
        foreach (var item in searchTerms)
        {
            if (item.StartsWith("-"))
            {
                if (searchKeywords.Exists(val => val.Contains(item[1..])))
                {
                    matchFound = false;
                    break;
                }
            }
            else if (item.StartsWith("t:"))
            {
                string itemType = item[2..];
                if (
                    requireAll != (
                     typeKeywords.Contains(itemType) ||
                     (
                      typeKeywordAliases.ContainsKey(itemType) &&
                      typeKeywords.Contains(typeKeywordAliases[itemType])
                      )
                     )
                    )
                {
                    matchFound = !requireAll;
                    break;
                }
            }
            else if (requireAll != searchKeywords.Exists(val=>val.Contains(item)))
            {
                matchFound = !requireAll;
                break;
            }
        }

        if (!matchFound)
            return false;

        return true;
    }
}
*/
