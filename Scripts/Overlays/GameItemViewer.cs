using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static HomebasePowerLevel;

public partial class GameItemViewer : ModalWindow
{
    public static GameItemViewer Instance { get; private set; }

    [Export]
    GameItemEntry primaryItemEntry;
    [Export]
    Tree statsTree;
    [Export]
    CodeEdit devText;

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


    //TODO: set up hero perks and stuff

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
    Control purchasePanel;
    [Export]
    Control outOfStockPanel;
    [Export]
    Control cantAffordPanel;
    [Export]
    SpinBox purchaseCountSpinner;
    [Export]
    TextureRect priceIcon;
    [Export]
    Label priceLabel;

    public override void _Ready()
    {
        base._Ready();
        Instance = this;

        levelSlider.ValueChanged += _ => RefreshHeroStats();
        tierSlider.ValueChanged += _ => RefreshHeroStats();

        purchaseCountSpinner.ValueChanged += SpinnerChanged;

        for (int i = 0; i < itemChoiceEntries.Length; i++)
        {
            int val = i;
            itemChoiceEntries[i].Pressed += () => SetChoiceIndex(val);
        }

    }

    public override void _ExitTree()
    {
        linkedProfileItem?.Unlink();
    }

    ProfileItemHandle linkedProfileItem;
    JsonObject linkedShopOffer;
    Action purchaseCallback;

    public async Task LinkItem(ProfileItemHandle profileItem)
    {
        itemChoiceParent.Visible = false;
        linkedProfileItem = profileItem;
        linkedShopOffer = null;
        purchasePanel.Visible = false;
        outOfStockPanel.Visible = false;
        cantAffordPanel.Visible = false;
        var itemInstance = await profileItem.GetItem();
        SetWindowOpen(true);
        await SetDisplayItem(itemInstance);
    }

    JsonObject[] choices;
    public async Task SetItem(JsonObject itemInstance)
    {
        itemChoiceParent.Visible = false;
        linkedProfileItem = null;
        SetWindowOpen(true);

        purchasePanel.Visible = false;
        outOfStockPanel.Visible = false;
        cantAffordPanel.Visible = false;
        linkedShopOffer = null;
        purchaseCallback = null;

        //if choice cardpack, display choices instead
        if (itemInstance["templateId"].ToString().StartsWith("CardPack") && (itemInstance["attributes"]?.AsObject().ContainsKey("options") ?? false))
        {
            itemChoiceParent.Visible = true;
            var optionsArr = itemInstance["attributes"]["options"].AsArray();
            choices = new JsonObject[optionsArr.Count];

            for (int i=0; i<optionsArr.Count; i++)
            {
                var thisChoice = optionsArr[i];
                BanjoAssets.TryGetTemplate(thisChoice["itemType"].ToString().Replace("Weapon:w", "Schematic:s"), out var itemTemplate);
                var itemStack = itemTemplate.CreateInstanceOfItem(thisChoice["quantity"].GetValue<int>(), thisChoice["attributes"]?.AsObject().Reserialise());
                itemChoiceEntries[i].SetItemData(itemStack);
                itemChoiceEntries[i].SetRewardNotification();
                choices[i] = itemStack;
            }

            for (int i = 0; i < optionsArr.Count-2; i++)
                itemChoiceLayoutSections[i].Visible = true;
            for (int i = optionsArr.Count - 2; i < itemChoiceLayoutSections.Length; i++)
                itemChoiceLayoutSections[i].Visible = false;

            await SetDisplayItem(choices[0]);
            itemChoiceEntries[0].EmitPressedSignal();
        }
        else
        {
            await SetDisplayItem(itemInstance);
        }
    }

