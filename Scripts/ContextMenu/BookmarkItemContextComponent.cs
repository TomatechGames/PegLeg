using Godot;
using System;
using System.Reflection;

public partial class BookmarkItemContextComponent : BaseContextComponent
{
    public override string Id => "BookmarkItem";
    GameItem currentItem;

    public override void Update(ContextMenuHook hook)
    {
        currentItem = hook?.itemContext?.currentItem;
    }
}
