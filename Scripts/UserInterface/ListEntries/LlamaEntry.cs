using Godot;
using System.Linq;
using System.Text.Json.Nodes;

//TODO: extend gameItemEntry and rename to cardPackEntry
public partial class LlamaEntry : GameItemEntry
{
    [Signal]
    public delegate void LlamaPressedEventHandler(string itemId);

    [Signal]
    public delegate void Color1ChangedEventHandler(Color color);
    [Signal]
    public delegate void Color2ChangedEventHandler(Color color);
    [Signal]
    public delegate void Color3ChangedEventHandler(Color color);
    [Signal]
    public delegate void GradientChangedEventHandler(Gradient gradient);

    [Export]
    bool includeAmountInName;

    public void SetLinkedItemId(string value) => linkedItemId = value;
    string linkedItemId = "";

    const string defaultPreviewImage = "PinataStandardPack";
    public static readonly Texture2D[] llamaTierIcons = new Texture2D[]
    {
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataStandardPack.png", "Texture2D"),
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataSilver.png", "Texture2D"),
        ResourceLoader.Load<Texture2D>("res://Images/Llamas/PinataGold.png", "Texture2D"),
    };
    static JsonObject llamaColorData;

    protected override void UpdateItemData(JsonObject itemInstance, string _ = null)
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

    Color[] currentLlamaColors;
    public Gradient currentLlamaGradient { get; private set; }
    public int LlamaTier { get; private set; } = -1;

    void SetCardPack(JsonObject cardPackInstance)
    {
        linkedItemId = cardPackInstance["templateId"].ToString();
        var cardPackTemplate = cardPackInstance?.GetTemplate();

        string name = cardPackTemplate["DisplayName"].ToString();
        int amount = cardPackInstance["quantity"].GetValue<int>();
        int shopAmount = cardPackInstance["shopQuantity"]?.GetValue<int>() ?? amount;
        string nameWithAmount = amount >= 0 ? $"{name} ({shopAmount} left)" : name;
        string description = cardPackTemplate["Description"]?.ToString();

        EmitSignal(SignalName.NameChanged, (includeAmountInName && shopAmount >= 0) ? nameWithAmount : name);
        EmitSignal(SignalName.DescriptionChanged, description);

        string amountText = amount.ToString();
        if (addXToAmount)
            amountText = "x" + amountText;
        if (amount <= (showSingleItemAmount ? 0 : 1))
            amountText = null;
        EmitSignal(SignalName.AmountChanged, amountText ?? null);


        var pinataIcon = llamaTierIcons[0];
        LlamaTier = Mathf.Max(0, cardPackTemplate.GetItemRarity() - 3);
        //cardPackTemplate.GetItemTexture(pinataIcon);
        string llamaPinataName =
            (cardPackTemplate.TryGetTexturePathFromTemplate(ItemTextureType.Preview, out var imagePath) ? imagePath : null)
            ?.ToString().Split("\\")[^1];
        if (llamaPinataName?.StartsWith(defaultPreviewImage) ?? false)
        {
            pinataIcon = llamaTierIcons[LlamaTier];
            llamaPinataName = LlamaTier switch
            {
                2 => "Gold",
                1 => "Silver",
                _ => "Standard"
            };
        }
        else
        {
            LlamaTier = 0;
            pinataIcon = cardPackTemplate.GetItemTexture();
        }

        var packIcon = cardPackTemplate.GetItemTexture(null, ItemTextureType.PackImage);
        if (name is null || name.StartsWith("Mini"))
            packIcon = null;

        Color? rarityColor = null;
        if (cardPackInstance?["attributes"]?.AsObject().ContainsKey("options") ?? false)
        {
            packIcon = pinataIcon;
            pinataIcon = llamaTierIcons[0];
            rarityColor = cardPackTemplate.GetItemRarityColor();
        }

        llamaColorData ??= OverridableFileLoader.LoadJsonFile("llamaColors.json");
        JsonArray colorData = llamaColorData?.FirstOrDefault(kvp=> llamaPinataName.StartsWith(kvp.Key)).Value?.AsArray();

        currentLlamaColors = new Color[]
        {
            rarityColor ?? Color.FromString(colorData?[0]?.ToString() ?? "", new("#0073ffff")),
            Color.FromString(colorData?[1]?.ToString() ?? "", new("#e600c3e3")),
            Color.FromString(colorData?[2]?.ToString() ?? "", new("#aa00ffd4")),
            Color.FromString(colorData?[3]?.ToString() ?? "", new("#00eaff8f"))
        };


        EmitSignal(
            SignalName.TooltipChanged,
            CustomTooltip.GenerateSimpleTooltip(
                name,
                amountText,
                new string[] { description },
                currentLlamaColors[0].ToHtml()
                )
            );

        currentLlamaGradient ??= new() 
        { 
            Offsets = new float[] {0,0.25f,0.5f,0.75f},
            InterpolationMode = Gradient.InterpolationModeEnum.Constant,
        };
        currentLlamaGradient.Colors = currentLlamaColors;

        EmitSignal(SignalName.RarityChanged, currentLlamaColors[0]);
        EmitSignal(SignalName.Color1Changed, currentLlamaColors[1]);
        EmitSignal(SignalName.Color2Changed, currentLlamaColors[2]);
        EmitSignal(SignalName.Color3Changed, currentLlamaColors[3]);
        EmitSignal(SignalName.GradientChanged, currentLlamaGradient);

        EmitSignal(SignalName.IconChanged, pinataIcon);
        EmitSignal(SignalName.SubtypeIconChanged, packIcon);
    }

    public override void ClearItem()
    {
        base.ClearItem();
        linkedItemId = "";
        LlamaTier = -1;
        EmitSignal(SignalName.NameChanged, "Select a Llama");
        EmitSignal(SignalName.IconChanged, llamaTierIcons[0]);
        EmitSignal(SignalName.SubtypeIconChanged, BanjoAssets.defaultIcon);
    }

    public override void EmitPressedSignal()
    {
        if (selectionGraphics is not null)
            selectionGraphics.ButtonPressed = true;
        EmitSignal(SignalName.LlamaPressed, linkedItemId);
    }
}
