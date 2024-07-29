using Godot;
using System;
using System.Text.Json.Nodes;
using System.Xml.Linq;

//TODO: extend gameItemEntry and rename to cardPackEntry
public partial class LlamaEntry : GameItemEntry
{
    [Signal]
    public delegate void LlamaPressedEventHandler(string itemId);

    [Export]
    bool includeNameInDescription = true;

    public void SetLinkedItemId(string value) => linkedItemId = value;
    string linkedItemId = "";

    const string defaultPreviewImage = "ExportedImages\\PinataStandardPack.png";
    public static readonly Texture2D[] llamaTierIcons = new Texture2D[]
    {
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataStandardPack.png", "Texture2D"),
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataSilver.png", "Texture2D"),
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataGold.png", "Texture2D"),
    };

    protected override void UpdateItemData(JsonObject itemInstance)
    {
        if (itemInstance is null)
            return;
        if (!itemInstance["templateId"].ToString().StartsWith("CardPack"))
        {
            base.UpdateItemData(itemInstance);
            return;
        }
        SetCardPack(itemInstance);
    }

    public static readonly Color[] llamaTierColors = new Color[]
    {
        Color.FromString("#0064ff", Colors.White),
        Color.FromString("#999999", Colors.White),
        Color.FromString("#bfbf00", Colors.White),
    };

    void SetCardPack(JsonObject cardPackInstance)
    {
        linkedItemId = cardPackInstance["templateId"].ToString();
        var cardPackTemplate = cardPackInstance?.GetTemplate();

        string name = cardPackTemplate["DisplayName"].ToString();
        int amount = cardPackInstance["quantity"].GetValue<int>();
        if (includeAmountInName)
        {
            int shopAmount = cardPackInstance["shopQuantity"]?.GetValue<int>() ?? amount;
            if (shopAmount > 0)
                name += " (" + shopAmount + " left)";
        }
        EmitSignal(SignalName.NameChanged, name);

        string description = cardPackTemplate["Description"]?.ToString();
        description = description?.Replace(". ", ".\n");
        description = description?.Replace("! ", "!\n");
        if (includeNameInDescription)
            description = name + "\n" + description;
        EmitSignal(SignalName.DescriptionChanged, description);

        string amountText = "x"+amount;
        if (amount < 0)
            amountText = "";
        EmitSignal(SignalName.AmountChanged, amountText);


        var pinataIcon = llamaTierIcons[0];
        int llamaTier = Mathf.Max(0, cardPackTemplate.GetItemRarity()-3);
        if (cardPackTemplate is not null)
        {
            if (cardPackTemplate["ImagePaths"]?["SmallPreview"]?.ToString() == defaultPreviewImage)
                pinataIcon = llamaTierIcons[llamaTier];
            else
            {
                llamaTier = 0;
                pinataIcon = cardPackTemplate.GetItemTexture();
            }
        }

        var packIcon = cardPackTemplate.GetItemTexture(null, BanjoAssets.TextureType.PackImage);
        if (name?.StartsWith("Mini") ?? true)
            packIcon = null;

        if (cardPackInstance?["attributes"]?.AsObject().ContainsKey("options") ?? false)
        {
            packIcon = pinataIcon;
            pinataIcon = llamaTierIcons[0];
        }

        EmitSignal(SignalName.IconChanged, pinataIcon);
        EmitSignal(SignalName.SubtypeIconChanged, packIcon);

        EmitSignal(SignalName.RarityChanged, llamaTierColors[llamaTier]);
    }

    public override void ClearItem()
    {
        base.ClearItem();
        linkedItemId = "";
        EmitSignal(SignalName.NameChanged, "Select a Llama");
        EmitSignal(SignalName.IconChanged, llamaTierIcons[0]);
        EmitSignal(SignalName.SubtypeIconChanged, BanjoAssets.defaultIcon);
    }

    public override void EmitPressedSignal()
    {
        EmitSignal(SignalName.LlamaPressed, linkedItemId);
    }
}
