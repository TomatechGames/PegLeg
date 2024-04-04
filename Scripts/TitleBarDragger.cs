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
    NodePath homebaseNumberTextPath;
    [Export]
    NodePath homebaseNumberPercentPath;
    [Export]
    ShaderHook background;

    public override async void _Ready()
    {
        this.GetNodeOrNull(homebaseNumberTextPath, out Label homebaseNumberText);
        this.GetNodeOrNull(homebaseNumberPercentPath, out ProgressBar homebaseNumberPercent);
        if (homebaseNumberText is null || homebaseNumberPercent is null)
            return;
        float powerLevel = await ProfileRequests.GetHomebasePowerLevel();
        homebaseNumberText.Text = MathF.Floor(powerLevel).ToString();
        homebaseNumberPercent.Value = powerLevel % 1;
    }

    bool isMaximised = false;
    Vector2I preMaximisedPos = Vector2I.Zero;
    Vector2I preMaximisedSize = Vector2I.Zero;

    public override void _GuiInput(InputEvent @event)
    {
        if(@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                var window = GetWindow();
                isDragging = !isDragging;
                mouseStart = DisplayServer.MouseGetPosition();
                if (isMaximised)
                {
                    int xSizeDifference = window.Size.X - preMaximisedSize.X;
                    float offsetScalar = (mouseStart.X- window.Position.X) / (float)window.Size.X;
                    window.Position += new Vector2I(Mathf.FloorToInt(offsetScalar*xSizeDifference),0);
                    window.Size = preMaximisedSize;
                    isMaximised = false;
                    background.SetShaderBool(true, "UseCorners");
                }
                windowStart = window.Position;
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

            isMaximised = true;
        }

        background.SetShaderBool(!isMaximised, "UseCorners");
    }

    public static event Action PerformRefresh;
    public async void Refresh()
    {
        LoadingOverlay.Instance.AddLoadingKey("RefreshButton");
        await ProfileRequests.RevalidateProfiles();
        PerformRefresh?.Invoke();
        LoadingOverlay.Instance.RemoveLoadingKey("RefreshButton");
    }

    public void CloseApp()
    {
        GetTree().Quit();
    }
}
