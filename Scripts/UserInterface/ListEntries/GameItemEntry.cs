using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;

public partial class GameItemEntry : Control, IRecyclableEntry
{
    [Signal]
    public delegate void NameRelevantEventHandler(bool value);

    [Signal]
    public delegate void NameChangedEventHandler(string name);

    [Signal]
    public delegate void DescriptionChangedEventHandler(string description);

    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);

    [Signal]
    public delegate void TypeChangedEventHandler(string type);

    [Signal]
    public delegate void SubtypeIconChangedEventHandler(Texture2D icon);

    [Signal]
    public delegate void AmmoIconChangedEventHandler(Texture2D icon); //also used for trap subtype

    [Signal]
    public delegate void PersonalityIconChangedEventHandler(Texture2D icon);

    [Signal]
    public delegate void SurvivorBoostIconChangedEventHandler(Texture2D icon); //squad synergy for leads, set bonus for non-leads

    [Signal]
    public delegate void LevelChangedEventHandler(float level);

    [Signal]
    public delegate void LevelMaxChangedEventHandler(float levelMax);

    [Signal]
    public delegate void LevelProgressChangedEventHandler(float levelProgress);

    [Signal]
    public delegate void RatingChangedEventHandler(string rating);

    [Signal]
    public delegate void RatingVisibilityEventHandler(bool visibility);

    [Signal]
    public delegate void AmountChangedEventHandler(string amountText);

    [Signal]
    public delegate void AmountVisibilityEventHandler(bool visibility);

    [Signal]
    public delegate void NotificationChangedEventHandler(bool isNotificationVisible);

    [Signal]
    public delegate void InteractableChangedEventHandler(bool interactable);

    [Signal]
    public delegate void RarityChangedEventHandler(Color rarityColour);

    [Signal]
    public delegate void TierChangedEventHandler(int tier);

    [Signal]
    public delegate void SuperchargeChangedEventHandler(int supercharge);

    [Signal]
    public delegate void PressedEventHandler();

    [Export]
    bool addXToAmount;
    [Export]
    bool compactifyAmount;
    [Export]
    bool includeAmountInName;
    [Export]
    bool preventInteractability;
    [Export]
    bool interactableByDefault;
    [Export]
    bool autoLinkToViewer = true;
    [Export]
    bool autoLinkToRecycleSelection = false;
    [Export]
    bool showSingleItemAmount = false;
    [Export]
    public bool useSurvivorBoosts = false;

    /* Bad
    public struct VisibleItemData
    {
        public string name;
        public Texture2D icon;
        public string description;
        public string type;
        public int amount;
        public int rarity;
        public bool hasNotification;
        public JsonObject sourceData;

        public VisibleItemData()
        {
            name = "";
            icon = null;
            description = "";
            type = "";
            amount = 0;
            rarity = 0;
            hasNotification = false;
            sourceData = null;
        }

        public VisibleItemData(string name, Texture2D icon, int amount = 1, string description = "", int rarity = 0)
        {
            this.name = name;
            this.icon = icon;
            this.description = description;
            this.amount = amount;
            this.rarity = rarity;
            type = "";
            hasNotification = false;
            sourceData = null;
        }

        //TODO: replace VisibleItemData with a direct interpretation of item instances
        public VisibleItemData(JsonObject itemInstance, BanjoAssets.TextureType iconHint = BanjoAssets.TextureType.Preview)
        {
            var template = itemInstance.GetTemplate().AsObject();
            name = template["DisplayName"]?.ToString() ?? "?";
            description = template["Description"]?.ToString() ?? "";
            icon = template.GetItemTexture(iconHint);
            amount = itemInstance["quantity"]?.GetValue<int>() ?? 1;
            rarity = template.GetItemRarity();
            hasNotification = itemInstance?["attributes"]?["item_seen"]?.GetValue<bool>() != true;
            sourceData = itemInstance;
            type = itemInstance?["templateId"]?.ToString().Split(":")[0] ?? "";
        }

        public async Task<VisibleItemData> SetRewardNotification()
        {
            if (sourceData is null)
                return this;
            bool existsInInventory = (await ProfileRequests.GetSumOfProfileItems(FnProfiles.AccountItems, sourceData["templateId"].ToString())) > 0;
            hasNotification = !existsInInventory;
            return this;
        }
    }
    */

    public override void _Ready()
    {
        if (autoLinkToViewer)
            Pressed += Inspect;
        if (autoLinkToRecycleSelection)
            Pressed += PerformRecycleSelection;
        EmitSignal(SignalName.InteractableChanged, interactableByDefault);
    }

    public override void _ExitTree()
    {
        if (profileItem is not null)
            profileItem.OnChanged -= UpdateLinkedProfileItem;
        profileItem?.Unlink();
    }

    ProfileItemHandle profileItem;

    public void LinkProfileItem(ProfileItemHandle newProfileItem)
    {
        if(profileItem is not null)
            profileItem.OnChanged -= UpdateLinkedProfileItem;
        profileItem = newProfileItem;
        profileItem.OnChanged += UpdateLinkedProfileItem;
        UpdateLinkedProfileItem(profileItem);
    }

    public void UnlinkProfileItem()
    {
        if (profileItem is not null)
            profileItem.OnChanged -= UpdateLinkedProfileItem;
        profileItem = null;
        ClearItem();
    }

    void UpdateLinkedProfileItem(ProfileItemHandle profileItem)
    {
        currentItemData = profileItem.GetItemUnsafe();
        UpdateItemData(currentItemData);
    }

    public void SetItemData(JsonObject itemInstance)
    {
        if (profileItem is not null)
            UnlinkProfileItem();
        currentItemData = itemInstance;
        UpdateItemData(currentItemData);
    }

    protected JsonObject currentItemData;
    protected string compositeItemOverride;
    protected virtual void UpdateItemData(JsonObject itemInstance)
    {
        if (!IsInsideTree())
            return;
        var template = itemInstance.GetTemplate();
        int amount = itemInstance["quantity"].GetValue<int>();
        string amountText = compactifyAmount ? amount.Compactify() : amount.ToString();

        compositeItemOverride = itemInstance["attributes"]?["overrideItem"]?.ToString();

        if (addXToAmount)
            amountText = "x" + amountText;
        if (amount <= (showSingleItemAmount ? 0 : 1))
            amountText = "";
        bool amountNeeded = amountText != "";

        if (template?["DisplayName"]?.ToString() is null)
        {
            GD.Print(itemInstance);
        }

        string name = template["DisplayName"].ToString();
        string description = template["Description"]?.ToString();
        string type = template["Type"].ToString();
        var mainIcon = itemInstance.GetItemTexture() ?? BanjoAssets.defaultIcon;

        description ??= "";
        description = description.Replace("{Gender}|gender(him, her)", "them");
        if (type == "Worker" && name == "Survivor")
        {
            //cut off text that requires personality and set bonus if we dont know what they are
            description = description[..104];
        }
        description = description.Replace(". ", ".\n");

        if (type == "Worker")
            type = "Survivor";

        float rating = itemInstance.GetItemRating(useSurvivorBoosts);
        string ratingText = rating == 0 ? "" : rating.ToString();

        int tier = template["Tier"]?.GetValue<int>() ?? 0;
        float levelProgress = 0;
        int level = 0;
        int maxLevel = 0;
        int bonusMaxLevel = itemInstance?["attributes"]?["max_level_bonus"]?.GetValue<int>() ?? 0;
        if (itemInstance?["attributes"]?["level"]?.GetValue<int>() is int jsonLevel)
        {
            level = jsonLevel;
            maxLevel = Mathf.Max(tier * 10, 1) + bonusMaxLevel;
            int minLevel = maxLevel - 10;
            levelProgress = (level - (minLevel)) / 10f;
        }

        if (type == "AccountResource" || type == "ConsumableAccountItem")
        {
            type = name;
        }

        if (type != "Survivor")
        {
            EmitSignal(SignalName.PersonalityIconChanged, (Texture2D)null);
            EmitSignal(SignalName.SurvivorBoostIconChanged, (Texture2D)null);
        }

        EmitSignal(SignalName.NameChanged, name + (includeAmountInName && amountNeeded ? $" ({(addXToAmount ? "x" : "") + amount})" : ""));
        EmitSignal(SignalName.DescriptionChanged, description);
        EmitSignal(SignalName.TypeChanged, type);

        EmitSignal(SignalName.IconChanged, mainIcon);
        EmitSignal(SignalName.SubtypeIconChanged, template.GetItemSubtypeTexture());
        EmitSignal(SignalName.AmmoIconChanged, template.GetItemAmmoTexture());

        EmitSignal(SignalName.AmountVisibility, amountNeeded);
        EmitSignal(SignalName.AmountChanged, amountText);

        EmitSignal(SignalName.RatingChanged, ratingText);
        EmitSignal(SignalName.RatingVisibility, rating != 0);

        EmitSignal(SignalName.LevelChanged, level);
        EmitSignal(SignalName.LevelMaxChanged, maxLevel);
        EmitSignal(SignalName.LevelProgressChanged, levelProgress);

        EmitSignal(SignalName.InteractableChanged, interactableByDefault && !preventInteractability);

        //if survivor, set personality icons

        if (type == "Survivor")
        {
            EmitSignal(SignalName.PersonalityIconChanged, itemInstance.GetSurvivorPersonalityTexture());
            EmitSignal(SignalName.SurvivorBoostIconChanged, itemInstance.GetSurvivorSetTexture());
        }

        //var rarity = itemInstance.GetTemplate().GetItemRarity();
        //if (!(data.rarity < 7 && data.rarity >= 0))
        //    rarity = 0;

        LatestRarityColor = itemInstance.GetTemplate().GetItemRarityColor();
        EmitSignal(SignalName.RarityChanged, LatestRarityColor);
        EmitSignal(SignalName.NotificationChanged, !(itemInstance?["attributes"]?["item_seen"]?.GetValue<bool>() ?? false));
        EmitSignal(SignalName.TierChanged, tier);
        EmitSignal(SignalName.SuperchargeChanged, bonusMaxLevel);
    }

    public Color LatestRarityColor { get; private set; }

    public Control node => this;

    public Vector2 GetBasisSize() => CustomMinimumSize;

    public void SetInteractableSmart()
    {
        string type = currentItemData["templateId"].ToString().Split(":")[0];
        SetInteractable(TypeShouldBeInteractable(type));
    }

    public void SetInteractable(bool interactable)
    {
        EmitSignal(SignalName.InteractableChanged, interactable && !preventInteractability);
    }

    public virtual void EmitPressedSignal()
    {
        EmitSignal(SignalName.Pressed);
    }

    public virtual void ClearItem() => ClearItem(BanjoAssets.defaultIcon);
    public virtual void ClearItem(Texture2D clearIcon)
    {
        currentItemData = null;
        compositeItemOverride = null;
        EmitSignal(SignalName.NameChanged, "");
        EmitSignal(SignalName.DescriptionChanged, "");
        EmitSignal(SignalName.IconChanged, clearIcon);
        EmitSignal(SignalName.SubtypeIconChanged, clearIcon);
        EmitSignal(SignalName.TypeChanged, "");
        EmitSignal(SignalName.AmountVisibility, false);
        EmitSignal(SignalName.AmountChanged, "");
        LatestRarityColor = BanjoAssets.GetRarityColor(0);
        EmitSignal(SignalName.RarityChanged, LatestRarityColor);
        EmitSignal(SignalName.InteractableChanged, false);
        EmitSignal(SignalName.NotificationChanged, false);
    }

    public async void Inspect()
    {
        if(compositeItemOverride is not null)
        {
            await GameItemViewer.Instance.LinkItem(ProfileItemHandle.CreateHandleUnsafe(new(compositeItemOverride)));
            return;
        }
        if(profileItem is not null)
        {
            await GameItemViewer.Instance.LinkItem(profileItem);
            return;
        }
        await GameItemViewer.Instance.SetItem(currentItemData);
    }

    static readonly string[] reccomendedInteractableTypes = new string[]
    {
        "schematic",
        "hero",
        "worker",
        "defender",
        "cardpack"
    };

    public static bool TypeShouldBeInteractable(string type) => reccomendedInteractableTypes.Contains(type.ToLower());

    IRecyclableElementProvider<ProfileItemHandle> handleProvider;
    public void LinkRecyclableElementProvider(IRecyclableElementProvider provider)
    {
        if (provider is IRecyclableElementProvider<ProfileItemHandle> newHandleProvider)
        {
            handleProvider = newHandleProvider;
        }
    }

    int recycleIndex = 0;
    public void SetRecycleIndex(int index)
    {
        if (handleProvider is not null)
        {
            recycleIndex = index;
            LinkProfileItem(handleProvider.GetRecycleElement(index));
        }
    }

    public void PerformRecycleSelection()
    {
        if (handleProvider is not null)
            handleProvider.OnElementSelected(recycleIndex);
    }
}
