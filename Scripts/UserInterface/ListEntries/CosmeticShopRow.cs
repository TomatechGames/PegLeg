using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class CosmeticShopRow : Control
{
    [Export]
    PackedScene cosmeticShopEntry;
    [Export]
    Control entryParent;
    [Export]
    Control loadingCubes;
    [Export]
    Vector2 cellUnits = new(150,150);
    [Export]
    Vector2 cellSpacing = new(5,5);
    [Export]
    Control jamTrackViewerButton;
    [Export]
    Control contentNode;
    [Export]
    float largeSize = 333;
    [Export]
    float smallSize = 178;
    [Export]
    float largeContentSize = 325;
    [Export]
    float smallContentSize = 175;

    public JsonObject PageData { get; set; }

    public override void _Ready()
    {
        loadingCubes.Visible = true;
        entryParent.Visible = false;
        jamTrackViewerButton.Visible = false;
    }
    List<CosmeticShopOfferEntry> activeEntries = new();
    public async Task PopulatePage(CosmeticShopInterface parent)
    {
        if (PageData is null)
            return;
        int offerCount = 0;
        bool useJamTrackViewer = PageData.Count > 10;
        foreach (var offer in PageData)
        {
            offerCount++;
            if (offerCount > 8 && useJamTrackViewer)
                break;
            var entry = cosmeticShopEntry.Instantiate<CosmeticShopOfferEntry>();
            var splitCellText = offer.Value["tileSize"].ToString().Split("_");
            Vector2 cellSize = new(int.Parse(splitCellText[1]), int.Parse(splitCellText[3]));
            Vector2 finalSize = (cellSize * (cellUnits + cellSpacing)) - cellSpacing;
            entry.CustomMinimumSize = finalSize;
            entry.PopulateEntry(offer.Value.AsObject(), cellSize);
            entryParent.AddChild(entry);
            parent.RegisterOffer(entry);
            activeEntries.Add(entry);

            await this.WaitForFrame();
        }
        jamTrackViewerButton.Visible = useJamTrackViewer;
        entryParent.Visible = true;
        loadingCubes.Visible = false;
    }

    public bool FilterPage(Func<CosmeticShopOfferEntry, bool> filterFunc)
    {
        bool result = false;
        bool isSmall = true;
        int i = 0;
        foreach (var entry in activeEntries)
        {
            if (entry.Visible = filterFunc(entry))
            {
                result = true;
                if (PageData[entry.offerId]["tileSize"].ToString() != "Size_1_x_1" || i > 5)
                    isSmall = false;
                i++;
            }
        }
        SetSmall(isSmall);
        Visible = result;
        return result;
    }

    void SetSmall(bool small)
    {
        if (small)
        {
            CustomMinimumSize = -Vector2.Up * smallSize;
            contentNode.CustomMinimumSize = -Vector2.Up * smallContentSize;
        }
        else
        {
            CustomMinimumSize = -Vector2.Up * largeSize;
            contentNode.CustomMinimumSize = -Vector2.Up * largeContentSize;
        }
    }
}