    JsonObject latestItem = null;
    async Task SetDisplayItem(JsonObject itemInstance)
    {
        Visible = true;
        devText.Text = itemInstance.ToString();
        if (linkedProfileItem is null)
        {
            primaryItemEntry.SetItemData(itemInstance);
            primaryItemEntry.SetRewardNotification();
        }
        else
        {
            primaryItemEntry.LinkProfileItem(linkedProfileItem);
        }
        var template = itemInstance.GetTemplate();
        latestItem = itemInstance;
        await GetFORTStats();

        //TODO: add extra icons for survivors, and fix descriptions

        statsTree.Visible = false;
        perkDetailsPanel.Visible = false;
        heroDetailsPanel.Visible = false;

        if (template["Type"].ToString() == "Hero")
        {
            //parse hero stuff
            heroDetailsPanel.Visible = true;
            int tier = template["Tier"].GetValue<int>();
            var heroItems = template.GetHeroAbilities();
            for (int i = 0; i < 3; i++)
            {
                heroAbilityEntries[i].SetAbility(heroItems["HeroAbilities"][i].AsObject(), tier<=i);
            }
            heroPerkEntry.SetAbility(heroItems["HeroPerk"].AsObject(), false);
            heroCommanderPerkEntry.SetAbility(heroItems["CommanderPerk"].AsObject(), tier<2);
            if (template["UnlocksTeamPerk"]?.ToString() is string teamPerk)
            {
                teamPerkEntry.SetItemData(BanjoAssets.TryGetTemplate(teamPerk).CreateInstanceOfItem());
                teamPerkEntry.Visible = true;
            }
            else
                teamPerkEntry.Visible = false;

            RefreshHeroStats();

            statsTree.Visible = true;
            statsTree.CustomMinimumSize = statsTree.GetMinimumSize() + new Vector2(10, 0);
        }
        else if (template["Type"].ToString() == "Schematic")
        {
            //parse schematic stuff
            perkDetailsPanel.Visible = true;
            if (linkedProfileItem is not null)
                perkDetailsPanel.LinkItem(linkedProfileItem);
            else 
                perkDetailsPanel.SetDisplayItem(itemInstance);

            statsTree.Visible = true;
            statsTree.Clear();
            statsTree.Columns = 2;
            statsTree.SetColumnTitle(0, "Stat");
            statsTree.SetColumnTitle(1, "Value");
            var root = statsTree.CreateItem();
            JsonObject statsJson = null;
            if (template.ContainsKey("RangedWeaponStats"))
                statsJson = template["RangedWeaponStats"].AsObject();
            else if (template.ContainsKey("MeleeWeaponStats"))
                statsJson = template["MeleeWeaponStats"].AsObject();
            else if (template.ContainsKey("TrapStats"))
                statsJson = template["TrapStats"].AsObject();
            GenerateTreeFromJson(statsJson, root);
            statsTree.CustomMinimumSize = statsTree.GetMinimumSize() + new Vector2(35, 0);
        }
        else if (template["Type"].ToString() == "Defender")
        {
            //parse defender stuff
            perkDetailsPanel.Visible = true;
            if (linkedProfileItem is not null)
                perkDetailsPanel.LinkItem(linkedProfileItem);
            else if (itemInstance["attributes"]?.AsObject().ContainsKey("alterations") ?? false)
                perkDetailsPanel.SetDisplayItem(itemInstance);
            else
                perkDetailsPanel.Visible = false;
        }
        //should fold template line
        devText.FoldLine(1);
    }

    void RefreshHeroStats()
    {
        var fortStats = GetFORTStatsUnsafe();
        statsTree.Clear();
        statsTree.Columns = 3;
        statsTree.SetColumnTitle(0, "Stat");
        statsTree.SetColumnTitle(1, "Value");
        statsTree.SetColumnTitle(2, "Scaled Value");
        var root = statsTree.CreateItem();
        AddStatTreeEntry(root, "Health", latestItem.GetHeroStat(HeroStats.MaxHealth, (int)levelSlider.Value, (int) tierSlider.Value), fortStats.fortitude + ProfileRequests.GetSurvivorBonusUnsafe(SurvivorBonus.MaxHealth));
        AddStatTreeEntry(root, "Shield", latestItem.GetHeroStat(HeroStats.MaxShields, (int)levelSlider.Value, (int)tierSlider.Value), fortStats.resistance + ProfileRequests.GetSurvivorBonusUnsafe(SurvivorBonus.MaxShields));
        AddStatTreeEntry(root, "Health Regen Rate", latestItem.GetHeroStat(HeroStats.HealthRegenRate, (int)levelSlider.Value, (int)tierSlider.Value), fortStats.fortitude);
        AddStatTreeEntry(root, "Shield Regen Rate", latestItem.GetHeroStat(HeroStats.ShieldRegenRate, (int)levelSlider.Value, (int)tierSlider.Value), fortStats.resistance + ProfileRequests.GetSurvivorBonusUnsafe(SurvivorBonus.ShieldRegenRate));
        AddStatTreeEntry(root, "Ability Damage", latestItem.GetHeroStat(HeroStats.AbilityDamage, (int)levelSlider.Value, (int)tierSlider.Value), 0, 10); //ingame UI doesnt scale these by tech, should it?
        AddStatTreeEntry(root, "Healing Modifier", latestItem.GetHeroStat(HeroStats.HealingModifier, (int)levelSlider.Value, (int)tierSlider.Value), 0, 100);
    }

