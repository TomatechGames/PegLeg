using Godot;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

public partial class CardPackEntry : GameItemEntry
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

    const string defaultPreviewImage = "PinataStandardPack";
    static JsonObject llamaColorData;

    Color[] currentLlamaColors;
    public Gradient currentLlamaGradient { get; private set; }

    protected override void UpdateItem(GameItem item)
    {
        if (item is null)
            return;

        if (item.template.Type != "CardPack")
        {
            base.UpdateItem(item);
            return;
        }

        string name = item.template.DisplayName;
        int amount = item.customData["stackQuantity"]?.GetValue<int>() ?? item.quantity;
        int shopAmount = item.customData["shopQuantity"]?.GetValue<int>() ?? amount;
        string nameWithAmount = amount >= 0 ? $"{name} ({shopAmount} left)" : name;
        string description = item.template.Description;

        EmitSignal(SignalName.NameChanged, (includeAmountInName && shopAmount >= 0) ? nameWithAmount : name);
        EmitSignal(SignalName.DescriptionChanged, description);

        string amountText = amount.ToString();
        if (addXToAmount)
            amountText = "x" + amountText;
        if (amount <= (showSingleItemAmount ? 0 : 1))
            amountText = null;
        EmitSignal(SignalName.AmountChanged, amountText ?? null);


        int llamaTier = item.customData?["llamaTier"]?.GetValue<int>() ?? 0;
        string llamaPinataName =
            (item.template.TryGetTexturePath(FnItemTextureType.Preview, out var imagePath) ? imagePath : null)
            ?.ToString().Split("\\")[^1];
        if (llamaPinataName?.StartsWith(defaultPreviewImage) ?? false)
        {
            llamaPinataName = llamaTier switch
            {
                2 => "Gold",
                1 => "Silver",
                _ => "Standard"
            };
        }

        Color? rarityColor = null;
        if (item.attributes?.ContainsKey("options") ?? false)
            rarityColor = item.template.RarityColor;

        llamaColorData ??= OverridableFileLoader.LoadJsonFile("llamaColors.json");
        JsonArray colorData = llamaColorData?.FirstOrDefault(kvp => llamaPinataName.StartsWith(kvp.Key)).Value?.AsArray();

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
            Offsets = new float[] { 0, 0.25f, 0.5f, 0.75f },
            InterpolationMode = Gradient.InterpolationModeEnum.Constant,
        };
        currentLlamaGradient.Colors = currentLlamaColors;

        EmitSignal(SignalName.RarityChanged, currentLlamaColors[0]);
        EmitSignal(SignalName.Color1Changed, currentLlamaColors[1]);
        EmitSignal(SignalName.Color2Changed, currentLlamaColors[2]);
        EmitSignal(SignalName.Color3Changed, currentLlamaColors[3]);
        EmitSignal(SignalName.GradientChanged, currentLlamaGradient);


        var packIcon = item.GetTexture(FnItemTextureType.PackImage);
        if (!llamaPinataName.ToLower().Contains("Pinata") || (name?.Contains("Mini") ?? false))
            packIcon = null;

        EmitSignal(SignalName.IconChanged, item.GetTexture());
        EmitSignal(SignalName.SubtypeIconChanged, packIcon);
    }

    public override void ClearItem(Texture2D clearTexture)
    {
        base.ClearItem(clearTexture);
        EmitSignal(SignalName.NameChanged, "Select a Llama");
        EmitSignal(SignalName.IconChanged, GameItem.llamaTierIcons[0]);
        EmitSignal(SignalName.SubtypeIconChanged, BanjoAssets.defaultIcon);
    }

    public override void EmitPressedSignal()
    {
        if (selectionGraphics is not null)
            selectionGraphics.ButtonPressed = true;
        if (currentItem?.uuid is not null)
            EmitSignal(SignalName.LlamaPressed, currentItem.uuid);
    }
}
