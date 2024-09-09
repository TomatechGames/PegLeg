using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class HomebasePowerLevel : Control
{
    static DataTableCurve homebaseRatingCurve = new("res://External/DataTables/HomebaseRatingMapping.json", "UIMonsterRating");

    //static DataTable survivorRatingDataTable = new("res://External/DataTables/SurvivorItemRating.json");
    //public static float GetSurvivorRating(JsonObject itemInstance) =>
    //    survivorRatingDataTable[itemInstance.GetTemplate()["RatingLookup"].ToString()]
    //            .Sample(itemInstance["attributes"]["level"].GetValue<float>());

    static readonly Dictionary<string, string> synergyMap = new()
    {
        ["IsDoctor"] = "squad_attribute_medicine_emtsquad",
        ["IsTrainer"] = "squad_attribute_medicine_trainingteam",
        ["IsSoldier"] = "squad_attribute_arms_fireteamalpha",
        ["IsGadgeteer"] = "squad_attribute_scavenging_gadgeteers",
        ["IsEngineer"] = "squad_attribute_synthesis_corpsofengineering",
        ["IsMartialArtist"] = "squad_attribute_arms_closeassaultsquad",
        ["IsExplorer"] = "squad_attribute_scavenging_scoutingparty",
        ["IsInventor"] = "squad_attribute_synthesis_thethinktank",
    };
    public static event Action<FORTStats> OnFortStatsChanged;

    [Export]
    Label homebaseNumberLabel;
    [Export]
    ProgressBar homebaseNumberProgressBar;

    static HomebasePowerLevel instance;
    public override async void _Ready()
    {
        instance = this;
        equippedWorkerListener = await ProfileListener.CreateListener(FnProfiles.AccountItems, EquippedWorkerPredicate);
        equippedWorkerListener.OnAdded += handle => fortStatsAreDirty = true;
        equippedWorkerListener.OnUpdated += handle => fortStatsAreDirty = true;
        equippedWorkerListener.OnRemoved += handle => fortStatsAreDirty = true;

        //TODO: recalculate fort stats when applying research points (if i ever implement a research menu)

        OnFortStatsChanged += UpdateStatsVisuals;

        if (await LoginRequests.TryLogin())
            await RecalculateFORTStats();
    }

    private void UpdateStatsVisuals(FORTStats stats)
    {
        var homebaseRatingKey = 4 * (stats.fortitude + stats.offense + stats.resistance + stats.technology);
        var powerLevel = homebaseRatingCurve.Sample(homebaseRatingKey);
        homebaseNumberLabel.Text = MathF.Floor(powerLevel).ToString();
        homebaseNumberProgressBar.Value = powerLevel % 1;
        TooltipText = $"Homebase Power: {MathF.Floor(powerLevel)}\n({MathF.Floor((powerLevel % 1) * 100)}% progress to {MathF.Floor(powerLevel) + 1})";
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        OnFortStatsChanged -= UpdateStatsVisuals;
    }

    ProfileListener equippedWorkerListener;

    bool EquippedWorkerPredicate(KeyValuePair<string, JsonObject> item) =>
                item.Value["templateId"]?.ToString()?.StartsWith("Worker") ?? false &&
                item.Value["attributes"].AsObject().ContainsKey("squad_id");

    bool fortStatsAreDirty = false;
    public override async void _Process(double delta)
    {
        if (fortStatsAreDirty)
            await RecalculateFORTStats();
    }

    public struct FORTStats
    {
        public float fortitude;
        public float offense;
        public float resistance;
        public float technology;
        public FORTStats(float fortitude, float offense, float resistance, float technology)
        {
            this.fortitude = fortitude;
            this.offense = offense;
            this.resistance = resistance;
            this.technology = technology;
        }
    }

    static FORTStats? currentFortStats = null;
    public static async Task<FORTStats> GetFORTStats()
    {
        if (currentFortStats.HasValue)
            return currentFortStats.Value;
        return await instance.RecalculateFORTStats();
    }

    public static FORTStats GetFORTStatsUnsafe()
    {
        if (currentFortStats.HasValue)
            return currentFortStats.Value;
        return new();
    }

    bool isRecalculating = false;
    public async Task<FORTStats> RecalculateFORTStats()
    {
        if (isRecalculating)
        {
            while (isRecalculating)
                await Task.Delay(100);
            return currentFortStats.Value;
        }

        isRecalculating = true;
        var profileStats = (await ProfileRequests.GetProfile(FnProfiles.AccountItems))["profileChanges"][0]["profile"]["stats"]["attributes"]["research_levels"];
        var profileStatAndWorkerItems = await ProfileRequests.GetProfileItems(FnProfiles.AccountItems, item =>
            item.Value["templateId"].ToString().StartsWith("Stat") || EquippedWorkerPredicate(item)
        );
        int LookupStatItem(string statId) =>
            profileStatAndWorkerItems.First(
                item => item.Value["templateId"].ToString() == statId
            ).Value["quantity"].GetValue<int>();

        float LookupWorkers(string squadId)
        {
            var matchingWorkers = profileStatAndWorkerItems
                .Where(item => 
                    item.Value["attributes"]?["squad_id"]?.ToString() == squadId
                    )
                .OrderBy(item => item.Value["attributes"]["squad_slot_idx"].GetValue<int>())
                .ToArray();

            List<float> contributions = new();
            //GD.Print($"\n");
            for (int i = 0; i < matchingWorkers.Length; i++)
            {
                float thisContribution = matchingWorkers[i].Value?.AsObject()?.GetItemRating() ?? 0;
                //GD.Print($"{squadId}[{i}] contributes {thisContribution} ({matchingWorkers[i].Key})\n");
                contributions.Add(thisContribution);
            }

            var squadContribution = contributions.Sum();
            //GD.Print($"{squadId} in total contributes {squadContribution}");
            return squadContribution;
        }

        //+ profileStats["fortitude"].GetValue<int>()
        float fortitude = LookupStatItem("Stat:fortitude") + LookupStatItem("Stat:fortitude_team") + LookupWorkers("squad_attribute_medicine_trainingteam") + LookupWorkers("squad_attribute_medicine_emtsquad");
        float offense = LookupStatItem("Stat:offense") + LookupStatItem("Stat:offense_team") + LookupWorkers("squad_attribute_arms_fireteamalpha") + LookupWorkers("squad_attribute_arms_closeassaultsquad");
        float resistance = LookupStatItem("Stat:resistance") + LookupStatItem("Stat:resistance_team") + LookupWorkers("squad_attribute_scavenging_scoutingparty") + LookupWorkers("squad_attribute_scavenging_gadgeteers");
        float technology = LookupStatItem("Stat:technology") + LookupStatItem("Stat:technology_team") + LookupWorkers("squad_attribute_synthesis_corpsofengineering") + LookupWorkers("squad_attribute_synthesis_thethinktank");


        GD.Print($"FORTITUDE: {fortitude}");
        GD.Print($"OFFENCE: {offense}");
        GD.Print($"RESISTANCE: {resistance}");
        GD.Print($"TECH: {technology}");

        currentFortStats = new(fortitude, offense, resistance, technology);
        fortStatsAreDirty = false;
        isRecalculating = false;
        OnFortStatsChanged?.Invoke(currentFortStats.Value);
        return currentFortStats.Value;
    }
}
