using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public partial class MissionEntry : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void DescriptionChangedEventHandler(string description);
    [Signal]
    public delegate void LocationChangedEventHandler(string location);
    [Signal]
    public delegate void TooltipChangedEventHandler(string tooltip);
    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);
    [Signal]
    public delegate void BackgroundChangedEventHandler(Texture2D background);
    [Signal]
    public delegate void VenturesIndicatorVisibleEventHandler(bool visible);

    [Export]
    Control alertModifierLayout;
    [Export]
    Control alertModifierParent;

    [Export]
    Control searchResultsParent;
    [Export]
    Control searchResultsLayout;

    public int powerLevel { get; private set; }
    string theaterCat;

    List<JsonObject> possibleSearchRewards;

    public void SetMissionData(JsonObject missionData)
	{
        //apply data to element
        var generator = missionData["missionGenerator"].AsObject();
        var tile = missionData["tile"].AsObject();

        powerLevel = missionData["missionDifficultyInfo"]["RecommendedRating"].GetValue<int>();

        EmitSignal(SignalName.IconChanged, generator.GetItemTexture(BanjoAssets.TextureType.Icon));

        theaterCat = missionData["theaterCat"].ToString();
        EmitSignal(SignalName.VenturesIndicatorVisible, theaterCat == "v");

        EmitSignal(SignalName.NameChanged, generator["DisplayName"].ToString());
        EmitSignal(SignalName.DescriptionChanged, generator["Description"].ToString());
        EmitSignal(SignalName.LocationChanged, tile["zoneTheme"]["DisplayName"].ToString());

        EmitSignal(SignalName.TooltipChanged, $"{generator["DisplayName"]}\n{generator["Description"]}\nLocated in: {tile["zoneTheme"]["DisplayName"]}");

        possibleSearchRewards = missionData["missionRewards"].AsArray().Select(r=>r.AsObject()).ToList();

        if (missionData["missionAlert"] is JsonObject missionAlert)
        {
            var alertModifiers = missionAlert["modifiers"].AsArray();
            for (int i = 0; i < alertModifierParent.GetChildCount(); i++)
            {
                var alertChild = alertModifierParent.GetChild<TextureRect>(i);
                if (alertModifiers.Count <= i)
                {
                    alertChild.Visible = false;
                    continue;
                }
                alertChild.Visible = true;
                var modifierData = BanjoAssets.TryGetTemplate(alertModifiers[i].ToString());

                alertChild.Texture = modifierData.GetItemTexture();
                alertChild.TooltipText = modifierData["DisplayName"].ToString();
            }
            alertModifierParent.SizeFlagsHorizontal = alertModifiers.Count == 5 ? SizeFlags.ShrinkCenter : SizeFlags.ExpandFill;
            if (alertModifiers.Count == 6)
            {
                var seventhChild = alertModifierParent.GetChild(6) as Control;
                seventhChild.Visible = true;
                seventhChild.SelfModulate = Colors.Transparent;
            }
                
            possibleSearchRewards.AddRange(missionAlert["rewards"].AsArray().Select(r=>r.AsObject()));
            alertModifierLayout.Visible = true;
        }
        else
        {
            alertModifierLayout.Visible = false;
        }
	}

    public bool Filter(PLSearch.Instruction[] missionInstructions, PLSearch.Instruction[] itemInstructions, bool targetItems, string theaterFilter, string[] extraItemFilters)
    {
        if (!theaterFilter.Contains(theaterCat[0]))
            return false;
        bool matchesItemFilter = extraItemFilters.Length == 0;
        List<JsonObject> rewardsToDisplay = new();
        foreach (var itemFilter in extraItemFilters)
        {
            GD.Print("matching: "+itemFilter);
            foreach (var reward in possibleSearchRewards)
            {
                string rewardId = reward["itemType"].ToString();
                GD.Print("reward: " + rewardId);
                if (reward.ContainsKey("equivelent"))
                    rewardId = reward["equivelent"].ToString();

                bool match = rewardId.Contains(itemFilter);
                if (itemFilter.EndsWith("*"))
                    match = rewardId.StartsWith(itemFilter[..^1]);

                if (itemFilter == "MYTHICLEAD")
                    match = Regex.Match(rewardId, "Worker:manager\\w+_sr_\\w*").Success;

                if (match && !rewardsToDisplay.Contains(reward))
                {
                    rewardsToDisplay.Add(reward);
                    matchesItemFilter = true;
                }
            }
        }
        for (int i = 0; i < alertModifierParent.GetChildCount(); i++)
        {
            var rewardChild = searchResultsParent.GetChild<GameItemEntry>(i);
            if (rewardsToDisplay.Count <= i)
            {
                rewardChild.Visible = false;
                continue;
            }
            rewardChild.Visible = true;

            rewardChild.SetItemData(rewardsToDisplay[i]);
        }
        searchResultsLayout.Visible = rewardsToDisplay.Count > 0;
        return matchesItemFilter;
    }
}
