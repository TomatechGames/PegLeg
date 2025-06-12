using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ContextMenu : PopupPanel
{
    [Export]
    Control searchOn;
    [Export]
    BaseContextComponent[] contextComponents;
    [Export]
    Control componentParent;
    Dictionary<string, BaseContextComponent> contextComponentDict = [];

    public override void _Ready()
    {
        if (searchOn is null)
            return;
        searchOn.ChildEnteredTree += TryAddHook;
        foreach (var item in searchOn.FindChildren("*", "", true))
            TryAddHook(item);
        Transparent = true;
        foreach (var item in contextComponents)
        {
            contextComponentDict.TryAdd(item.Id, item);
            if(item.GetParent() is Node parent)
                parent.RemoveChild(item);
        }

        CloseRequested += OnClosed;
    }

    List<ContextMenuHook> attachedHooks = [];
    void TryAddHook(Node hookNode)
    {
        if (hookNode is not ContextMenuHook hook || attachedHooks.Contains(hook))
            return;
        attachedHooks.Add(hook);
        hook.ContextMenuTriggered += ShowMenu;
    }

    void RemoveHook(ContextMenuHook hook)
    {
        if (!attachedHooks.Contains(hook))
            return;
        attachedHooks.Remove(hook);
        hook.ContextMenuTriggered -= ShowMenu;
    }

    public override void _ExitTree()
    {
        foreach (var item in attachedHooks)
        {
            item.ContextMenuTriggered -= ShowMenu;
        }
        attachedHooks.Clear();
    }

    List<HSeparator> activeSeparators = [];
    List<BaseContextComponent> activeComponents = [];
    void ShowMenu(ContextMenuHook hook)
    {
        OnClosed();
        foreach (var item in hook.contextComponents)
        {
            if (item == "-")
            {
                HSeparator sep = new();
                componentParent.AddChild(sep);
                activeSeparators.Add(sep);
                continue;
            }
            if(contextComponentDict.TryGetValue(item, out var comp) && !activeComponents.Contains(comp))
            {
                componentParent.AddChild(comp);
                comp.Update(hook);
            }
        }


        var ds = DisplayServer.Singleton;
        var targetPos = ds.MouseGetPosition();
        var oobPush = -Size;
        if (hook.attachTo is not null)
        {
            var window = GetWindow();
            var hscale = (float)window.ContentScaleSize.X / window.Size.X;
            var vscale = (float)window.ContentScaleSize.Y / window.Size.Y;
            var scale = Mathf.Max(hscale, vscale);
            var rect = hook.attachTo.GetGlobalRect();
            var scaledPos = rect.Position / scale;
            var scaledSize = rect.Size / scale;
            bool horizontal = hook.attachHorizontally;
            targetPos = window.Position + (Vector2I)(scaledPos + scaledSize * (horizontal ? Vector2.Right : Vector2.Down));
            if (horizontal)
            {
                oobPush.Y += (int)scaledSize.Y;
                oobPush.X -= (int)scaledSize.X;
            }
            else
            {
                oobPush.Y -= (int)scaledSize.Y;
                oobPush.X += (int)scaledSize.X;
            }
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


    private void OnClosed()
    {
        foreach (var sep in activeSeparators)
        {
            componentParent.RemoveChild(sep);
            sep.QueueFree();
        }
        foreach (var comp in activeComponents)
        {
            componentParent.RemoveChild(comp);
            comp.Update(null);
        }
        activeComponents.Clear();
        activeSeparators.Clear();
    }
}
