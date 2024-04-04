using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;

[Tool]
public partial class GameItemEntry : Control
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
            name = template["ItemName"]?.ToString() ?? "?";
            description = template["ItemDescription"]?.ToString() ?? "";
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
        EmitSignal(SignalName.InteractableChanged, interactableByDefault);
    }

    ProfileItemHandle profileItem;

    public void LinkProfileItem(ProfileItemHandle profileItem)
    {
        this.profileItem = profileItem;
        profileItem.OnChanged += UpdateLinkedProfileItem;
        UpdateLinkedProfileItem(profileItem);
    }

    public void UnlinkProfileItem()
    {
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
        currentItemData = itemInstance;
        UpdateItemData(currentItemData);
    }

    protected JsonObject currentItemData;
    protected virtual void UpdateItemData(JsonObject itemInstance)
    {
        int amount = itemInstance["quantity"].GetValue<int>();
        string amountText = compactifyAmount ? amount.Compactify() : amount.ToString();

        if (addXToAmount)
            amountText = "x" + amountText;
        if (amount <= 1)
            amountText = "";
        bool amountNeeded = amountText != "";

        string name = itemInstance.GetTemplate()["ItemName"].ToString();
        string description = itemInstance.GetTemplate()["ItemDescription"]?.ToString();
        string type = itemInstance["templateId"].ToString().Split(":")[0];
        var icon = itemInstance.GetTemplate().GetItemTexture();

        EmitSignal(SignalName.NameChanged, name + (includeAmountInName && amountNeeded ? $" ({(addXToAmount ? "x" : "") + amount})" : ""));
        EmitSignal(SignalName.DescriptionChanged, description);

        EmitSignal(SignalName.IconChanged, icon);
        EmitSignal(SignalName.TypeChanged, type);

        EmitSignal(SignalName.AmountVisibility, amountNeeded);
        EmitSignal(SignalName.AmountChanged, amountText);

        EmitSignal(SignalName.InteractableChanged, interactableByDefault);

        //var rarity = itemInstance.GetTemplate().GetItemRarity();
        //if (!(data.rarity < 7 && data.rarity >= 0))
        //    rarity = 0;

        LatestRarityColor = itemInstance.GetTemplate().GetItemRarityColor();
        EmitSignal(SignalName.RarityChanged, LatestRarityColor);
        EmitSignal(SignalName.NotificationChanged, !(itemInstance?["attributes"]?["item_seen"]?.GetValue<bool>() ?? false));
    }

    public Color LatestRarityColor { get; private set; }

    public void SetInteractableSmart()
    {
        string type = currentItemData["templateId"].ToString().Split(":")[0];
        SetInteractable(TypeShouldBeInteractable(type));
    }

    public void SetInteractable(bool interactable)
    {
        EmitSignal(SignalName.InteractableChanged, interactable && !preventInteractability);
    }

    public void EmitPressedSignal()
    {
        EmitSignal(SignalName.Pressed);
    }

    public virtual void ClearItem()
    {
        currentItemData = null;
        EmitSignal(SignalName.NameChanged, "");
        EmitSignal(SignalName.DescriptionChanged, "");
        EmitSignal(SignalName.IconChanged, null);
        EmitSignal(SignalName.SubtypeIconChanged, null);
        EmitSignal(SignalName.TypeChanged, "");
        EmitSignal(SignalName.AmountVisibility, false);
        EmitSignal(SignalName.AmountChanged, "");
        LatestRarityColor = BanjoAssets.GetRarityColor(0);
        EmitSignal(SignalName.RarityChanged, LatestRarityColor);
        EmitSignal(SignalName.InteractableChanged, false);
        EmitSignal(SignalName.NotificationChanged, false);
    }

    public async void Inspect() => await GameItemViewer.Instance.SetItem(currentItemData);

    static readonly string[] reccomendedInteractableTypes = new string[]
    {
        "schematic",
        "hero",
        "worker",
        "defender",
        "cardpack"
    };

    public static bool TypeShouldBeInteractable(string type) => reccomendedInteractableTypes.Contains(type.ToLower());
}
