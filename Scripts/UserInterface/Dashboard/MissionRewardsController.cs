using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class MissionRewardsController : Control, IRecyclableElementProvider<MissionRewardPair>
{
    [Export]
    RecycleListContainer missionList;
    [Export]
    Control loadingIcon;
    [Export]
    Button sortByPower;
    [Export]
    bool notableMode;

    [Export]
	CheckButton[] rarityFilters;
    [Export]
    CheckButton[] zoneFilters;
    [Export]
    CheckButton[] typeFilters;

    [Export]
    CheckButton[] repeatabilityFilters;

    CheckButton[] allFilters;

    List<MissionRewardPair> rewards = new();


    public MissionRewardPair GetRecycleElement(int index) => index>=0 && index<rewards.Count ? rewards[index] : default;

    public int GetRecycleElementCount() => rewards.Count;

    public async void ReloadMissions()
    {
        await GameMission.UpdateMissions();
    }

    public override async void _Ready()
	{
        missionList.SetProvider(this);
        allFilters = rarityFilters.Union(zoneFilters).Union(typeFilters).Where(f => f is not null).ToArray();
        foreach (var filter in allFilters)
        {
            var current = filter;
            current.Toggled += newVal =>
            {
                if (lockFilter)
                    return;
                if (!Input.IsKeyPressed(Key.Shift) && newVal)
                    TurnOffOtherFilters(current);
                FilterMissions();
            };
        }
        if (sortByPower is not null)
            sortByPower.Toggled += _ => FilterMissions();
        foreach (var filter in repeatabilityFilters)
        {
            if (lockFilter)
                return;
            filter.Toggled += _ => FilterMissions();
        }
        GameMission.OnMissionsUpdated += FilterMissions;
        GameMission.OnMissionsInvalidated += ClearMissions;
        VisibilityChanged += TryRefresh;
        await GameMission.UpdateMissions();
    }

    public override void _ExitTree()
    {
        GameMission.OnMissionsUpdated -= FilterMissions;
        GameMission.OnMissionsInvalidated -= ClearMissions;
    }

    public void TurnOffFilters()
    {
        TurnOffOtherFilters(null);
        lockFilter = true;
        foreach (var filter in repeatabilityFilters)
        {
            filter.ButtonPressed = false;
        }
        lockFilter = false;
        FilterMissions();
    }

    bool lockFilter = false;

    void TurnOffOtherFilters(CheckButton exceptThis)
    {
        lockFilter = true;
        foreach (var filter in allFilters)
        {
            if (filter == exceptThis)
                continue;
            filter.ButtonPressed = false;
        }
        lockFilter = false;
    }

    bool IsNotable(GameItem item)
    {
        var template = item.sortingTemplate;
        if (template.RarityLevel == 6 && template.Type == "Worker")
            return true; // mythic leads
        if (template.VBucksOrXRayTickets)
            return true; // v-bucks
        if (template.TemplateId == "AccountResource:voucher_cardpack_bronze")
            return true; // upgrade llamas
        if (template.RarityLevel == 5 && template.Type == "Worker" && template.SubType is null)
            return true; // legendary survivor (excl. leads)
        return false;
    }

    void TryRefresh()
    {
        if (needsRefresh)
            FilterMissions();
    }

    bool needsRefresh = false;
    CancellationTokenSource filterCTS = new();
    async void FilterMissions()
    {
        var missions = GameMission.currentMissions;
        if (lockFilter || missions is null)
            return;
        if (!IsVisibleInTree())
        {
            needsRefresh = true;
            return;
        }
        needsRefresh = false;
        filterCTS.CancelAndRegenerate(out var ct);
        loadingIcon.Visible = true;
        missionList.Visible = false;

        Predicate<GameMission> missionPredicate = null;
        Predicate<GameItem> itemPredicate = null;
        if (notableMode)
        {
            itemPredicate = IsNotable;
        }
        else
        {
            List<string> requiredZones = zoneFilters
                .Select(c => c.ButtonPressed && c.GetMeta("zone", "").ToString() is string result && !string.IsNullOrWhiteSpace(result) ? result : null)
                .Where(result => result is not null)
                .ToList();
            missionPredicate = m => requiredZones.Count <= 0 || requiredZones.Contains(m.TheaterCat);

            List<string> requiredRarities = rarityFilters
                .Select(c => c.ButtonPressed && c.GetMeta("rarity", "").ToString() is string result && !string.IsNullOrWhiteSpace(result) ? result : null)
                .Where(result => result is not null)
                .ToList();
            List<string> requiredTypes = typeFilters
                .Select(c => c.ButtonPressed && c.GetMeta("type", "").ToString() is string result && !string.IsNullOrWhiteSpace(result) ? result : null)
                .Where(result => result is not null)
                .ToList();
            itemPredicate = i =>
            {
                if (repeatabilityFilters[0].ButtonPressed && i.zcpEquivelent is not null)
                    return false;
                else if (repeatabilityFilters[1].ButtonPressed && i.zcpEquivelent is null)
                    return false;
                else if (repeatabilityFilters[2].ButtonPressed && (i.zcpEquivelent is null || i.quantity < 4))
                    return false;

                if (
                    requiredRarities.Count > 0 &&
                    !requiredRarities.Contains(i.sortingTemplate.Rarity ?? "Uncommon")
                    )
                    return false;
                if (
                    requiredTypes.Count > 0 &&
                    !requiredTypes.Any(t => i.sortingTemplate.TemplateId.StartsWith(t)) &&
                    !requiredTypes.Contains($"{i.sortingTemplate.Type}>{i.sortingTemplate.Category}")
                    )
                    return false;
                return true;
            };
        }

        List<MissionRewardPair> filteredRewards = new();

        await Task.Run(() =>
        {
            foreach (var mission in missions)
            {
                if (missionPredicate is not null && !missionPredicate(mission))
                    continue;
                foreach (var item in mission.allItems)
                {
                    if (
                        item.template.DisplayName == "Gold" || 
                        item.template.DisplayName == "Venture XP"
                        )
                        continue;
                    if(itemPredicate is not null && !itemPredicate(item))
                        continue;
                    filteredRewards.Add(new() { mission = mission, item = item });
                    if (ct.IsCancellationRequested)
                        return;
                }
            }
        }, ct);

        if (ct.IsCancellationRequested)
            return;

        var sortedRewards = filteredRewards.OrderBy(r =>
        {
            var template = r.item.sortingTemplate;
            if (notableMode)
            {
                if (template.RarityLevel == 6 && template.Type == "Worker")
                    return -20; // mythic leads
                if (template.VBucksOrXRayTickets)
                    return -19; // v-bucks
                if (template.TemplateId == "AccountResource:voucher_cardpack_bronze")
                    return -18; // upgrade llamas
                if (template.RarityLevel == 5 && template.Type == "Worker" && template.SubType is null)
                    return -17; // legendary survivor (excl. leads)
            }
            return -template.RarityLevel - (r.item.template.CanBeLeveled ? 0.1 : 0);
        })
        .ThenBy(r => r.item.sortingTemplate.Type);

        if (sortByPower?.ButtonPressed ?? false)
        {
            sortedRewards = sortedRewards.OrderBy(r => -r.mission.PowerLevel);
        }

        rewards = sortedRewards.ToList();

        loadingIcon.Visible = false;
        missionList.Visible = true;

        missionList.UpdateList(true);
    }

    void ClearMissions()
    {
        loadingIcon.Visible = true;
        missionList.Visible = false;
    }
}

public struct MissionRewardPair
{
    public GameMission mission;
    public GameItem item;
}