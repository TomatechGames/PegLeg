using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public partial class MissionInterface : Control, IRecyclableElementProvider<MissionData>
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
    RecycleListContainer missionList;

    [Export]
	Control loadingIcon;

    string currentTheaterFilter => theaterFilters[zoneFilterTabBar.CurrentTab];

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
            "Defender:*",
        },
        new string[]
        {
            "Worker:workerbasic_sr_t01",
            "MYTHICLEAD",
        }
    };


    public override async void _Ready()
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
            GenerateSearchInstructions(searchBar.Text);
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
        GenerateSearchInstructions(searchBar.Text);
        GenerateSearchFilters();

        missionList.SetProvider(this);

        if (Visible)
            LoadMissions();
        await MissionRequests.GetMissions();
    }

    PLSearch.Instruction[] currentMissionSearchInstructions;
    PLSearch.Instruction[] currentItemSearchInstructions;
    List<MissionData> allMissions = new();
    List<MissionData> activeMissions = new();
    List<string> currentItemFilters = new();

    public MissionData GetRecycleElement(int index) => index >= 0 && index < activeMissions.Count ? activeMissions[index] : null;
    public int GetRecycleElementCount() => activeMissions.Count;

    public void ForceReloadMissions() => LoadMissions(true);

    bool isLoadingMissions = false;
    async void LoadMissions(bool force = false)
    {
        if (isLoadingMissions || !await LoginRequests.TryLogin())
            return;
        isLoadingMissions = true;
        if (MissionRequests.MissionsRequireUpdate() || force)
        {
            try
            {
                await ProfileRequests.GetProfile(FnProfiles.AccountItems);
                missionList.Visible = false;
                loadingIcon.Visible = true;
                await this.WaitForFrame();

                var allMissionData = await MissionRequests.GetMissions(force);
                await this.WaitForFrame();
                allMissions = allMissionData["missions"].AsArray().Select(m=>new MissionData(m.AsObject())).ToList();

                foreach (var mission in allMissions)
                {
                    //()=> this.WaitForFrame()
                    await mission.SetRewardNotifications();
                    await this.WaitForFrame();
                }
            }
            finally
            {
                missionList.Visible = true;
                loadingIcon.Visible = false;
                isLoadingMissions = false;
            }
        }

        FilterMissionGrid();
    }

    void GenerateSearchInstructions(string searchText)
    {
        searchText = searchText
            .Replace("///", " i:/ ")
            .Replace(" I:/ ", " i:/ ");
        if (searchText.Contains(" i:/ ") || searchText.StartsWith("i:/ "))
        {
            string[] splitSearchText = searchText.Split("i:/");
            currentMissionSearchInstructions = PLSearch.GenerateSearchInstructions(splitSearchText[0], out var _) ?? Array.Empty<PLSearch.Instruction>();
            currentItemSearchInstructions = PLSearch.GenerateSearchInstructions(splitSearchText[1..].Join(), out var _) ?? Array.Empty<PLSearch.Instruction>();
        }
        else
        {
            currentMissionSearchInstructions = PLSearch.GenerateSearchInstructions(searchText, out var _) ?? Array.Empty<PLSearch.Instruction>();
            currentItemSearchInstructions = Array.Empty<PLSearch.Instruction>();
        }
    }

    void GenerateSearchFilters()
    {
        currentItemFilters.Clear();
        for (int i = 0; i < itemFilters.Length; i++)
        {
            if (itemFilterButtons.Length>i && itemFilterButtons[i].ButtonPressed)
            {
                currentItemFilters.AddRange(itemFilters[i]);
            }
        }
    }

    void FilterMissionGrid()
	{
        activeMissions = allMissions
            .Where(m => 
                m.Filter(
                    currentMissionSearchInstructions, 
                    currentItemSearchInstructions, 
                    currentTheaterFilter, 
                    currentItemFilters.ToArray()
                )
            ).
            ToList();
        missionList.UpdateList(true);
    }
}

public class MissionData
{
    public int powerLevel { get; private set; }
    public string theaterCat { get; private set; }

