using Godot;
using System;
using System.Collections.Generic;

public partial class StatusBarController : Control
{
    static StatusBarController instance;
    static List<Guid> blockerTokens = new();

    [Export]
    Control leftStatusBlocker;
    [Export]
    Control rightStatusBlocker;

    public override void _Ready()
    {
        instance = this;
        UpdateBlockerVisibility();
    }

    void UpdateBlockerVisibility()
    {
        bool blockersVisible = blockerTokens.Count > 0;
        if(leftStatusBlocker is not null)
            leftStatusBlocker.Visible = blockersVisible;
        if (rightStatusBlocker is not null)
            rightStatusBlocker.Visible = blockersVisible;
    }


    public static StatusBlockerToken CreateToken() => new();

    public struct StatusBlockerToken : IDisposable
    {
        Guid guid = Guid.NewGuid();
        public StatusBlockerToken()
        {
            bool notify = blockerTokens.Count == 0;
            blockerTokens.Add(guid);
            if (notify)
                instance?.UpdateBlockerVisibility();
        }

        public void Dispose()
        {
            if (blockerTokens.Contains(guid))
            {
                blockerTokens.Remove(guid);
                if (blockerTokens.Count == 0)
                    instance?.UpdateBlockerVisibility();
            }
        }
    }
}

