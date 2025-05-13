using Godot;
using System;

public partial class GameItemContextMenu : PopupMenu
{
    [Export]
    Texture2D iconTexture;
    [Export]
    Texture2D uncheckedIcon;
    [Export]
    Control attachTo;

    PopupMenu root;
    PopupMenu nestedMenuA = new();
    public override void _Ready()
    {
        iconTexture = ConvertIcon(iconTexture);
        uncheckedIcon = ConvertIcon(uncheckedIcon);

        root = this;
        var parent = GetParent();
        if (parent is MenuButton mb)
        {
            root = mb.GetPopup();
        }
        else if (parent is Control ctrl)
        {
            attachTo = ctrl;
        }

        ConfigurePopup(root);
        ConfigurePopup(nestedMenuA);

        BuildMenu();
    }

    void ConfigurePopup(PopupMenu popup)
    {
        popup.HideOnItemSelection = false;
        popup.HideOnCheckableItemSelection = false;
        popup.Transparent = true;
        popup.IdPressed += id => HandleId(popup, (int)id);
    }

    void BuildMenu()
    {
        Clear();
        root.AddItem("Item 1", 33);
        root.AddIconItem(uncheckedIcon, "Item 2 Lrg", 111);
        //root.SetItemChecked(root.ItemCount-1, true);
        root.AddSeparator();
        //root.AddCheckItem("Chk Itm", 345);

        nestedMenuA.Clear();
        CreateRadio(nestedMenuA, 1330, 1334, index => "Switch " + index switch
        {
            0 => "I",
            1 => "II",
            2 => "III",
            3 => "IV",
            4 => "V",
            _ => "err"
        });
        nestedMenuA.SetItemChecked(0, true);
        root.AddSubmenuNodeItem("Switch I", nestedMenuA, 133);
        root.AddMultistateItem("Multistate (0)", 3, 0, 1987);
    }

    static Texture2D ConvertIcon(Texture2D basis)
    {
        var iconImage = basis.GetImage();
        iconImage.Resize(16, 16);
        return ImageTexture.CreateFromImage(iconImage);
    }

    static void CreateRadio(PopupMenu menu, int fromId, int toID, Func<int, string> labelGenerator)
    {
        for (int i = fromId; i <= toID; i++)
        {
            menu.AddRadioCheckItem(labelGenerator(i - fromId), i);
        }
    }

    void HandleId(PopupMenu menu, int id)
    {
        var index = menu.GetItemIndex(id);
        switch (id)
        {
            case 33:
                menu.Visible = false; 
                break;
            case 111:
                menu.ToggleItemChecked(index);
                var isChecked = menu.IsItemChecked(index);
                menu.SetItemIcon(index, isChecked ? iconTexture : uncheckedIcon);
                break;
            case 345:
                menu.ToggleItemChecked(index);
                break;
            case 1987:
                menu.ToggleItemMultistate(index);
                var currentMultistate = menu.GetItemMultistate(index);
                menu.SetItemText(index, $"Multistate ({currentMultistate})");
                break;
            case >= 1330 and <= 1334:
                HandleRadio(menu, id, 1330, 1334);
                var nestIndex = GetItemIndex(133);
                root.SetItemText(nestIndex, menu.GetItemText(index));
                break;
        }
    }

    void HandleRadio(PopupMenu menu, int id, int fromId, int toID)
    {
        for (int i = fromId; i <= toID; i++)
        {
            var index = menu.GetItemIndex(i);
            menu.SetItemChecked(index, i == id);
        }
    }

    public void OpenMenu()
    {
        var ds = DisplayServer.Singleton;
        var targetPos = ds.MouseGetPosition();
        var oobPush = -root.Size;
        if(attachTo is not null)
        {
            var window = attachTo.GetWindow();
            var hscale = (float)window.ContentScaleSize.X / window.Size.X;
            var vscale = (float)window.ContentScaleSize.Y / window.Size.Y;
            var scale = Mathf.Max(hscale, vscale);
            var rect = attachTo.GetGlobalRect();
            var scaledPos = rect.Position / scale;
            var scaledSize = rect.Size / scale;
            targetPos = attachTo.GetWindow().Position + (Vector2I)(scaledPos + scaledSize * Vector2.Down);
            oobPush.Y -= (int)scaledSize.Y;
            oobPush.X += (int)scaledSize.X;
        }
        var screen = ds.GetScreenFromRect(new(targetPos, Vector2.One));
        //var clamp = new Rect2I(ds.ScreenGetPosition(screen), ds.ScreenGetSize(screen));
        var clamp = ds.ScreenGetUsableRect(screen);
        var clampMax = clamp.Position + (clamp.Size - root.Size);

        if (targetPos.X > clampMax.X)
            targetPos.X += oobPush.X;
        if (targetPos.Y > clampMax.Y)
            targetPos.Y += oobPush.Y;

        targetPos.X = Mathf.Min(targetPos.X, clampMax.X);
        targetPos.Y = Mathf.Min(targetPos.Y, clampMax.Y);

        targetPos.X = Mathf.Max(targetPos.X, clamp.Position.X);
        targetPos.Y = Mathf.Max(targetPos.Y, clamp.Position.Y);

        root.Position = targetPos;
        root.Visible = true;
        root.GrabFocus();
    }
}