    public JsonObject missionJson { get; private set; }

    List<Texture2D> textureDependancies = new();
    List<JsonObject> rewardItems = new();

    public MissionData(JsonObject missionJson)
    {
        this.missionJson = missionJson;

        powerLevel = missionJson["missionDifficultyInfo"]["RecommendedRating"].GetValue<int>();
        theaterCat = missionJson["theaterCat"].ToString();

        textureDependancies.Add(missionJson["missionGenerator"].AsObject().GetItemTexture(BanjoAssets.TextureType.Icon));

        if (missionJson["missionGenerator"].AsObject().GetItemTexture(BanjoAssets.TextureType.LoadingScreen) is Texture2D missionLoadingScreen)
            textureDependancies.Add(missionLoadingScreen);
        else if (missionJson["tile"]["zoneTheme"].AsObject().GetItemTexture(BanjoAssets.TextureType.LoadingScreen) is Texture2D zoneLoadingScreen)
            textureDependancies.Add(zoneLoadingScreen);

        foreach (var item in missionJson["missionRewards"].AsArray())
        {
            textureDependancies.Add(item.AsObject().GetItemTexture());
            rewardItems.Add(item.AsObject());
        }

        if (missionJson["missionAlert"] is JsonObject missionAlert)
        {
            foreach (var item in missionAlert["modifiers"].AsArray())
            {
                textureDependancies.Add(item.GetTemplate().GetItemTexture());
            }
            foreach (var item in missionAlert["rewards"].AsArray())
            {
                textureDependancies.Add(item.AsObject().GetItemTexture());
                rewardItems.Add(item.AsObject());
            }
        }
    }

    public async Task SetRewardNotifications(Func<Task> frameTaskGenerator = null)
    {
        foreach (var item in rewardItems)
        {
            await item.SetItemRewardNotification();
            if (frameTaskGenerator is not null)
                await frameTaskGenerator();
        }
        //for (int i = 0; i < missionJson["missionRewards"].AsArray().Count; i++)
        //{
        //    missionJson["missionRewards"][i] = await missionJson["missionRewards"][i].AsObject().SetItemRewardNotification();
        //}

        //for (int i = 0; i < (missionJson["missionAlert"]?["rewards"].AsArray().Count ?? 0); i++)
        //{
        //    missionJson["missionAlert"]["rewards"][i] = await missionJson["missionAlert"]["rewards"][i].AsObject().SetItemRewardNotification();
        //}
    }

    public bool Filter(PLSearch.Instruction[] missionInstructions, PLSearch.Instruction[] itemInstructions, string theaterFilter, string[] extraItemFilters)
    {
        if (!theaterFilter.Contains(theaterCat[0]))
            return false;
        if(!PLSearch.EvaluateInstructions(missionInstructions, missionJson))
            return false;

        //GD.Print("matching: "+itemFilter);
        bool matchesItemFilter = true;
        foreach (var reward in rewardItems)
        {
            matchesItemFilter = extraItemFilters.Length == 0;
            foreach (var itemFilter in extraItemFilters)
            {
                string rewardId = reward["itemType"].ToString();
                var template = reward.GetTemplate();

                //GD.Print("reward: " + rewardId);
                if (reward.ContainsKey("equivelent"))
                {
                    rewardId = reward["equivelent"].ToString();
                    template = reward["equivelentTemplate"].AsObject();
                    //GD.Print("equivelent: " + rewardId);
                }

                matchesItemFilter = rewardId.Contains(itemFilter);
                if (itemFilter.EndsWith("*"))
                    matchesItemFilter = rewardId.StartsWith(itemFilter[..^1]);

                if (itemFilter == "MYTHICLEAD")
                    matchesItemFilter = Regex.Match(rewardId, "Worker:manager\\w+_sr_\\w*").Success;

                if (matchesItemFilter)
                    break;
            }
            if (!matchesItemFilter)
                continue;
            matchesItemFilter = PLSearch.EvaluateInstructions(itemInstructions, reward); //search item instructions
            if (matchesItemFilter)
                break;
        }
        return matchesItemFilter;
    }
}
