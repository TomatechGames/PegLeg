using Godot;
using System;
using System.Text.Json.Nodes;
using System.Xml.Linq;

//TODO: extend gameItemEntry and rename to cardPackEntry
public partial class LlamaEntry : GameItemEntry
{

    [Export]
    bool includeAmountInName = false;
    [Export]
    bool includeNameInDescription = true;

    public string linkedPressData;

    const string defaultPreviewImage = "ExportedImages\\PinataStandardPack_128.png";
    public static readonly Texture2D[] llamaTierIcons = new Texture2D[]
    {
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataStandardPack.png", "Texture2D"),
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataSilver.png", "Texture2D"),
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataGold.png", "Texture2D"),
    };

    protected override void UpdateItemData(JsonObject itemInstance)
    {
        if (!itemInstance["templateId"].ToString().StartsWith("CardPack"))
        {
            base.UpdateItemData(itemInstance);
            return;
        }
        SetCardPack(itemInstance);
    }

    void SetCardPack(JsonObject cardPackInstance)
    {
        var cardPackTemplate = cardPackInstance?.GetTemplate();

        string name = cardPackTemplate["ItemName"].ToString();
        int amount = cardPackInstance["quantity"].GetValue<int>();
        if (includeAmountInName)
        {
            if(amount > 0)
                name += " (" + amount + " left)";
            else if(amount==0)
                name += " (Sold Out)";
        }
        EmitSignal(SignalName.NameChanged, name);

        string description = cardPackTemplate["ItemDescription"]?.ToString();
        description = description?.Replace(". ", ".\n");
        description = description?.Replace("! ", "!\n");
        if (includeNameInDescription)
            description = name + "\n" + description;
        EmitSignal(SignalName.DescriptionChanged, description);

        string amountText = "x"+amount;
        if (amount < 0)
            amountText = "";
        else if (amount == 0)
            amountText = "Sold Out";
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

        EmitSignal(SignalName.RarityChanged, ShopOfferEntry.llamaTierColors[llamaTier]);
    }

    public override void ClearItem()
    {
        base.ClearItem();
        EmitSignal(SignalName.NameChanged, "Select a Llama");
        EmitSignal(SignalName.IconChanged, llamaTierIcons[0]);
        EmitSignal(SignalName.SubtypeIconChanged, BanjoAssets.defaultIcon);
    }
}
