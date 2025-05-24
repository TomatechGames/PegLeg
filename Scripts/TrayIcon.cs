using Godot;
using System;

public partial class TrayIcon : StatusIndicator
{
    bool minimised = false;
    bool hasShownTutorial = false;
    PopupMenu menu;
    [Export]
    Window tutorialWindow;
    [Export]
    Timer autoclose;
    [Export]
    Control mainAppRoot;
    [Export]
    Texture2D editorIcon;
    [Export]
    Texture2D testBuildIcon;

    static TrayIcon inst;
    public override void _Ready()
    {
        inst = this;
        menu = GetNode<PopupMenu>(Menu);
        menu.IdPressed += HandleMenu;
        GetWindow().CloseRequested += Minimise;
        GetTree().AutoAcceptQuit = false;
        Pressed += (_, __) => Unminimise();
        autoclose.Timeout += () => tutorialWindow.Visible = false;
        menu.Clear();
        menu.AddItem("Close PegLeg", 404);
        if (OS.HasFeature("test"))
            Icon = testBuildIcon;
        if (OS.HasFeature("editor"))
        {
            Visible = false;
            Icon = editorIcon;
        }
    }

    public void HandleMenu(long id)
    {
        var index = menu.GetItemIndex((int)id);
        switch (id)
        {
            case 404:
                GetTree().Quit();
                break;
        }
    }

    void Minimise()
    {
        if (!minimised)
        {
            minimised = true;
            Helpers.SetMainWindowVisible(false);
            autoclose.Start();
            if (mainAppRoot is not null)
            {
                mainAppRoot.Visible = false;
                mainAppRoot.ProcessMode = ProcessModeEnum.Disabled;
            }
            if (!hasShownTutorial)
            {
                var rect = GetRect();
                var target = rect.Position;
                target.X -= tutorialWindow.Size.X * 0.5f;
                target.X += rect.Size.X * 0.5f;
                target.Y -= tutorialWindow.Size.Y + 3;
                tutorialWindow.Position = (Vector2I)target;
                tutorialWindow.Show();
                hasShownTutorial = true;
            }
            Visible = true;
        }
        
    }

    public static void UnminimiseDeferred()
    {
        if (!inst.IsInsideTree())
        {
            inst = null;
            return;
        }
        inst.CallDeferred(nameof(inst.Unminimise));
    }

    static Vector2I maximisedOffset = new(0, 23);
    public void Unminimise()
    {
        var window = GetWindow();
        if (minimised)
        {
            minimised = false;
            Helpers.SetMainWindowVisible(true);
            if (mainAppRoot is not null)
            {
                mainAppRoot.ProcessMode = ProcessModeEnum.Inherit;
                mainAppRoot.Visible = true;
            }
            if (window.Mode == Window.ModeEnum.Minimized)
            {
                var safeRect = DisplayServer.ScreenGetUsableRect(window.CurrentScreen);
                GD.Print(safeRect);
                GD.Print(new Rect2I(window.Position- maximisedOffset, window.Size+ maximisedOffset));
                if (safeRect.Position == window.Position - maximisedOffset && safeRect.Size == window.Size + maximisedOffset)
                    window.Mode = Window.ModeEnum.Maximized;
                else
                    window.Mode = Window.ModeEnum.Windowed;
            }
            var mousePos = DisplayServer.MouseGetPosition();
            var mouseScreen = DisplayServer.GetScreenFromRect(new(mousePos, new(1, 1)));
            if (window.CurrentScreen != mouseScreen)
            {
                window.CurrentScreen = mouseScreen;
                window.MoveToCenter();
            }
            if (OS.HasFeature("editor"))
                Visible = false;
        }
        window.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (minimised)
            Unminimise();
        GetWindow().CloseRequested -= Minimise;
        GetTree().AutoAcceptQuit = true;
        menu.IdPressed -= HandleMenu;
        Visible = false;
    }
}
