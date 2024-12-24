using Godot;
using System;
using System.Linq;
using System.Security.Principal;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public partial class GameItemViewer : ModalWindow
{
    public static GameItemViewer Instance { get; private set; }

    [Export]
    GameItemEntry displayItemEntry;
    [Export]
    TabContainer activeTabParent;
    [Export]
    Control inactiveTabParent;

    [ExportGroup("Item Upgrader")]
    [Export]
    GameItemUpgrader upgrader;


    [ExportGroup("Perk Details")]
    [Export]
    PerkViewer perkDetailsPanel;

    [ExportGroup("Hero Details")]
    [Export]
    Control heroDetailsPanel;

    [Export(PropertyHint.ArrayType)]
    HeroAbilityEntry[] heroAbilityEntries;

    [Export]
    HeroAbilityEntry heroPerkEntry;

    [Export]
    HeroAbilityEntry heroCommanderPerkEntry;

    [Export]
    GameItemEntry teamPerkEntry;

    [Export]
    Slider tierSlider;
    [Export]
    Slider levelSlider;

    [ExportGroup("Stats")]
    [Export]
    Control statsTreeContainer;
    [Export]
    Tree statsTree;

    [ExportGroup("Data")]
    [Export]
    Control devTextContainer;
    [Export]
    CodeEdit devText;

    [ExportGroup("Buttons")]
    [Export]
    Control recycleButtonPanel;
    [Export]
    Control levelupButtonPanel;
    [Export]
    Control evolveButtonPanel;
    [Export]
    Control rarityupButtonPanel;

    [ExportGroup("Choices")]
    [Export]
    Control itemChoiceParent;
    [Export]
    GameItemEntry[] itemChoiceEntries;
    [Export]
    Control[] itemChoiceLayoutSections;

    [ExportGroup("Purchases")]
    [Export]
    GameOfferEntry currentOfferEntry;
    [Export]
    Control purchasePanel;

    public override void _Ready()
    {
        base._Ready();
        Instance = this;

        levelSlider.ValueChanged += _ => RefreshHeroStats();
        tierSlider.ValueChanged += _ => RefreshHeroStats();

        for (int i = 0; i < itemChoiceEntries.Length; i++)
        {
            int val = i;
            itemChoiceEntries[i].Pressed += () => SetChoiceIndex(val);
        }

    }

    GameItem currentItem;
    GameOffer currentOffer;

    GameItem[] choices;
    int itemTier = 1;
    int itemLevel = 1;

    public void ShowItem(GameItem newItem)
    {
        itemChoiceParent.Visible = false;
        currentItem = newItem;
        SetWindowOpen(true);
        ClearShopOffer();

        upgrader.Visible = currentItem.attributes?["level"] is not null;
        upgrader.SetItem(currentItem);

        //if choice cardpack, display choices instead
        if (currentItem.template.Type == "CardPack" && (currentItem.attributes?.ContainsKey("options") ?? false))
        {
            itemChoiceParent.Visible = true;
            var optionsArr = currentItem.attributes["options"].AsArray();
            choices = new GameItem[optionsArr.Count];

            for (int i = 0; i < optionsArr.Count; i++)
            {
                var thisChoice = optionsArr[i];
                var templateId = thisChoice["itemType"].ToString().Replace("Weapon:w", "Schematic:s");
                var template = GameItemTemplate.Get(templateId);
                var choiceItem = template.CreateInstance(thisChoice["quantity"].GetValue<int>(), thisChoice["attributes"]?.AsObject().Reserialise());
                itemChoiceEntries[i].SetItem(choiceItem);
                itemChoiceEntries[i].SetRewardNotification();
                choices[i] = choiceItem;
            }

            for (int i = 0; i < optionsArr.Count-2; i++)
                itemChoiceLayoutSections[i].Visible = true;
            for (int i = optionsArr.Count - 2; i < itemChoiceLayoutSections.Length; i++)
                itemChoiceLayoutSections[i].Visible = false;

            SetDisplayItem(choices[0]);
            itemChoiceEntries[0].EmitPressedSignal();
        }
        else
        {
            SetDisplayItem(currentItem);
        }
    }

    bool showDevText = false;
    GameItem displayedItem = null;
    void SetDisplayItem(GameItem item)
    {
        if (Input.IsKeyPressed(Key.Minus))
            showDevText ^= true;
        Visible = true;
        displayItemEntry.SetItem(item);
        displayItemEntry.SetRewardNotification();
        displayedItem = item;
        
        //TODO: add extra icons for survivors, and fix descriptions

        statsTreeContainer.Reparent(inactiveTabParent);
        perkDetailsPanel.Reparent(inactiveTabParent);
        heroDetailsPanel.Reparent(inactiveTabParent);
        devTextContainer.Reparent(inactiveTabParent);

        var type = item.template.Type;
        if (type == "Hero")
        {
            //parse hero stuff
            heroDetailsPanel.Reparent(activeTabParent);
            int tier = item.template.Tier;
            var heroItems = item.template.GetHeroAbilities();
            heroPerkEntry.SetAbility(heroItems[0], false);
            heroCommanderPerkEntry.SetAbility(heroItems[1], tier < 2);
            for (int i = 0; i < 3; i++)
            {
                heroAbilityEntries[i].SetAbility(heroItems[i + 2], tier <= i);
            }
            if (item.template["UnlocksTeamPerk"]?.ToString() is string teamPerk)
            {
                teamPerkEntry.SetItem(GameItemTemplate.Get(teamPerk).CreateInstance());
                teamPerkEntry.Visible = true;
            }
            else
                teamPerkEntry.Visible = false;

            RefreshHeroStats();

            statsTreeContainer.Reparent(activeTabParent);
            //statsTree.CustomMinimumSize = statsTree.GetMinimumSize() + new Vector2(10, 0);
        }
        else if (type == "Schematic")
        {
            //parse schematic stuff
            perkDetailsPanel.Reparent(activeTabParent);
            perkDetailsPanel.SetItem(item);

            statsTreeContainer.Reparent(activeTabParent);
            statsTree.Clear();
            statsTree.Columns = 2;
            statsTree.SetColumnTitle(0, "Stat");
            statsTree.SetColumnTitle(1, "Value");
            statsTree.SetColumnClipContent(0, false);
            statsTree.SetColumnClipContent(1, false);
            statsTree.SetColumnExpand(1, false);
            statsTree.SetColumnCustomMinimumWidth(1, 90);
            var root = statsTree.CreateItem();
            JsonObject statsJson = null;
            if (item.template["RangedWeaponStats"] is JsonObject rangedWeaponStats)
                statsJson = rangedWeaponStats;
            else if (item.template["MeleeWeaponStats"] is JsonObject meleeWeaponStats)
                statsJson = meleeWeaponStats;
            else if (item.template["TrapStats"] is JsonObject trapStats)
                statsJson = trapStats;
            GenerateTreeFromJson(statsJson, root);
            //statsTree.CustomMinimumSize = statsTree.GetMinimumSize() + new Vector2(35, 0);
        }
        else if (type == "Defender")
        {
            //parse defender stuff
            if (item.attributes?.ContainsKey("alterations") ?? false)
            {
                perkDetailsPanel.Reparent(activeTabParent);
                perkDetailsPanel.SetItem(item);
            }
        }

        if (showDevText)
        {
            devText.Text = item.RawData.ToJsonString(new() { WriteIndented = true });
            devText.FoldLine(1);
            devTextContainer.Reparent(activeTabParent);
        }

        activeTabParent.Visible = activeTabParent.GetChildCount() > 0;
        if (activeTabParent.Visible)
            activeTabParent.CurrentTab = 0;
    }

    SemaphoreSlim heroStatsActiveSephamore = new(1);
    SemaphoreSlim heroStatsQueuedSephamore = new(2);
    async void RefreshHeroStats()
    {
        if (heroStatsQueuedSephamore.CurrentCount == 0)
            return;
        try
        {
            await heroStatsQueuedSephamore.WaitAsync();
            await heroStatsActiveSephamore.WaitAsync();
            var account = displayedItem.profile.account;
            var fortStats = account.FortStats;

            statsTree.Clear();
            statsTree.Columns = 3;
            statsTree.SetColumnTitle(0, "Stat");
            statsTree.SetColumnTitle(1, "Value");
            statsTree.SetColumnTitle(2, "Scaled Value");

            statsTree.SetColumnClipContent(0, false);
            statsTree.SetColumnClipContent(1, false);
            statsTree.SetColumnClipContent(2, false);

            statsTree.SetColumnExpand(0, true);
            statsTree.SetColumnExpand(1, false);
            statsTree.SetColumnExpand(2, false);

            statsTree.SetColumnCustomMinimumWidth(1, 40);
            statsTree.SetColumnCustomMinimumWidth(2, 75);

            var root = statsTree.CreateItem();
            AddStatTreeEntry(root, "Health", displayedItem.GetHeroStat(HeroStats.MaxHealth), displayedItem.GetHeroStat(HeroStats.MaxHealth, (int)levelSlider.Value, (int)tierSlider.Value), fortStats.fortitude + await account.GetSurvivorBonus(SurvivorBonus.MaxHealth));
            AddStatTreeEntry(root, "Shield", displayedItem.GetHeroStat(HeroStats.MaxShields), displayedItem.GetHeroStat(HeroStats.MaxShields, (int)levelSlider.Value, (int)tierSlider.Value), fortStats.resistance + await account.GetSurvivorBonus(SurvivorBonus.MaxShields));
            AddStatTreeEntry(root, "Health Regen Rate", displayedItem.GetHeroStat(HeroStats.HealthRegenRate), displayedItem.GetHeroStat(HeroStats.HealthRegenRate, (int)levelSlider.Value, (int)tierSlider.Value), fortStats.fortitude);
            AddStatTreeEntry(root, "Shield Regen Rate", displayedItem.GetHeroStat(HeroStats.ShieldRegenRate), displayedItem.GetHeroStat(HeroStats.ShieldRegenRate, (int)levelSlider.Value, (int)tierSlider.Value), fortStats.resistance + await account.GetSurvivorBonus(SurvivorBonus.ShieldRegenRate));
            AddStatTreeEntry(root, "Ability Damage", displayedItem.GetHeroStat(HeroStats.AbilityDamage), displayedItem.GetHeroStat(HeroStats.AbilityDamage, (int)levelSlider.Value, (int)tierSlider.Value), 0, 10); //ingame UI doesnt scale these by tech, should it?
            AddStatTreeEntry(root, "Healing Modifier", displayedItem.GetHeroStat(HeroStats.HealingModifier), displayedItem.GetHeroStat(HeroStats.HealingModifier, (int)levelSlider.Value, (int)tierSlider.Value), 0, 100);

        }
        finally
        {
            heroStatsActiveSephamore.Release();
            heroStatsQueuedSephamore.Release();
        }
    }

    static void AddStatTreeEntry(TreeItem parent, string name, float baseValue = 5, float upgradeValue = 5, float scalar = 0, int roundingDivisor = 1)
    {
        var row = parent.CreateChild();
        row.SetText(0, name);
        row.SetText(1, (Mathf.Round(upgradeValue * roundingDivisor)/roundingDivisor).ToString());
        if (upgradeValue > baseValue)
            row.SetCustomColor(1, Colors.Green);
        else if (upgradeValue < baseValue)
            row.SetCustomColor(1, Colors.Red);
        else
            row.SetCustomColor(1, Colors.White);
        if (scalar != 0)
            row.SetText(2, (Mathf.Round(upgradeValue * ((scalar/100)+1)*roundingDivisor)/roundingDivisor).ToString());
    }

    //todo: display final values as scaled by FORT stats
    void GenerateTreeFromJson(JsonObject jsonParent, TreeItem treeParent)
    {
        foreach (var item in jsonParent)
        {
            string fixedName = Regex.Replace(item.Key, "[A-Z]", " $0");
            TreeItem entry = treeParent.CreateChild();
            entry.SetText(0, fixedName);
            if (item.Value is JsonObject childObject)
            {
                GenerateTreeFromJson(childObject, entry);
                //entry.Collapsed = true;
            }
            else
            {
                string fixedVal = Regex.Replace(item.Value.ToString(), "[A-Z]", " $0");

                if (item.Value.AsValue().TryGetValue(out float floatValue))
                    fixedVal = (Mathf.Round(floatValue * 100) / 100).ToString();

                if(fixedVal.StartsWith(" "))
                    fixedVal = fixedVal[1..];
                fixedVal = fixedVal.Replace("  ", " ");
                entry.SetText(1, fixedVal);
            }
        }
    }

    public void ShowShopOffer(GameOffer offer)
    {
        purchasePanel.Visible = true;
        currentOffer = offer;
        currentOfferEntry.SetOffer(currentOffer).Start();
        SetDisplayItem(currentOffer.itemGrants[0]);
    }

    void ClearShopOffer()
    {
        purchasePanel.Visible = false;
        currentOfferEntry.ClearOffer();
        currentOffer = null;
    }

    int latestPurchasablePrice = 0;

    public void SpinnerChanged(double newValue)
    {
        currentOfferEntry.SetTargetPurchaseQuantity((int)newValue);
    }

    public async void PurchaseItem()
    {
        if (currentOffer is null)
            return;
        try
        {
            LoadingOverlay.AddLoadingKey("ItemPurchase");

            var account = GameAccount.activeAccount;
            if (!await account.Authenticate())
                return;

            GD.Print("attempting to purchase offer: " + currentOffer.OfferId);
            //fake it to test purchase animation
            //GD.Print("FAKE PURCHASE");
            //ShopPurchaseAnimation.PlayAnimation(latestItem.GetTemplate().GetItemTexture(), (int)purchaseCountSpinner.Value);
            //return;

            var notifs = await account.PurchaseOffer(currentOffer, currentOfferEntry.currentPurchaseQuantity);
            GD.Print(notifs);
            SetWindowOpen(false);

            var resultItemData = notifs.First(val => val["type"].ToString() == "CatalogPurchase")["lootResult"]["items"][0];
            GameItem resultItem = new(null, null, resultItemData.AsObject());
            //await CardPackOpener.Instance.StartOpeningShopResults(resultItems.Select(val=>val.AsObject()).ToArray());

            ShopPurchaseAnimation.PlayAnimation(resultItem.GetTexture(), currentOfferEntry.currentPurchaseQuantity);
            ClearShopOffer();
        }
        finally
        {
            LoadingOverlay.RemoveLoadingKey("ItemPurchase");
        }
    }


    public void SetChoiceIndex(int index)
    {
        SetDisplayItem(choices[index]);
    }
}
