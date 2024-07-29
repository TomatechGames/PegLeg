using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class TitleBarDragger : Control
{
	bool isDragging;
	Vector2I windowStart;
    Vector2I mouseStart;
    [Export]
    bool maximiseAtRoof = true;
    [Export]
    AdaptivePanelController background;
    [Export]
    ModalWindow settingsWindow;
    [Export(PropertyHint.ArrayType)]
    WindowSizeDragger[] sizeDraggers = Array.Empty<WindowSizeDragger>();

    bool isMaximised = false;
    bool isMuted = false;
    Vector2I preMaximisedPos = Vector2I.Zero;
    Vector2I preMaximisedSize = Vector2I.Zero;

    public override async void _Ready()
    {
        bool hasLoadingOverlay = IsInstanceValid(LoadingOverlay.Instance);

        if(hasLoadingOverlay)
        LoadingOverlay.Instance.AddLoadingKey("TempLockout");
        await this.WaitForTimer(1);
        if (hasLoadingOverlay)
            LoadingOverlay.Instance.RemoveLoadingKey("TempLockout");
    }

    public override void _GuiInput(InputEvent @event)
    {
        if(@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                var window = GetWindow();
                bool wasDragging = isDragging;
                isDragging = mouseEvent.Pressed;
                mouseStart = DisplayServer.MouseGetPosition();
                if (isMaximised && isDragging)
                {
                    int xSizeDifference = window.Size.X - preMaximisedSize.X;
                    float offsetScalar = (mouseStart.X- window.Position.X) / (float)window.Size.X;
                    window.Position += new Vector2I(Mathf.FloorToInt(offsetScalar*xSizeDifference),0);
                    window.Size = preMaximisedSize;
                    isMaximised = false;
                    foreach (var dragger in sizeDraggers)
                    {
                        dragger.Visible = true;
                    }
                    background?.SetShaderBool(true, "UseCorners");
                }
                windowStart = window.Position;
                if (wasDragging && !isDragging && mouseStart.Y < 10 && maximiseAtRoof)
                {
                    MaximiseApp();
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (isDragging)
            GetWindow().Position = windowStart + (DisplayServer.MouseGetPosition()-mouseStart);
    }

    public void MinimiseApp()
    {
        GetTree().Root.Mode = Window.ModeEnum.Minimized;
    }

    public void MaximiseApp()
    {
        var window = GetWindow();
        if (isMaximised)
        {
            //unmaximise
            window.Position = preMaximisedPos;
            window.Size = preMaximisedSize;

            foreach (var dragger in sizeDraggers)
            {
                dragger.Visible = true;
            }

            isMaximised = false;
        }
        else
        {
            //maximise
            var displaySize = DisplayServer.ScreenGetUsableRect();

            preMaximisedPos = window.Position;
            preMaximisedSize = window.Size;

            window.Position = displaySize.Position;
            window.Size = displaySize.Size;
            foreach (var dragger in sizeDraggers)
            {
                dragger.Visible = false;
            }

            isMaximised = true;
        }

        background?.SetShaderBool(!isMaximised, "UseCorners");
    }

    public static event Action PerformRefresh;
    public async void Refresh()
    {
        LoadingOverlay.Instance.AddLoadingKey("RefreshButton");
        await ProfileRequests.RevalidateProfiles();
        GetTree().ReloadCurrentScene();
        /*
        LoadingOverlay.Instance.AddLoadingKey("RefreshButton");
        await ProfileRequests.RevalidateProfiles();
        PerformRefresh?.Invoke();
        LoadingOverlay.Instance.RemoveLoadingKey("RefreshButton");
        */
    }

    public void OpenSettings()
    {
        settingsWindow.SetWindowOpen(true);
    }

    public void CloseApp()
    {
        GetTree().Quit();
    }
}
