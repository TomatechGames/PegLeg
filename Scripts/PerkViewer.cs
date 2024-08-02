using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class PerkViewer : Control
{
    [Export]
    float tweenDuration = 0.1f;
    [Export]
    NodePath realPerkAreaPath;
    Control realPerkArea;

    [Export]
    NodePath optionalPerkAreaPath;
    Control optionalPerkArea;

    [Export]
    NodePath interactionBlockerPath;
    Control interactionBlocker;

    [Export(PropertyHint.ArrayType)]
    NodePath[] realPerkEntryPaths = new NodePath[6];
    PerkEntry[] realPerkEntries;

    [Export(PropertyHint.ArrayType)]
    NodePath[] optionalPerkEntryPaths = new NodePath[5];
    PerkEntry[] optionalPerkEntries;

    [Export]
    NodePath tierUpSeparatorPath;
    Control tierUpSeparator;

    [Export]
    NodePath reperkApplyButtonPath;
    Control reperkApplyButton;

    [Export(PropertyHint.ArrayType)]
    NodePath[] reperkCostEntryPaths = new NodePath[3];
    GameItemEntry[] reperkCostEntries;

    static JsonObject reperkCosts;

    public override void _Ready()
    {
        this.GetNodesOrNull(realPerkEntryPaths, out realPerkEntries);
        this.GetNodesOrNull(optionalPerkEntryPaths, out optionalPerkEntries);
        this.GetNodesOrNull(reperkCostEntryPaths, out reperkCostEntries);
        this.GetNodeOrNull(tierUpSeparatorPath, out tierUpSeparator);
        this.GetNodeOrNull(interactionBlockerPath, out interactionBlocker);
        this.GetNodeOrNull(realPerkAreaPath, out realPerkArea);
        this.GetNodeOrNull(optionalPerkAreaPath, out optionalPerkArea);
        this.GetNodeOrNull(reperkApplyButtonPath, out reperkApplyButton);

        using var reperkCostFile = FileAccess.Open("res://External/reperkCosts.json", FileAccess.ModeFlags.Read);
        reperkCosts = JsonNode.Parse(reperkCostFile.GetAsText()).AsObject();

        for (int i = 0; i < realPerkEntries.Length; i++)
        {
            realPerkEntries[i].Pressed += (index, id, rep) => OpenPerkChanger(index, rep);
        }

        for (int i = 0; i < optionalPerkEntries.Length; i++)
        {
            optionalPerkEntries[i].Pressed += (index, id, rep) => SelectReplacementPerk(id, index);
        }

    }

    ProfileItemHandle linkedItem;
    public async void LinkItem(ProfileItemHandle profileItem)
    {
        linkedItem = profileItem;
        linkedItem.OnChanged += UpdateProfileItem;
        SetItem(await profileItem.GetItem());
    }

    public void SetDisplayItem(JsonObject itemInstance)
    {
        linkedItem = null;
        SetItem(itemInstance);
    }

    string[] existingPerks;
    string[][] perkPossibilities;
    int unlockedPerks = 1;

    void UpdateProfileItem(ProfileItemHandle profileItem)
    {
        SetItem(profileItem.GetItemUnsafe());
    }

    static bool IsTrap(JsonObject template)
    {
        if (template["Type"].ToString() != "Schematic") 
            return false;
        switch (template["SubType"].ToString())
        {
            case "Wall":
                return true;
            case "Ceiling":
                return true;
            case "Floor":
                return true;
            default: 
                return false;
        }
    }

    void SetItem(JsonObject itemInstance)
    {
        var template = itemInstance.GetTemplate();
        var maxedTemplate = template;

        while (maxedTemplate?["RarityUpItem"]?.ToString() is string nextRarity && BanjoAssets.TryGetTemplate(nextRarity, out var newMax))
        {
            maxedTemplate = newMax;
        }

        bool isDefender = template["Type"].ToString() == "Defender";
        isTrap = IsTrap(template);
        int itemRarity = template.GetItemRarity();

        interactionBlocker.MouseFilter = MouseFilterEnum.Ignore;
        realPerkArea.AnchorLeft = 0;
        realPerkArea.AnchorRight = 1;
        optionalPerkArea.AnchorLeft = 1;
        optionalPerkArea.AnchorRight = 2;

        existingPerks = null;
        if (itemInstance["attributes"]?.AsObject().ContainsKey("alterations") ?? false)
        {
            existingPerks = itemInstance["attributes"]["alterations"]
                .AsArray()
                .Select(e => e.ToString())
                .ToArray();
            //set current values on each element, lock if item level is too low
            for (int i = 0; i < existingPerks.Length; i++)
            {
                realPerkEntries[i].Visible = true;
                realPerkEntries[i].SetPerkAlteration(existingPerks[i], !isDefender, i==5 && !isTrap, i);
                realPerkEntries[i].SetInteractable(false);
            }
            for (int i = existingPerks.Length; i < realPerkEntries.Length; i++)
            {
                realPerkEntries[i].Visible = false;
            }
        }
        if (template["Type"].ToString() == "Schematic")
        {
            //set interactable and assign possibilities (if possibilities greater than one and not max level)
            perkPossibilities = maxedTemplate["AlterationSlots"]
                .AsArray()
                .Select(
                    slot => slot["Alterations"][0]
                    .AsArray()
                    .Select(
                        alt=>alt
                        .ToString()
                    )
                    .ToArray()
                )
                .ToArray();
            if(existingPerks is null)
            {
                existingPerks = new string[perkPossibilities.Length];
                //GD.Print("blank perks");
                for (int i = 0; i < existingPerks.Length; i++)
                {
                    //string perk = existingPerks[i];
                    //string tierSource = perk?[^1..] ?? "";
                    //_ = int.TryParse(tierSource, out int tier);
                    //if (tier > 0)
                    //    perk = perk[..^1] + tier;
                    realPerkEntries[i].Visible = true;
                    realPerkEntries[i].SetPerkAlteration("", !isDefender, i == 5 && !isTrap, i);
                    realPerkEntries[i].SetInteractable(false);
                }
                for (int i = existingPerks.Length; i < realPerkEntries.Length; i++)
                {
                    realPerkEntries[i].Visible = false;
                }
            }
            for (int i = 0; i < existingPerks.Length; i++)
            {
                if (i>=perkPossibilities.Length)
                {
                    //rarity is too low
                    realPerkEntries[i].SetInteractable(false);
                    realPerkEntries[i].SetReplaceable(false);
                    //realPerkEntries[i].Visible = false;
                    continue;
                }
                bool hasMoreThanOnePossibility = perkPossibilities[i].Length>1;
                int requiredLevel = maxedTemplate["AlterationSlots"][i]["RequiredLevel"].GetValue<int>();
                bool isLevelValid = requiredLevel <= (itemInstance["attributes"]?["level"]?.GetValue<int>() ?? 0);
                realPerkEntries[i].SetInteractable((hasMoreThanOnePossibility || PerkIsUpgradeable(existingPerks[i])));
                realPerkEntries[i].SetReplaceable(linkedItem is not null && isLevelValid);
            }
        }
        else
            perkPossibilities = null;
    }

    static bool PerkIsUpgradeable(string perk) =>
        perk is null ||
        perk.EndsWith("t01") ||
        perk.EndsWith("t02") ||
        perk.EndsWith("t03") ||
        perk.EndsWith("t04");

    bool isTrap = false;
    int selectedPerkIndex;
    int selectedPerkTier;
    bool selectedPerkReplaceability = false;

    public void OpenPerkChanger(int index, bool replaceable = false)
    {
        //GD.Print("opening perk changer for index: " + index);
        selectedPerkReplaceability = replaceable;
        string baseAlteration = existingPerks[index];
        string[] possibilities = perkPossibilities[index];
        string tierSource = baseAlteration?[^1..] ?? "";
        int.TryParse(tierSource, out int tier);
        bool hasUpgrade = !(index == 5 && !isTrap) && tier > 0 && tier < 5;

        if (!hasUpgrade && possibilities.Length==0)
        {
            //this shouldnt happen, but if it does, kablam
            return;
        }

        selectedPerkIndex = index;
        selectedPerkTier = tier;

        if (hasUpgrade)
        {
            string tierUpAlteration = baseAlteration[..^1] + (tier + 1);
            optionalPerkEntries[0].SetPerkAlteration(tierUpAlteration, true);
            optionalPerkEntries[0].Visible = true;
            tierUpSeparator.Visible = true;
        }
        else
        {
            optionalPerkEntries[0].Visible = false;
            tierUpSeparator.Visible = false;
        }

        for (int i = 0; i < possibilities.Length; i++)
        {
            string perk = possibilities[i];
            if (tier > 0)
                perk = perk[..^1] + tier;
            else if (index != 5)
                perk = perk[..^1] + 5;

            if (perk == baseAlteration)
            {
                optionalPerkEntries[i + 1].Visible = false;
                continue;
            }

            optionalPerkEntries[i + 1].SetPerkAlteration(perk, true, index == 5 && !isTrap);
            optionalPerkEntries[i + 1].Visible = true;
        }
        if(index == 5 && !isTrap)
            optionalPerkEntries[possibilities.Length].Visible = false;
        for (int i = possibilities.Length; i < optionalPerkEntries.Length-1; i++)
        {
            optionalPerkEntries[i+1].Visible = false;
        }

        //reset cost visuals
        selectedReplacementPerk = null;
        for (int i = 0; i < reperkCostEntries.Length; i++)
        {
            reperkCostEntries[i].Visible = false;
        }
        reperkApplyButton.Visible = false;

        UISounds.PlaySound("WipeAppear");
        interactionBlocker.MouseFilter = MouseFilterEnum.Stop;
        var tween = GetTree().CreateTween().Parallel();
        tween.SetTrans(Tween.TransitionType.Linear);
        tween.Parallel().TweenProperty(realPerkArea, "anchor_left", -1, tweenDuration).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(realPerkArea, "anchor_right", 0, tweenDuration).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(optionalPerkArea, "anchor_left", 0, tweenDuration).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(optionalPerkArea, "anchor_right", 1, tweenDuration).SetEase(Tween.EaseType.In);
        tween.Finished += () => interactionBlocker.MouseFilter = MouseFilterEnum.Ignore;
    }

    string selectedReplacementPerk;
    public async void SelectReplacementPerk(string replacementId, int replacementIndex)
    {
        GD.Print("selecting perk: "+replacementId);
        if (linkedItem is not null)
        {
            selectedReplacementPerk = replacementId;

            JsonObject costs = null;
            if (selectedPerkIndex == 6)
            {
                costs = reperkCosts["core"].AsObject();
            }
            else if (replacementIndex==0)
            {
                costs = reperkCosts["upgrades"][selectedPerkTier-1].AsObject();
            }
            else
            {
                var elementalCheck = reperkCosts["elemental"].AsObject().FirstOrDefault(kvp => selectedReplacementPerk.Contains(kvp.Key));
                if (elementalCheck.Value is not null)
                    costs = elementalCheck.Value.AsObject();
            }
            costs ??= reperkCosts["generic"].AsObject();
            bool allCostsMet = true;
            for (int i = 0; i < costs.Count; i++)
            {
                var costItemEntry = costs.ElementAt(i);
                BanjoAssets.TryGetTemplate(costItemEntry.Key, out var costItem);
                int requiredAmount = costItemEntry.Value.GetValue<int>();
                var existingItem = (await ProfileRequests.GetProfileItems(FnProfiles.AccountItems, costItemEntry.Key)).FirstOrDefault();
                int existingAmount = existingItem.Value?["quantity"].GetValue<int>() ?? 0;
                if (existingAmount < requiredAmount)
                    allCostsMet = false;
                reperkCostEntries[i].SetItemData(existingItem.Value?.AsObject() ?? costItem.CreateInstanceOfItem(0));
                reperkCostEntries[i].Visible = true;
            }
            for (int i = costs.Count; i < reperkCostEntries.Length; i++)
            {
                reperkCostEntries[i].Visible = false;
            }
            reperkApplyButton.Visible = allCostsMet && linkedItem is not null && selectedPerkReplaceability;
        }
    }

    public void ApplyReplacementPerk()
    {
        GD.Print("applying perk: " + selectedReplacementPerk);
        if (linkedItem is not null && selectedReplacementPerk != null)
        {
            //await profile request
        }
        ClosePerkChanger();
    }

    public void ClosePerkChanger()
    {
        UISounds.PlaySound("WipeDisappear");
        interactionBlocker.MouseFilter = MouseFilterEnum.Stop;
        var tween = GetTree().CreateTween();
        tween.Parallel().TweenProperty(realPerkArea, "anchor_left", 0, tweenDuration).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(realPerkArea, "anchor_right", 1, tweenDuration).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(optionalPerkArea, "anchor_left", 1, tweenDuration).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(optionalPerkArea, "anchor_right", 2, tweenDuration).SetEase(Tween.EaseType.Out);
        tween.Finished += () => interactionBlocker.MouseFilter = MouseFilterEnum.Ignore;
    }
}
