using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public partial class GameItemViewer : ModalWindow
{
    public static GameItemViewer Instance { get; private set; }

    [Export]
    NodePath primaryItemEntryPath;
    GameItemEntry primaryItemEntry;

    [Export]
    NodePath descriptionTextPath;
    RichTextLabel descriptionText;

    [Export]
    NodePath extraIconsRootPath;
    Control extraIconsRoot;

    [Export]
    NodePath statsTreePath;
    Tree statsTree;

    [Export]
    NodePath devTextPath;
    CodeEdit devText;

    [ExportGroup("Perk Details")]
    [Export]
    NodePath perkDetailsPanelPath;
    PerkViewer perkDetailsPanel;

    //TODO: set up weapon/defender perks and stuff

    [ExportGroup("Hero Details")]
    [Export]
    NodePath heroDetailsPanelPath;
    Control heroDetailsPanel;

    [Export(PropertyHint.ArrayType)]
    NodePath[] heroAbilityEntryPaths = new NodePath[3];
    HeroAbilityEntry[] heroAbilityEntries;

    [Export]
    NodePath heroPerkEntryPath;
    HeroAbilityEntry heroPerkEntry;

    [Export]
    NodePath heroCommanderPerkEntryPath;
    HeroAbilityEntry heroCommanderPerkEntry;


    //TODO: set up hero perks and stuff

    [ExportGroup("Buttons")]
    [Export]
    NodePath purchaseButtonPanelPath;
    Control purchaseButtonPanel;

    [Export]
    NodePath recycleButtonPanelPath;
    Control recycleButtonPanel;

    [Export]
    NodePath levelupButtonPanelPath;
    Control levelupButtonPanel;

    [Export]
    NodePath evolveButtonPanelPath;
    Control evolveButtonPanel;

    [Export]
    NodePath rarityupButtonPanelPath;
    Control rarityupButtonPanel;

    [ExportGroup("Choices")]
    [Export]
    NodePath itemChoiceParentPath;
    Control itemChoiceParent;
    [Export]
    GameItemEntry[] itemChoiceEntries;
    [Export]
    Control[] itemChoiceLayoutSections;

    public override void _Ready()
    {
        base._Ready();
        Instance = this;

        this.GetNodeOrNull(primaryItemEntryPath, out primaryItemEntry);
        this.GetNodeOrNull(descriptionTextPath, out descriptionText);
        this.GetNodeOrNull(extraIconsRootPath, out extraIconsRoot);

        this.GetNodeOrNull(statsTreePath, out statsTree);
        this.GetNodeOrNull(perkDetailsPanelPath, out perkDetailsPanel);

        this.GetNodeOrNull(heroDetailsPanelPath, out heroDetailsPanel);
        this.GetNodesOrNull(heroAbilityEntryPaths, out heroAbilityEntries);
        this.GetNodeOrNull(heroCommanderPerkEntryPath, out heroCommanderPerkEntry);
        this.GetNodeOrNull(heroPerkEntryPath, out heroPerkEntry);

        this.GetNodeOrNull(purchaseButtonPanelPath, out purchaseButtonPanel);
        this.GetNodeOrNull(recycleButtonPanelPath, out recycleButtonPanel);
        this.GetNodeOrNull(levelupButtonPanelPath, out levelupButtonPanel);
        this.GetNodeOrNull(evolveButtonPanelPath, out evolveButtonPanel);
        this.GetNodeOrNull(rarityupButtonPanelPath, out rarityupButtonPanel);

        this.GetNodeOrNull(devTextPath, out devText);
        this.GetNodeOrNull(itemChoiceParentPath, out itemChoiceParent);

        for (int i = 0; i < itemChoiceEntries.Length; i++)
        {
            int val = i;
            itemChoiceEntries[i].Pressed += () => SetChoiceIndex(val);
        }

    }

    ProfileItemHandle linkedProfileItem;
    public async Task LinkItem(ProfileItemHandle profileItem)
    {
        itemChoiceParent.Visible = false;
        linkedProfileItem = profileItem;
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

        //if choice cardpack, display choices instead
        if (itemInstance["templateId"].ToString().StartsWith("CardPack") && itemInstance["attributes"].AsObject().ContainsKey("options"))
        {
            itemChoiceParent.Visible = true;
            var optionsArr = itemInstance["attributes"]["options"].AsArray();
            choices = new JsonObject[optionsArr.Count];

            for (int i=0; i<optionsArr.Count; i++)
            {
                var thisChoice = optionsArr[i];
                BanjoAssets.TryGetTemplate(thisChoice["itemType"].ToString(), out var itemTemplate);
                var itemStack = itemTemplate.CreateInstanceOfItem(thisChoice["quantity"].GetValue<int>(), thisChoice["attributes"].AsObject().Reserialise());
                itemChoiceEntries[i].SetItemData(new(itemStack));
                choices[i] = itemStack;
            }

            for (int i = 0; i < optionsArr.Count-2; i++)
                itemChoiceLayoutSections[i].Visible = true;
            for (int i = optionsArr.Count - 2; i < itemChoiceLayoutSections.Length; i++)
                itemChoiceLayoutSections[i].Visible = false;

            await SetDisplayItem(choices[0]);
        }
        else
        {
            await SetDisplayItem(itemInstance);
        }
    }

    async Task SetDisplayItem(JsonObject itemInstance)
    {
        Visible = true;
        devText.Text = itemInstance.ToString();
        primaryItemEntry.SetItemData(new(itemInstance));
        var template = itemInstance.GetTemplate();
        var description = template["ItemDescription"]?.ToString() ?? "";

        description = description.Replace("{Gender}|gender(him, her)", "them");

        if(template["Type"].ToString() == "Worker" && template["ItemName"].ToString()=="Survivor")
        {
            //cut off text that requires personality and set bonus, since we dont know what they are
            description = description[..104];
        }
        description = description.Replace(". ", ".\n");

        descriptionText.Text = description;

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

            var fortStats = await ProfileRequests.GetCurrentFortStats();
            statsTree.Visible = true;
            statsTree.Clear();
            statsTree.Columns = 3;
            statsTree.SetColumnTitle(0, "Stat");
            statsTree.SetColumnTitle(1, "Value");
            statsTree.SetColumnTitle(2, "Scaled Value");
            var root = statsTree.CreateItem();
            AddStatTreeEntry(root, "Health",               itemInstance.GetHeroStat(HeroStats.MaxHealth),          fortStats.fortitude);
            AddStatTreeEntry(root, "Shield",               itemInstance.GetHeroStat(HeroStats.MaxShields),         fortStats.resistance);
            AddStatTreeEntry(root, "Health Regen Rate",    itemInstance.GetHeroStat(HeroStats.HealthRegenRate),    fortStats.fortitude);
            AddStatTreeEntry(root, "Shield Regen Rate",    itemInstance.GetHeroStat(HeroStats.ShieldRegenRate),    fortStats.resistance);
            AddStatTreeEntry(root, "Ability Damage",       itemInstance.GetHeroStat(HeroStats.AbilityDamage),      fortStats.technology);
            AddStatTreeEntry(root, "Healing Modifier",     itemInstance.GetHeroStat(HeroStats.HealingModifier),    fortStats.technology);
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
            else if (itemInstance["attributes"].AsObject().ContainsKey("alterations"))
                perkDetailsPanel.SetDisplayItem(itemInstance);
            else
                perkDetailsPanel.Visible = false;
        }
        //should fold template line
        devText.FoldLine(1);
    }

    static void AddStatTreeEntry(TreeItem parent, string name, float baseValue, float scalar = 0)
    {
        var row = parent.CreateChild();
        row.SetText(0, name);
        row.SetText(1, baseValue.ToString());
        if (scalar != 0)
            row.SetText(2, (baseValue*((scalar/100)+1)).ToString());
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
                if(fixedVal.StartsWith(" "))
                    fixedVal = fixedVal[1..];
                fixedVal = fixedVal.Replace("  ", " ");
                entry.SetText(1, fixedVal);
            }
        }
    }


    public async void SetChoiceIndex(int index)
    {
        await SetDisplayItem(choices[index]);
    }
}
