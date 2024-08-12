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
    [Export]
    Button goToLoginButton;

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
        goToLoginButton.Pressed += () =>
        {
			GetTree().ChangeSceneToFile(loginSceneFilePath);
        };

    }
}
