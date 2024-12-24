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
    Control realPerkArea;

    [Export]
    Control optionalPerkArea;

    [Export]
    Control interactionBlocker;

    [Export(PropertyHint.ArrayType)]
    PerkEntry[] realPerkEntries;

    [Export(PropertyHint.ArrayType)]
    PerkEntry[] optionalPerkEntries;

    [Export]
    Control tierUpSeparator;

    [Export]
    Control reperkApplyButton;

    [Export(PropertyHint.ArrayType)]
    GameItemEntry[] reperkCostEntries;

    static JsonObject reperkCosts;

    public override void _Ready()
    {
        //todo: export this via BanjoBotAssets
        using var reperkCostFile = FileAccess.Open("res://External/reperkCosts.json", FileAccess.ModeFlags.Read);
        reperkCosts = JsonNode.Parse(reperkCostFile.GetAsText()).AsObject();

        for (int i = 0; i < realPerkEntries.Length; i++)
        {
            realPerkEntries[i].Pressed += (index, id, locked) => OpenPerkChanger(index, locked);
        }

        for (int i = 0; i < optionalPerkEntries.Length; i++)
        {
            optionalPerkEntries[i].Pressed += (index, id, locked) => SelectReplacementPerk(id, index);
        }

    }

    GameItem currentItem;
    public void SetItem(GameItem item)
    {
        if (currentItem == item)
            return;
        bool hadItem = currentItem is not null;
        if(hadItem)
            currentItem.OnChanged -= UpdateItem;

        currentItem = item;

        if (currentItem is not null)
        {
            currentItem.OnChanged += UpdateItem;
            UpdateItem(currentItem, hadItem);
        }
    }

    void UpdateItem(GameItem item) => UpdateItem(item, true);

    bool isSchematic = true;
    bool isTrap = false;
    string[] activePerks;
    string[][] perkPossibilities;
    int unlockedPerks = 1;

    void UpdateItem(GameItem item, bool animateToReset)
    {
        var maxedTemplate = item.template;

        while (maxedTemplate.TryUpgradeTemplateRarity() is GameItemTemplate upgradedTemplate)
        {
            maxedTemplate = upgradedTemplate;
        }

        isSchematic = item.template.Type == "Schematic";
        isTrap = item.template.Category == "Trap";
        int itemRarity = item.template.RarityLevel;

        if (animateToReset)
        {
            ClosePerkChanger();
        }
        else
        {
            selectedPerkIndex = -1;
            interactionBlocker.MouseFilter = MouseFilterEnum.Ignore;
            realPerkArea.AnchorLeft = 0;
            realPerkArea.AnchorRight = 1;
            optionalPerkArea.AnchorLeft = 1;
            optionalPerkArea.AnchorRight = 2;
        }

        activePerks = item.attributes?["alterations"]?
            .AsArray()
            .Select(e => e.ToString())
            .ToArray();
        if (isSchematic)
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
            activePerks ??= new string[perkPossibilities.Length];
            int itemLevel = item.attributes?["level"]?.GetValue<int>() ?? 0;
            for (int i = 0; i < perkPossibilities.Length; i++)
            {
                int requiredLevel = maxedTemplate["AlterationSlots"][i]["RequiredLevel"].GetValue<int>();
                if (requiredLevel <= itemLevel)
                    unlockedPerks = i + 1;
                else
                    break;
            }
        }
        else
            perkPossibilities = null;
        RefreshActivePerks();
    }

    void RefreshActivePerks()
    {
        activePerks ??= Array.Empty<string>();
        for (int i = 0; i < activePerks.Length; i++)
        {
            realPerkEntries[i].Visible = true;
            realPerkEntries[i].SetPerkAlteration(activePerks[i], isSchematic, i == 5 && !isTrap, i);
            if (i >= (perkPossibilities?.Length ?? 0))
            {
                realPerkEntries[i].SetInteractable(false);
                realPerkEntries[i].SetLocked(true);
                continue;
            }
            realPerkEntries[i].SetInteractable(perkPossibilities[i].Length > 1 || PerkIsUpgradeable(activePerks[i]));
            realPerkEntries[i].SetLocked(currentItem.profile is not null && i + 1 > unlockedPerks);
        }
        for (int i = activePerks.Length; i < realPerkEntries.Length; i++)
        {
            realPerkEntries[i].Visible = false;
        }
    }

    static bool PerkIsUpgradeable(string perk) =>
        perk is null ||
        perk.EndsWith("t01") ||
        perk.EndsWith("t02") ||
        perk.EndsWith("t03") ||
        perk.EndsWith("t04");

    int selectedPerkIndex = -1;
    int selectedPerkTier;
    bool selectedPerkLocked = false;
    Tween wipeTween = null;

    public void OpenPerkChanger(int index, bool isLocked = false)
    {
        //GD.Print("opening perk changer for index: " + index);
        selectedPerkLocked = isLocked;
        string baseAlteration = activePerks[index];
        string[] possibilities = perkPossibilities[index];
        string tierSource = baseAlteration?[^1..] ?? "";
        int.TryParse(tierSource, out int tier);
        if ((baseAlteration?[^2] ?? ' ') == 'v')
            tier = 0;
        bool hasUpgrade = (index != 5 || isTrap) && tier > 0 && tier < 5;

        if (!hasUpgrade && possibilities.Length==0)
        {
            //this shouldnt happen, but if it does, kablam
            return;
        }

        bool wasOpen = selectedPerkIndex != -1;
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
            else if (index != 5 || isTrap)
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

        if (wasOpen)
            return;

        UISounds.PlaySound("WipeAppear");
        interactionBlocker.MouseFilter = MouseFilterEnum.Stop;
        if (wipeTween?.IsRunning() ?? false)
            wipeTween.Kill();
        wipeTween = GetTree().CreateTween().Parallel();
        wipeTween.SetTrans(Tween.TransitionType.Linear);
        wipeTween.Parallel().TweenProperty(realPerkArea, "anchor_left", -1, tweenDuration).SetEase(Tween.EaseType.Out);
        wipeTween.Parallel().TweenProperty(realPerkArea, "anchor_right", 0, tweenDuration).SetEase(Tween.EaseType.Out);
        wipeTween.Parallel().TweenProperty(optionalPerkArea, "anchor_left", 0, tweenDuration).SetEase(Tween.EaseType.In);
        wipeTween.Parallel().TweenProperty(optionalPerkArea, "anchor_right", 1, tweenDuration).SetEase(Tween.EaseType.In);
        wipeTween.Finished += () => interactionBlocker.MouseFilter = MouseFilterEnum.Ignore;
    }

    string selectedReplacementPerk;
    public void SelectReplacementPerk(string replacementId, int replacementIndex)
    {
        if (currentItem.profile is null)
        {
            activePerks[selectedPerkIndex] = replacementId;
            RefreshActivePerks();
            ClosePerkChanger();
            return;
        }

        GD.Print("selecting perk: " + replacementId);

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
            var costItem = GameItemTemplate.Get(costItemEntry.Key);
            int requiredAmount = costItemEntry.Value.GetValue<int>();
            //can safely assume that this is the AccountItem profile, and that it has been queried
            var existingItem = currentItem.profile.GetTemplateItems(costItemEntry.Key).FirstOrDefault();
            int existingAmount = existingItem?.quantity ?? 0;
            if (existingAmount < requiredAmount)
                allCostsMet = false;
            reperkCostEntries[i].SetItem(existingItem ?? costItem.CreateInstance(0));
            reperkCostEntries[i].Visible = true;
        }
        for (int i = costs.Count; i < reperkCostEntries.Length; i++)
        {
            reperkCostEntries[i].Visible = false;
        }
        reperkApplyButton.Visible = allCostsMet && !selectedPerkLocked;
    }

    public void ApplyReplacementPerk()
    {
        GD.Print("applying perk: " + selectedReplacementPerk);
        if (currentItem.profile is not null && selectedReplacementPerk != null)
        {
            //await profile request
        }
        ClosePerkChanger();
    }

    public void ClosePerkChanger()
    {
        if (selectedPerkIndex == -1)
            return;
        selectedPerkIndex = -1;

        UISounds.PlaySound("WipeDisappear");
        interactionBlocker.MouseFilter = MouseFilterEnum.Stop;
        if (wipeTween?.IsRunning() ?? false)
            wipeTween.Kill();
        wipeTween = GetTree().CreateTween();
        wipeTween.Parallel().TweenProperty(realPerkArea, "anchor_left", 0, tweenDuration).SetEase(Tween.EaseType.In);
        wipeTween.Parallel().TweenProperty(realPerkArea, "anchor_right", 1, tweenDuration).SetEase(Tween.EaseType.In);
        wipeTween.Parallel().TweenProperty(optionalPerkArea, "anchor_left", 1, tweenDuration).SetEase(Tween.EaseType.Out);
        wipeTween.Parallel().TweenProperty(optionalPerkArea, "anchor_right", 2, tweenDuration).SetEase(Tween.EaseType.Out);
        wipeTween.Finished += () => interactionBlocker.MouseFilter = MouseFilterEnum.Ignore;
    }
}