    static void AddStatTreeEntry(TreeItem parent, string name, float baseValue = 5, float scalar = 0, int roundingDivisor = 1)
    {
        var row = parent.CreateChild();
        row.SetText(0, name);
        row.SetText(1, (Mathf.Round(baseValue*roundingDivisor)/roundingDivisor).ToString());
        if (scalar != 0)
            row.SetText(2, (Mathf.Round(baseValue*((scalar/100)+1)*roundingDivisor)/roundingDivisor).ToString());
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
                entry.Collapsed = true;
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

    public async Task LinkShopOffer(JsonObject offer, Action purchaseCallback = null)
    {
        linkedShopOffer = offer;
        this.purchaseCallback = purchaseCallback;

        string priceType = offer["prices"][0]["currencySubType"].ToString();
        int price = offer["prices"][0]["finalPrice"].GetValue<int>();
        var inInventory = await ProfileRequests.GetSumOfProfileItems(FnProfiles.AccountItems, priceType);

        int maxAffordable = price == 0 ? 999 : Mathf.FloorToInt(inInventory / price);
        int maxInStock = await offer.GetPurchaseLimitFromOffer();
        int maxAmount = Mathf.Min(maxAffordable, maxInStock);

        int maxSimultaniousAmount = Mathf.Min(int.Parse(
            offer["metaInfo"]?
                .AsArray()
                .FirstOrDefault(val => val["key"].ToString() == "MaxConcurrentPurchases")?
                ["value"]
                .ToString()
            ??
            maxAmount.ToString()
        ), maxAmount);

        purchasePanel.Visible = false;
        outOfStockPanel.Visible = false;
        cantAffordPanel.Visible = false;

        if (maxAmount > 0)
        {
            purchasePanel.Visible = true;
            latestPurchasablePrice = price;
            SpinnerChanged(purchaseCountSpinner.Value);
            purchaseCountSpinner.MaxValue = maxSimultaniousAmount;
            purchaseCountSpinner.Visible = maxSimultaniousAmount > 1;
            BanjoAssets.TryGetTemplate(priceType, out var priceTemplate);
            priceIcon.Texture = priceTemplate.GetItemTexture();
        }
        else if (maxInStock <= 0)
        {
            //out of stock
            outOfStockPanel.Visible = true;
        }
        else
        {
            //can't afford
            cantAffordPanel.Visible = true;
        }
    }
    int latestPurchasablePrice = 0;

    public void SpinnerChanged(double newValue)
    {
        priceLabel.Text = (latestPurchasablePrice * newValue).ToString();
    }
    public async void PurchaseItem()
    {
        if (linkedShopOffer is null)
            return;
        GD.Print("attempting to purchase offer: " + linkedShopOffer);
        //fake it to test purchase animation
        //GD.Print("FAKE PURCHASE");
        //ShopPurchaseAnimation.PlayAnimation(latestItem.GetTemplate().GetItemTexture(), (int)purchaseCountSpinner.Value);
        //return;

        JsonObject body = new()
        {
            ["offerId"] = linkedShopOffer["offerId"].ToString(),
            ["purchaseQuantity"] = purchaseCountSpinner.Value,
            ["currency"] = linkedShopOffer["prices"][0]["currencyType"].ToString(),
            ["currencySubType"] = linkedShopOffer["prices"][0]["currencySubType"].ToString(),
            ["expectedTotalPrice"] = linkedShopOffer["prices"][0]["finalPrice"].GetValue<int>() * purchaseCountSpinner.Value,
            ["gameContext"] = "Pegleg",
        };
        LoadingOverlay.Instance.AddLoadingKey("ItemPurchase");
        var result = (await ProfileRequests.PerformProfileOperation(FnProfiles.Common, "PurchaseCatalogEntry", body.ToString()));
        GD.Print(result["notifications"]);
        GD.Print(result["multiUpdate"]);
        SetWindowOpen(false);
        LoadingOverlay.Instance.RemoveLoadingKey("ItemPurchase");

        var resultItem = result["notifications"].AsArray().First(val => val["type"].ToString() == "CatalogPurchase")["lootResult"]["items"][0];
        //await CardPackOpener.Instance.StartOpeningShopResults(resultItems.Select(val=>val.AsObject()).ToArray());

        ShopPurchaseAnimation.PlayAnimation(resultItem.GetTemplate().GetItemTexture(), (int)purchaseCountSpinner.Value);
        purchaseCallback?.Invoke();
        purchaseCallback = null;
    }


    public async void SetChoiceIndex(int index)
    {
        await SetDisplayItem(choices[index]);
    }
}
