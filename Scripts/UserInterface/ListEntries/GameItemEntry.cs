using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;

public partial class GameItemEntry : Control, IRecyclableEntry
{
    [Signal]
    public delegate void ItemDoesExistEventHandler(bool value);

    [Signal]
    public delegate void ItemDoesNotExistEventHandler(bool value);

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
    public delegate void IsCollectableEventHandler(bool collectable);

    [Signal]
    public delegate void CanBeLeveledChangedEventHandler(bool canBeLeveled);

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
    public delegate void MaxTierChangedEventHandler(int maxTier);

    [Signal]
    public delegate void TierChangedEventHandler(int tier);

    [Signal]
    public delegate void SuperchargeChangedEventHandler(int supercharge);

    [Signal]
    public delegate void SelectionMarkerChangedEventHandler(Texture2D marker);

    [Signal]
    public delegate void SelectionTintChangedEventHandler(Color rarityColour);

    [Signal]
    public delegate void PressedEventHandler();

    [Export]
    public bool addXToAmount;
    [Export]
    public bool compactifyAmount;
    [Export]
    public bool includeAmountInName;
    [Export]
    public bool preventInteractability;
    [Export]
    public bool interactableByDefault;
    [Export]
    public bool allowInteractableWhenEmpty;
    [Export]
    public bool autoLinkToViewer = true;
    [Export]
    public bool showSingleItemAmount = false;
    [Export]
    public bool autoLinkToRecycleSelection = false;
    [Export]
    public bool autoSelectOnPress = true;
    [Export]
    public bool unlinkOnInvalidHandle = true;
    [Export]
    protected CheckButton selectionGraphics;

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
        rewardNotificationRequest?.Cancel();
        //profileItem?.Free();
    }

    protected ProfileItemHandle profileItem;

    public void UnlinkProfileItem() => LinkProfileItem(null);
    public void LinkProfileItem(ProfileItemHandle newProfileItem)
    {
        if(profileItem is not null)
        {
            profileItem.OnChanged -= UpdateLinkedProfileItem;
            profileItem.OnRemoved -= UpdateLinkedProfileItem;
        }

        profileItem = newProfileItem;

        if (profileItem is not null)
        {
            profileItem.OnChanged += UpdateLinkedProfileItem;
            profileItem.OnRemoved += UpdateLinkedProfileItem;
        }

        UpdateLinkedProfileItem(profileItem);
    }

    public void RefreshProfileItem() => UpdateLinkedProfileItem(profileItem);

    void UpdateLinkedProfileItem(ProfileItemHandle newProfileItem)
    {
        if(newProfileItem?.isValid == false)
        {
            if (unlinkOnInvalidHandle)
                UnlinkProfileItem();
            return;
        }
        if (newProfileItem is not null)
        {
            currentItemData = newProfileItem.GetItemUnsafe();
            UpdateItemData(currentItemData, selector?.overrideSurvivorSquad);
        }
        else
        {
            ClearItem();
        }
    }

    public void SetItemData(JsonObject itemInstance)
    {
        if (profileItem is not null)
            UnlinkProfileItem();
        currentItemData = itemInstance;
        UpdateItemData(currentItemData);
    }

    protected JsonObject currentItemData;
    protected RewardNotifications.Request rewardNotificationRequest;
    protected string inspectorOverride;
    protected virtual void UpdateItemData(JsonObject itemInstance, string overrideSurvivorSquad = null)
    {
        if (!IsInstanceValid(this) || !IsInsideTree())
            return;
        rewardNotificationRequest?.Cancel();
        var template = itemInstance.GetTemplate();
        if(template is null)
        {
            GD.Print("uh oh");
            GD.Print(itemInstance);
        }
        bool isPermenant = template["IsPermanent"]?.GetValue<bool>() ?? false;

        int amount = itemInstance["quantity"].GetValue<int>();
        if (template["Type"].ToString() == "Accolades")
            amount = template["AccoladeXP"]?.GetValue<int>() ?? template["Tier"]?.GetValue<int>() ?? 1;
        string amountText = compactifyAmount ? amount.Compactify() : amount.ToString();

        inspectorOverride = itemInstance["attributes"]?["overrideItem"]?.ToString();

        if (addXToAmount)
            amountText = "x" + amountText;
        if (amount <= (showSingleItemAmount ? 0 : 1))
            amountText = "";
        bool amountNeeded = amountText != "";

        if (template?["DisplayName"]?.ToString() is null)
        {
            GD.Print("No Display Name:" + itemInstance);
        }

        string name = template["DisplayName"].ToString();
        string description = template["Description"]?.ToString();
        string type = template["Type"].ToString();
        var mainIcon = itemInstance.GetItemTexture() ?? BanjoAssets.defaultIcon;
        if (type == "TeamPerk")
            mainIcon = itemInstance.GetItemTexture(BanjoAssets.TextureType.Icon);

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

        float rating = itemInstance.GetItemRating(overrideSurvivorSquad);
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

        EmitSignal(SignalName.ItemDoesExist, true);
        EmitSignal(SignalName.ItemDoesNotExist, false);

        EmitSignal(SignalName.NameChanged, name + (includeAmountInName && amountNeeded ? $" ({(addXToAmount ? "x" : "") + amount})" : ""));
        //if (itemInstance["searchTags"] is JsonArray tagArray)
        //    EmitSignal(SignalName.NameChanged, tagArray.Select(t=>t?.ToString()).ToArray().Join("\n"));

        EmitSignal(SignalName.DescriptionChanged, description);
        EmitSignal(SignalName.TypeChanged, type);

        EmitSignal(SignalName.IconChanged, mainIcon);
        EmitSignal(SignalName.SubtypeIconChanged, template.GetItemSubtypeTexture());
        EmitSignal(SignalName.AmmoIconChanged, template.GetItemAmmoTexture());

        EmitSignal(SignalName.AmountVisibility, amountNeeded);
        EmitSignal(SignalName.AmountChanged, amountText);

        EmitSignal(SignalName.RatingChanged, ratingText);
        EmitSignal(SignalName.RatingVisibility, rating != 0);

        //collection book algorithm is unpolished
        EmitSignal(SignalName.IsCollectable, false);
        //EmitSignal(SignalName.IsCollectable, !isPermenant && (ProfileRequests.IsItemCollectedUnsafe(itemInstance) == false));
        EmitSignal(SignalName.CanBeLeveledChanged, !isPermenant);
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

        LatestRarityColor = template.GetItemRarityColor();
        if (itemInstance["templateId"].ToString().StartsWith("CardPack:zcp"))
            LatestRarityColor = Colors.Transparent;
        EmitSignal(SignalName.RarityChanged, LatestRarityColor);
        EmitSignal(SignalName.NotificationChanged, !(itemInstance?["attributes"]?["item_seen"]?.GetValue<bool>() ?? false));

        EmitSignal(SignalName.MaxTierChanged, Mathf.Min(template.GetItemRarity() + 1, 5));
        EmitSignal(SignalName.TierChanged, tier);
        EmitSignal(SignalName.SuperchargeChanged, bonusMaxLevel/2);
    }

    public void SetRewardNotification()
    {
        if (currentItemData is null || (currentItemData["rewardNotifSet"]?.GetValue<bool>() ?? false))
            return;
        rewardNotificationRequest?.Cancel();
        EmitSignal(SignalName.NotificationChanged, false);
        rewardNotificationRequest = RewardNotifications.RequestNotification(currentItemData, itemInstance =>
        {
            itemInstance["rewardNotifSet"] = true;
            EmitSignal(SignalName.NotificationChanged, !(itemInstance?["attributes"]?["item_seen"]?.GetValue<bool>() ?? false));
            rewardNotificationRequest = null;
        });
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
        EmitSignal(SignalName.InteractableChanged, interactable && (allowInteractableWhenEmpty || currentItemData is not null) && !preventInteractability);
    }

    public virtual void EmitPressedSignal()
    {
        if (selectionGraphics is not null && autoSelectOnPress)
            selectionGraphics.ButtonPressed = true;
        EmitSignal(SignalName.Pressed);
    }

    public virtual void ClearItem() => ClearItem(BanjoAssets.defaultIcon);
    public virtual void ClearItem(Texture2D clearIcon)
    {
        currentItemData = null;
        inspectorOverride = null;
        EmitSignal(SignalName.ItemDoesExist, false);
        EmitSignal(SignalName.ItemDoesNotExist, true);
        EmitSignal(SignalName.NameChanged, "");
        EmitSignal(SignalName.DescriptionChanged, "");
        EmitSignal(SignalName.IconChanged, clearIcon);
        EmitSignal(SignalName.SubtypeIconChanged, clearIcon);
        EmitSignal(SignalName.TypeChanged, "");
        EmitSignal(SignalName.AmountVisibility, false);
        EmitSignal(SignalName.AmountChanged, "");
        LatestRarityColor = BanjoAssets.GetRarityColor(0);
        EmitSignal(SignalName.RarityChanged, LatestRarityColor);
        EmitSignal(SignalName.InteractableChanged, allowInteractableWhenEmpty && interactableByDefault && !preventInteractability);
        EmitSignal(SignalName.NotificationChanged, false);
    }

    public async void Inspect()
    {
        if(inspectorOverride is not null)
        {
            await GameItemViewer.Instance.ShowItemHandle(ProfileItemHandle.CreateHandleUnsafe(new(inspectorOverride)));
            return;
        }
        if(profileItem is not null)
        {
            await GameItemViewer.Instance.ShowItemHandle(profileItem);
            return;
        }
        await GameItemViewer.Instance.ShowItem(currentItemData);
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

    protected IRecyclableElementProvider<ProfileItemHandle> handleProvider;
    protected GameItemSelector selector;
    public virtual void SetRecyclableElementProvider(IRecyclableElementProvider provider)
    {
        if (provider is GameItemSelector newSelector)
        {
            handleProvider = selector = newSelector;
        }
        else if (provider is IRecyclableElementProvider<ProfileItemHandle> newHandleProvider)
        {
            handleProvider = newHandleProvider;
        }
    }

    protected int recycleIndex = 0;
    public virtual void SetRecycleIndex(int index)
    {
        if (handleProvider is not null)
        {
            recycleIndex = index;
            LinkProfileItem(handleProvider.GetRecycleElement(index));
            UpdateSelectionVisuals();
        }
    }

    public virtual void PerformRecycleSelection()
    {
        if (handleProvider is not null)
        {
            handleProvider.OnElementSelected(recycleIndex);
            UpdateSelectionVisuals();
        }
    }

    public virtual void ClearRecycleIndex()
    {
        UnlinkProfileItem();
    }

    void UpdateSelectionVisuals()
    {
        if (selector is null || selectionGraphics is null)
            return;

        bool isSelected = selector.IndexIsSelected(recycleIndex);
        selectionGraphics.ButtonPressed = isSelected;
        if (isSelected)
        {
            //bool isCollectable = ProfileRequests.IsItemCollectedUnsafe(profileItem.GetItemUnsafe()) == false;
            //collection book algorithm is unpolished
            bool isCollectable = false;
            EmitSignal(SignalName.SelectionTintChanged, isCollectable ? selector.collectionTintColor : selector.selectedTintColor);
            EmitSignal(SignalName.SelectionMarkerChanged, isCollectable ? selector.collectionMarkerTex : selector.selectedMarkerTex);
        }
    }
}
