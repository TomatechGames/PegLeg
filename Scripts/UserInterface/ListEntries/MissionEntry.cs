using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class MissionEntry : Control
{
	static List<string> debugKnownZones = new();
	// Called when the node enters the scene tree for the first time.
	[Export] NodePath powerLevelLabel;
	[Export] NodePath missionIcon;
	[Export] NodePath missionRewardParent;
	[Export] NodePath alertModifierParent;
	[Export] NodePath alertRewardParent;
	[Export] NodePath alertModifierLayout;
	[Export] NodePath alertRewardLayout;
	[Export] NodePath backgroundTexture;
	public override void _Ready()
	{
	}

	bool isActive = false;
	string generatorID;
	int powerLevel;
	string theatreName;
	char theatreCategory;
	string zoneTheme;

	Dictionary<string, int> missionRewards = new();
	List<string> alertModifiers = new();
	Dictionary<string, int> alertRewards = new();
    List<string> searchKeywords = new();
    List<string> typeKeywords = new();

	static readonly Dictionary<string,string> typeKeywordAliases = new()
	{
		["survivor"] = "worker"
	};

    bool hasMissionAlert = false;
	bool isGroup;

	public void DeactivateEntry()
	{
		isActive = false;
    }

	public void PrintSearchTerms()
	{
		GD.Print(searchKeywords.ToArray().Join(", "));
	}
	
	public void ActivateEntry(JsonObject entryData)
	{
		isActive = true;
		generatorID = entryData["generatorID"].ToString();
		powerLevel = entryData["powerLevel"].GetValue<int>();
		theatreName = entryData["theatreName"].ToString();
        theatreCategory = entryData["theatreCat"].GetValue<char>();
        zoneTheme = entryData["zoneTheme"].ToString();
		missionRewards = entryData["rewards"].Deserialize<Dictionary<string,int>>();

		if (!debugKnownZones.Contains(zoneTheme) && theatreCategory!='v')
		{
			//GD.Print("Zone: "+zoneTheme);
			debugKnownZones.Add(zoneTheme);
		}
		
		isGroup = generatorID.Contains("Group");
		
		if(entryData.ContainsKey("missionAlert"))
		{
			hasMissionAlert = true;
			alertModifiers = entryData["missionAlert"]["modifiers"].Deserialize<List<string>>();
			alertRewards = entryData["missionAlert"]["rewards"].Deserialize<Dictionary<string,int>>();
		}
		else
		{
			alertModifiers.Clear();
			alertRewards.Clear();
		}
        searchKeywords.Clear();


        if (isGroup)
        {
			searchKeywords.Add("group");
            searchKeywords.Add("4-player");
        }

        //apply data to element

        GetNode<Label>(powerLevelLabel).Text = powerLevel.ToString();

		var missionGenDisplayData = FNAssetData.GetDisplayDataForMissionGenerator(generatorID);
		var missionIconNode = GetNode<TextureRect>(missionIcon);
		missionIconNode.Texture = missionGenDisplayData.GetIcon();
		missionIconNode.TooltipText = missionGenDisplayData.GetOptimalName() + "\n" + zoneTheme + "\n" + theatreName;

		searchKeywords.Add(missionGenDisplayData.GetOptimalName().ToLower().Replace(" ", "_"));

        if (alertRewards.Count > 0)
        {
            searchKeywords.Add("alert");
        }

        Node missionRewardParentNode = GetNode(missionRewardParent);
		for (int i = 0; i < missionRewardParentNode.GetChildCount(); i++)
		{
			var rewardChild = missionRewardParentNode.GetChild<GameItemEntry>(i);
			if(missionRewards.Count<=i)
			{
				rewardChild.Visible = false;
				continue;
            }
            rewardChild.Visible = true;
            var rewardKVP = missionRewards.ElementAt(i);
			var rewardData = FNAssetData.GetDataForReward(rewardKVP.Key.Split(":")[1]);
			int rewardAmount = rewardKVP.Value;

            searchKeywords.AddRange(rewardData.Item1.GetOptimalName(rewardKVP.Key).Replace('!', ' ').ToLower().Split(" "));


            rewardChild.SetItemData(new(
			rewardData.Item1.GetOptimalName(),
            rewardData.Item1.GetIcon(),
			rewardAmount
			));

			/*
			var textureRect = rewardChild.GetChild<TextureRect>(0);
            textureRect.Texture = rewardData.Item1.GetIcon();
            textureRect.GetChild<Label>(0).Text = rewardAmount==1 ? "" : $"x{rewardAmount}";
			rewardChild.TooltipText = rewardData.Item1.GetOptimalName()+(rewardAmount==1 ? "" : $" (x{rewardAmount})");
			*/
		}

		Node alertRewardParentNode = GetNode(alertRewardParent);
		for (int i = 0; i < alertRewardParentNode.GetChildCount(); i++)
		{
            var itemChild = alertRewardParentNode.GetChild<GameItemEntry>(i);
			if(alertRewards.Count<=i)
			{
				itemChild.Visible = false;
				continue;
            }
            itemChild.Visible = true;
            var resourceKVP = alertRewards.ElementAt(i);

			if (BanjoAssets.TryGetTemplate(resourceKVP.Key, out var itemObject))
            {
				string name = itemObject["ItemName"].ToString();

                searchKeywords.AddRange(name.Replace('!', ' ').ToLower().Split(" "));

				string type = resourceKVP.Key.Split(':')[0].ToLower();
				if (!typeKeywords.Contains(type))
					typeKeywords.Add(type);

				itemChild.SetItemData(new(itemObject.CreateInstanceOfItem(resourceKVP.Value, new() { ["item_seen"] = true })))
					.SetInteractable(GameItemEntry.TypeShouldBeInteractable(type));
                continue;
            }

			/*
			if(resourceKVP.Key.StartsWith("AccountResource"))
			{
				GD.Print("This shouldnt happen");
				var resourceData = FNAssetData.GetDataForResource(resourceKVP.Key.Split(":")[1]);

                searchKeywords.AddRange(resourceData.Item1.GetOptimalName(resourceKVP.Key).Replace('!', ' ').ToLower().Split(" "));

                itemChild.SetItemData(
					resourceData.Item1.GetOptimalName(resourceKVP.Key), 
					resourceData.Item1.GetIcon(), 
					resourceKVP.Value, 
					resourceData.Item2
				);
				continue;
            }
			*/

            searchKeywords.Add("?");

            itemChild.SetItemData(new(resourceKVP.Key, FNAssetData.defaultIcon, resourceKVP.Value));
        }

        Node alertModifierParentNode = GetNode(alertModifierParent);
        for (int i = 0; i < alertModifierParentNode.GetChildCount(); i++)
        {
            TextureRect alertChild = alertModifierParentNode.GetChild<TextureRect>(i);
            if (alertModifiers.Count <= i)
            {
                alertChild.Visible = false;
                continue;
            }
            alertChild.Visible = true;
			string modifierID = alertModifiers[i];
            var modifierData = FNAssetData.GetDataForModifier(modifierID.Split(":")[1]);

            searchKeywords.AddRange(modifierData.GetOptimalName(modifierID).ToLower().Split(" "));

            alertChild.Texture = modifierData.GetIcon();
            alertChild.TooltipText = modifierData.GetOptimalName(modifierID);
        }

		List<string> extraSearchKeywords = new();
		for (int i = 0; i < searchKeywords.Count; i++)
		{
			if (searchKeywords[i].Contains('-'))
            {
                // this allows all 3 of the following to be interchangable:
                // - "re perk" (with all terms mode enabled)
                // - "re-perk"
                // - "reperk"
                extraSearchKeywords.AddRange(searchKeywords[i].Split('-'));
                extraSearchKeywords.Add(searchKeywords[i].Split('-').Join(""));
            }
		}
		searchKeywords.AddRange(extraSearchKeywords);

		GetNode<Control>(alertModifierLayout).Visible = hasMissionAlert;
		GetNode<Control>(alertRewardLayout).Visible = hasMissionAlert;
	}

	static readonly Color[] rarityColours = {
		new("#bfbfbf"),
		new("#bfbfbf"),
		new("#83db00"),
		new("#008bf1"),
		new("#a952ff"),
		new("#ff7b3d"),
	};

	public bool FilterEntry(char[] theatreFilters, string[] searchTerms, bool requireAll, int minPowerLevel, int maxPowerLevel)
	{
		if (!isActive)
            return Visible = false;

        if (!theatreFilters.Contains(theatreCategory))
			return Visible = false;

        if (minPowerLevel > powerLevel)
            return Visible = false;

        if (maxPowerLevel < powerLevel)
            return Visible = false;

        if (searchTerms.Length==0)
            return Visible = true;

		bool matchFound = requireAll;
        foreach (var item in searchTerms)
        {
            if (item.StartsWith("-"))
			{
				if (searchKeywords.Contains(item[1..]))
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
			else if (requireAll!=searchKeywords.Contains(item))
			{
				matchFound = !requireAll;
				break;
			}
        }

		if(!matchFound)
            return Visible = false;

        /*
        bool rewardsSearchSuccess = requireAll;
        bool alertRewardsSearchSuccess = requireAll;
        bool modifiersSearchSuccess = requireAll;
        foreach (string searchTerm in searchTerms)
		{
			if(requireAll != missionRewards.ContainsKey(searchTerm))
			{
				rewardsSearchSuccess = !requireAll;
			}
            if (requireAll != alertRewards.ContainsKey(searchTerm))
            {
                alertRewardsSearchSuccess = !requireAll;
            }
            if (requireAll != alertModifiers.Contains(searchTerm))
            {
                modifiersSearchSuccess = !requireAll;
            }

			if (requireAll != (rewardsSearchSuccess || alertRewardsSearchSuccess || modifiersSearchSuccess))
				break;
        }

		if(!rewardsSearchSuccess || !alertRewardsSearchSuccess || !modifiersSearchSuccess)
            return Visible = false;
		*/

        /*
        if (filterData.ContainsKey("anyReward") )
		{
			bool found = false;
			foreach(JsonNode value in filterData["anyReward"].AsArray())
            {
                string item = value.ToString();
                if (missionRewards.ContainsKey(item) || (hasMissionAlert && alertRewards.ContainsKey(item)))
				{
					found = true;
					break;
				}
			}
			if(!found)
                return Visible = false;
        }

		if(filterData.ContainsKey("everyReward") )
		{
            bool found = false;
            foreach (JsonNode value in filterData["anyReward"].AsArray())
            {
				string item = value.ToString();
				if(!missionRewards.ContainsKey(item) && !(hasMissionAlert && alertRewards.ContainsKey(item)))
				{
					found = true;
					break;
				}
			}
			if(!found)
                return Visible = false;
        }
		*/

        return Visible = true;
	}
}
