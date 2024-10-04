using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class TitleBarDragger : Control
{
	bool isDragging;
	Vector2I windowStart;
    Vector2I mouseStart;
    [Export]
    bool isMainWindow = true;
    [Export]
    Control spacer;
    [Export]
    Control title;
    [Export]
    Control windowButtons;
    [Export]
    ModalWindow settingsWindow;
    [Export(PropertyHint.ArrayType)]
    WindowSizeDragger[] sizeDraggers = Array.Empty<WindowSizeDragger>();

    bool IsMaximised => GetTree().Root.Mode == Window.ModeEnum.Maximized;
    bool hasBoarder = false;

    public override async void _Ready()
    {
        AppConfig.OnConfigChanged += OnConfigChanged;
        SetBoarder(AppConfig.Get<bool>("window", "boardered"));

        if (isMainWindow)
        {
            var window = GetWindow();
            var px = AppConfig.Get("window", "pos_x", window.Position.X);
            var py = AppConfig.Get("window", "pos_y", window.Position.Y);
            var sx = AppConfig.Get("window", "size_x", window.Size.X);
            var sy = AppConfig.Get("window", "size_y", window.Size.Y);

            window.Position = new(px, py);
            window.Size = new(sx, sy);

            if (AppConfig.Get("window", "minimised", false))
                GetTree().Root.Mode = Window.ModeEnum.Minimized;
            else if (AppConfig.Get("window", "maximised", false))
                GetTree().Root.Mode = Window.ModeEnum.Maximized;
        }


        bool hasLoadingOverlay = IsInstanceValid(LoadingOverlay.Instance);
        if (hasLoadingOverlay)
        LoadingOverlay.AddLoadingKey("TempLockout");
        await this.WaitForTimer(1);
        if (hasLoadingOverlay)
            LoadingOverlay.RemoveLoadingKey("TempLockout");
    }

    public override void _Process(double delta)
    {
        var window = GetWindow();
        if (isDragging)
            window.Position = windowStart + (DisplayServer.MouseGetPosition() - mouseStart);
    }

    public override void _ExitTree()
    {
        AppConfig.OnConfigChanged -= OnConfigChanged;
        if (isMainWindow)
        {
            var window = GetWindow();
            AppConfig.Set("window", "minimised", GetTree().Root.Mode == Window.ModeEnum.Minimized);
            AppConfig.Set("window", "maximised", GetTree().Root.Mode == Window.ModeEnum.Maximized);
            //if (GetTree().Root.Mode == Window.ModeEnum.Maximized)
            //    GetTree().Root.Mode = Window.ModeEnum.Windowed;
            AppConfig.Set("window", "pos_x", window.Position.X);
            AppConfig.Set("window", "pos_y", window.Position.Y);
            AppConfig.Set("window", "size_x", window.Size.X);
            AppConfig.Set("window", "size_y", window.Size.Y);
        }
    }

    private void OnConfigChanged(string sec, string key, JsonValue val)
    {
        if (sec != "window")
            return;
        if (key == "boardered")
            SetBoarder(val.GetValue<bool>());
    }

    void SetBoarder(bool value)
    {
        value &= isMainWindow;
        bool isUnboardering = hasBoarder && !value;
        hasBoarder = value;
        GetWindow().Borderless = !hasBoarder;
        if (windowButtons is not null)
            windowButtons.Visible = !hasBoarder;
        if(title is not null)
            title.Visible = !hasBoarder;
        if (spacer is not null)
            spacer.Visible = !hasBoarder;
        Visible = !hasBoarder;
        //MouseDefaultCursorShape = hasBoarder ? CursorShape.Arrow : CursorShape.Drag;

        UpdateRectangular();

        if (isUnboardering)
        {
            SetMaximised(GetTree().Root.Mode == Window.ModeEnum.Maximized);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (hasBoarder)
        {
            return;
        }

        if(@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                var window = GetWindow();
                bool wasDragging = isDragging;
                isDragging = mouseEvent.Pressed;
                mouseStart = DisplayServer.MouseGetPosition();
                if (IsMaximised && isDragging)
                {
                    GetTree().Root.Mode = Window.ModeEnum.Windowed;

                    var displaySize = DisplayServer.ScreenGetUsableRect();
                    var toScreenTransform = GetWindow().GetScreenTransform();
                    var grabCenterOffset = toScreenTransform * GetGlobalRect().GetCenter();

                    var targetPosition = mouseStart - ((Vector2I)grabCenterOffset);
                    targetPosition = new(
                        Mathf.Clamp(targetPosition.X, 0, displaySize.Size.X - window.Size.X),
                        Mathf.Clamp(targetPosition.Y, 0, displaySize.Size.Y - window.Size.Y)
                        );
                    window.Position = targetPosition;
                    UpdateRectangular();
                }
                windowStart = window.Position;
                if (wasDragging && !isDragging && mouseStart.Y < 10 && isMainWindow)
                {
                    MaximiseApp();
                }
            }
        }
    }

    public void MinimiseApp()
    {
        GetTree().Root.Mode = Window.ModeEnum.Minimized;
    }

    public void MaximiseApp()
    {
        SetMaximised(!IsMaximised);
    }

    void SetMaximised(bool value)
    {
        var window = GetWindow();
        if (value)
        {
            //maximise
            var displaySize = DisplayServer.ScreenGetUsableRect();

            //preMaximisedPos = window.Position;
            //preMaximisedSize = window.Size;

            //window.Position = displaySize.Position;
            //window.Size = displaySize.Size;

            GetTree().Root.Mode = Window.ModeEnum.Maximized;
        }
        else
        {
            //unmaximise
            GetTree().Root.Mode = Window.ModeEnum.Windowed;
            //window.Position = preMaximisedPos;
            //window.Size = preMaximisedSize;
        }

        UpdateRectangular();
    }

    void UpdateRectangular()
    {
        bool isRect = IsMaximised || hasBoarder;

        RenderingServer.GlobalShaderParameterSetOverride("GlobalCorners", !isRect);

        foreach (var dragger in sizeDraggers)
        {
            dragger.Visible = !isRect;
        }
    }

    public void OpenSettings()
    {
        if (!LoadingOverlay.Instance.IsOpen)
            settingsWindow.SetWindowOpen(true);
    }

    public void CloseApp()
    {
        GetTree().Quit();
    }
}
