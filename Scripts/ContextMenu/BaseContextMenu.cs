using Godot;
using Godot.Collections;
using System;

public abstract partial class BaseContextMenu : PopupMenu
{
    protected static Texture2D ConvertIcon(Texture2D basis, int size=16)
    {
        var iconImage = basis.GetImage();
        iconImage.Resize(size, size);
        return ImageTexture.CreateFromImage(iconImage);
    }

    protected static void ConfigurePopup(PopupMenu popup)
    {
        popup.HideOnItemSelection = false;
        popup.HideOnCheckableItemSelection = false;
        popup.Transparent = true;
    }

    protected static void CreateIconToggle(PopupMenu menu, string label, int id = -1, bool initialState = false, Texture2D onTexture = null, Texture2D offTexture = null)
    {
        Dictionary<bool, Texture2D> stateDict = new()
        {
            { true, onTexture },
            { false, offTexture }
        };
        var idx = menu.ItemCount;
        menu.AddIconItem(initialState ? onTexture : offTexture, label, id);
        menu.SetItemChecked(idx, initialState);
        menu.SetItemMetadata(idx, stateDict);
    }

    protected static bool HandleIconToggle(PopupMenu menu, int index)
    {
        menu.ToggleItemChecked(index);
        var isChecked = menu.IsItemChecked(index);
        var stateDict = menu.GetItemMetadata(index).As<Dictionary<bool, Texture2D>>();
        menu.SetItemIcon(index, stateDict[isChecked]);
        return isChecked;
    }

    protected static PopupMenu CreateSwitch(string[] options, Action<int, string> switchSelected)
    {
        PopupMenu switchMenu = new();
        ConfigurePopup(switchMenu);
        for (int i = 0; i <= options.Length; i++)
        {
            switchMenu.AddRadioCheckItem(options[i], i);
        }
        switchMenu.IndexPressed += index =>
        {
            for (int i = 0; i < options.Length; i++)
            {
                switchMenu.SetItemChecked((int)index, i == index);
            }
            switchSelected?.Invoke((int)index, options[index]);
        };
        return switchMenu;
    }

    protected static void CreateRadioButtons(PopupMenu menu, int fromId, int toID, Func<int, string> labelGenerator)
    {
        for (int i = fromId; i <= toID; i++)
        {
            menu.AddRadioCheckItem(labelGenerator(i - fromId), i);
        }
    }

    protected void ShowMenu(Control attachTo = null)
    {
        var ds = DisplayServer.Singleton;
        var targetPos = ds.MouseGetPosition();
        var oobPush = -Size;
        if (attachTo is not null)
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
        var clampMax = clamp.Position + (clamp.Size - Size);

        if (targetPos.X > clampMax.X)
            targetPos.X += oobPush.X;
        if (targetPos.Y > clampMax.Y)
            targetPos.Y += oobPush.Y;

        targetPos.X = Mathf.Min(targetPos.X, clampMax.X);
        targetPos.Y = Mathf.Min(targetPos.Y, clampMax.Y);

        targetPos.X = Mathf.Max(targetPos.X, clamp.Position.X);
        targetPos.Y = Mathf.Max(targetPos.Y, clamp.Position.Y);

        Position = targetPos;
        Visible = true;
        GrabFocus();
    }
}
