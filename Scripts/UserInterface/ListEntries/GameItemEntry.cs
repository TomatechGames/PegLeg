using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;

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
    public delegate void TooltipChangedEventHandler(string tooltip);

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
    public bool includeDescriptionInTooltip = false;
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
        if (currentItem is not null)
        {
            currentItem.OnChanged -= UpdateItem;
            currentItem.OnRemoved -= RemoveItem;
        }
    }

    public GameItem currentItem { get; protected set; }
    protected GameItem inspectorOverride;

    public void SetItem(GameItem newItem)
    {
        if (newItem == currentItem)
            return;

        if (currentItem is not null)
        {
            currentItem.OnChanged -= UpdateItem;
            currentItem.OnRemoved -= RemoveItem;
        }

        currentItem = newItem;

        if (currentItem is not null)
        {
            currentItem.OnChanged += UpdateItem;
            currentItem.OnRemoved += RemoveItem;
            UpdateItem(currentItem);
        }
        else
            ClearItem();
    }

    public void UpdateItem() => UpdateItem(currentItem);

    protected virtual void UpdateItem(GameItem item)
    {
        if (!IsInstanceValid(this) || !IsInsideTree())
            return;
        if (item.template is null)
        {
            GD.Print("uh oh");
            GD.Print(item);
        }

        bool isPermenant = item.template["IsPermanent"]?.GetValue<bool>() ?? false;

        int amount = item.quantity;
        if (item.template.Type == "Accolades")
            amount = item.template["AccoladeXP"]?.GetValue<int>() ?? 1;
        string amountText = compactifyAmount ? amount.Compactify() : amount.ToString();

        inspectorOverride = item.inspectorOverride;

        if (addXToAmount)
            amountText = "x" + amountText;
        if (amount <= (showSingleItemAmount ? 0 : 1))
            amountText = "";
        bool amountNeeded = amountText != "";

        if (item.template.DisplayName is null)
        {
            GD.Print("No Display Name:" + item);
        }

        string name = item.template.DisplayName;
        string description = item.template.Description;
        string type = item.template.Type;
        Texture2D mainIcon = item.GetTexture();

        description ??= "";
        var personalityText = item.attributes["personality"]?.ToString().Split(".")[^1][2..];
        var setBonusText = item.attributes["set_bonus"]?.ToString().Split(".")[^1][2..];
        if (type == "Worker" && name == "Survivor")
        {
            if(personalityText is not null && setBonusText is not null)
            {
                var pronoun = item.attributes["gender"]?.ToString() is string gender ? (gender == "1" ? "him" : "her") : "them";
                description = description
                    .Replace("{Gender}|gender(him, her)", pronoun)
                    .Replace("[Worker.Personality]", personalityText)
                    .Replace("[Worker.SetBonus.Buff]", setBonusText);
            }
            else
            {
                //cut off text that requires personality and set bonus if we dont know what they are
                description = description[..104];
            }
        }
        description = description.Replace(". ", ".\n");

        if (type == "Worker")
            type = "Survivor";

        float rating = selector?.overrideSurvivorSquad is string squadOverride ? item.CalculateRating(squadOverride) : item.Rating;
        string ratingText = rating == 0 ? "" : rating.ToString();

        int tier = item.template.Tier;
        float levelProgress = 0;
        int level = item.attributes?["level"]?.GetValue<int>() ?? 1;
        int bonusMaxLevel = item.attributes?["max_level_bonus"]?.GetValue<int>() ?? 0;
        int maxLevel = Mathf.Max(tier * 10, 1) + bonusMaxLevel;
        int minLevel = Mathf.Max(maxLevel - 10, 1);
        levelProgress = (level - minLevel) / (maxLevel - minLevel);

        if (type == "AccountResource" || type == "ConsumableAccountItem")
        {
            //type = Regex.Replace(type, "([A-Z])", " $1");
            type = name;
        }

        if (type != "Survivor")
        {
            EmitSignal(SignalName.PersonalityIconChanged, (Texture2D)null);
            EmitSignal(SignalName.SurvivorBoostIconChanged, (Texture2D)null);
        }

        EmitSignal(SignalName.ItemDoesExist, true);
        EmitSignal(SignalName.ItemDoesNotExist, false);

        EmitSignal(SignalName.NameChanged, name);
        EmitSignal(SignalName.DescriptionChanged, description);
        EmitSignal(SignalName.TypeChanged, type);
        EmitSignal(SignalName.RarityChanged, item.template.RarityColor);

        string tooltip = name + (amountNeeded ? $" ({(addXToAmount ? "x" : "") + amount})" : "");
        List<string> tooltipDescriptions = new();
        if (!string.IsNullOrWhiteSpace(description))
            tooltipDescriptions.Add(description);
        if (item.template["searchTags"] is JsonArray tagArray && tagArray.Count > 0)
            tooltipDescriptions.Add("Search Tags: " + tagArray.Select(t => t?.ToString()).Except(new string[] { name }).ToArray().Join(", "));
        EmitSignal(
            SignalName.TooltipChanged,
            CustomTooltip.GenerateSimpleTooltip(
                name,
                amountNeeded ? ((addXToAmount ? "x" : "") + amount) : null,
                tooltipDescriptions.Count > 0 ? tooltipDescriptions.ToArray() : null,
                item.template.RarityColor.ToHtml()
                )
            );

        EmitSignal(SignalName.IconChanged, mainIcon);
        EmitSignal(SignalName.SubtypeIconChanged, item.template.GetSubtypeTexture());
        EmitSignal(SignalName.AmmoIconChanged, item.template.GetAmmoTexture());

        EmitSignal(SignalName.AmountVisibility, amountNeeded);
        EmitSignal(SignalName.AmountChanged, amountText);

        EmitSignal(SignalName.RatingChanged, ratingText);
        EmitSignal(SignalName.RatingVisibility, rating != 0);

        //collection book algorithm is unpolished
        EmitSignal(SignalName.IsCollectable, false);
        //EmitSignal(SignalName.IsCollectable, !isPermenant && (ProfileRequests.IsItemCollectedUnsafe(itemInstance) == false));
        EmitSignal(SignalName.CanBeLeveledChanged, !isPermenant || type != "Schematic");
        EmitSignal(SignalName.LevelChanged, level);
        EmitSignal(SignalName.LevelMaxChanged, maxLevel);
        EmitSignal(SignalName.LevelProgressChanged, levelProgress);

        EmitSignal(SignalName.InteractableChanged, interactableByDefault && !preventInteractability);

        //if survivor, set personality icons

        if (type == "Survivor")
        {
            EmitSignal(SignalName.PersonalityIconChanged, item.GetTexture(FnItemTextureType.Personality));
            EmitSignal(SignalName.SurvivorBoostIconChanged, item.GetTexture(FnItemTextureType.SetBonus));
        }

        //var rarity = itemInstance.GetTemplate().GetItemRarity();
        //if (!(data.rarity < 7 && data.rarity >= 0))
        //    rarity = 0;

        EmitSignal(SignalName.NotificationChanged, !(item.IsSeen));
        EmitSignal(SignalName.MaxTierChanged, Mathf.Min(item.template.RarityLevel+ 1, 5));
        EmitSignal(SignalName.TierChanged, tier);
        EmitSignal(SignalName.SuperchargeChanged, bonusMaxLevel / 2);
    }

    void RemoveItem(GameItem item)
    {
        if (unlinkOnInvalidHandle)
            ClearItem();
    }

    public void SetRewardNotification()
    {
        //if (currentItemData is null || (currentItemData["rewardNotifSet"]?.GetValue<bool>() ?? false))
        //    return;
        //rewardNotificationRequest?.Cancel();
        //EmitSignal(SignalName.NotificationChanged, false);
        //rewardNotificationRequest = RewardNotifications.RequestNotification(currentItemData, itemInstance =>
        //{
        //    itemInstance["rewardNotifSet"] = true;
        //    EmitSignal(SignalName.NotificationChanged, !(itemInstance?["attributes"]?["item_seen"]?.GetValue<bool>() ?? false));
        //    rewardNotificationRequest = null;
        //});

    }

    public Control node => this;

    public Vector2 GetBasisSize() => CustomMinimumSize;

    public void SetInteractableSmart()
    {
        SetInteractable(TypeShouldBeInteractable(currentItem.template.Type));
    }

    public void SetInteractable(bool interactable)
    {
        EmitSignal(SignalName.InteractableChanged, interactable && (allowInteractableWhenEmpty || currentItem is not null) && !preventInteractability);
    }

    public virtual void EmitPressedSignal()
    {
        if (selectionGraphics is not null && autoSelectOnPress)
            selectionGraphics.ButtonPressed = true;
        EmitSignal(SignalName.Pressed);
    }

    public void ClearItem() => ClearItem(BanjoAssets.defaultIcon);
    public virtual void ClearItem(Texture2D clearIcon)
    {
        if (currentItem is not null)
        {
            currentItem.OnChanged -= UpdateItem;
            currentItem.OnRemoved -= RemoveItem;
            currentItem = null;
        }
        inspectorOverride = default;
        EmitSignal(SignalName.ItemDoesExist, false);
        EmitSignal(SignalName.ItemDoesNotExist, true);
        EmitSignal(SignalName.NameChanged, "");
        EmitSignal(SignalName.DescriptionChanged, "");
        EmitSignal(SignalName.IconChanged, clearIcon);
        EmitSignal(SignalName.SubtypeIconChanged, clearIcon);
        EmitSignal(SignalName.TypeChanged, "");
        EmitSignal(SignalName.AmountVisibility, false);
        EmitSignal(SignalName.AmountChanged, "");
        EmitSignal(SignalName.RarityChanged, Colors.Transparent);
        EmitSignal(SignalName.InteractableChanged, allowInteractableWhenEmpty && interactableByDefault && !preventInteractability);
        EmitSignal(SignalName.NotificationChanged, false);
    }

    public void Inspect()
    {
        if (inspectorOverride is not null)
            GameItemViewer.Instance.ShowItem(inspectorOverride);
        else
            GameItemViewer.Instance.ShowItem(currentItem);
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

    protected IRecyclableElementProvider<GameItem> itemProvider;
    protected GameItemSelector selector;
    public virtual void SetRecyclableElementProvider(IRecyclableElementProvider provider)
    {
        if (provider is GameItemSelector newSelector)
        {
            itemProvider = selector = newSelector;
        }
        else if (provider is IRecyclableElementProvider<GameItem> newHandleProvider)
        {
            itemProvider = newHandleProvider;
        }
    }

    protected int recycleIndex = 0;
    public virtual void SetRecycleIndex(int index)
    {
        if (itemProvider is not null)
        {
            recycleIndex = index;
            SetItem(itemProvider.GetRecycleElement(index));
            UpdateSelectionVisuals();
        }
    }

    public virtual void PerformRecycleSelection()
    {
        if (itemProvider is not null)
        {
            itemProvider.OnElementSelected(recycleIndex);
            UpdateSelectionVisuals();
        }
    }

    public virtual void ClearRecycleIndex() => ClearItem();

    void UpdateSelectionVisuals()
    {
        if (selector is null || selectionGraphics is null)
            return;

        bool isSelected = selector.IndexIsSelected(recycleIndex);
        selectionGraphics.ButtonPressed = isSelected;
        if (isSelected)
        {
            bool isCollectable = currentItem.isCollectedCache ?? false;
            EmitSignal(SignalName.SelectionTintChanged, isCollectable ? selector.collectionTintColor : selector.selectedTintColor);
            EmitSignal(SignalName.SelectionMarkerChanged, isCollectable ? selector.collectionMarkerTex : selector.selectedMarkerTex);
        }
    }
}
