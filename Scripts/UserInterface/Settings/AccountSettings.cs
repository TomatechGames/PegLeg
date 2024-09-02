using Godot;
using System;

public partial class AccountSettings : Control
{
	[Export(PropertyHint.File, "*.tscn")]
	string loginSceneFilePath;

    [Export]
    Button generateBanjoAssets;
    [Export]
    FileDialog gameFolderDialog;

    public override void _Ready()
	{
        generateBanjoAssets.Pressed += () =>
        {
            gameFolderDialog.Show();
        };
        gameFolderDialog.DirSelected += async gameDir =>
        {
            LoadingOverlay.Instance.AddLoadingKey("banjo");
            await BanjoAssets.GenerateAssets(gameDir, () => this.WaitForFrame());
            GetTree().ReloadCurrentScene();
        };
        LoginRequests.OnLoginFailAlertPressed += ReturnToLogin;
    }

    public void ForgetAuthDebug()
    {
        LoginRequests.DebugClearToken();
    }
    public void ForgetRefreshDebug()
    {
        LoginRequests.DebugClearRefresh();
    }

    public void ReturnToLogin()
    {
        GetTree().ChangeSceneToFile(loginSceneFilePath);
    }

    public override void _ExitTree()
    {
        LoginRequests.OnLoginFailAlertPressed -= ReturnToLogin;
    }
}
