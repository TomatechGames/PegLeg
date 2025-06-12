using Godot;
using System;

public abstract partial class BaseContextComponent : Node
{
    public abstract string Id { get; }
    public abstract void Update(ContextMenuHook hook);
}