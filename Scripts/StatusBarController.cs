using Godot;

public partial class StatusBarController : Control
{
    [Export]
    ModalWindow settingsWindow;

    bool IsMaximised => GetTree().Root.Mode == Window.ModeEnum.Maximized;

    public override async void _Ready()
    {
        if (IsInstanceValid(LoadingOverlay.Instance))
        {
            LoadingOverlay.AddLoadingKey("TempLockout");
            await Helpers.WaitForTimer(0.2);
            LoadingOverlay.RemoveLoadingKey("TempLockout");
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
