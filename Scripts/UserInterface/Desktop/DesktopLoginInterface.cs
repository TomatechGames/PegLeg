using Godot;
using System;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class DesktopLoginInterface : LoginInterface
{
	[Export(PropertyHint.File, "*.tscn")]
	string desktopMainInterfacePath;
    [Export]
    Control sizeTarget;
    [Export]
    Control sizeSource;
    [Export]
    Control loginControls;
    [Export]
    Control banjoControls;
    [Export]
    Control logo;
    [Export]
    Label loginText;
    [Export]
    FileDialog gameFolderDialog;
    [Export]
    LineEdit gameFolderPath;
    [Export]
    float windowOpenDuration = 0.25f;

    Vector2I existingSize;
    Vector2I smallSize;

    bool isLoggedIn = false;
    bool hasBanjoAssets = false;
    protected override async Task ReadyTask()
    {
        gameFolderDialog.DirSelected += ApplyFolder;
        GetTree().Root.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
        sizeTarget.CustomMinimumSize = Vector2.Zero;
        logo.Scale = Vector2.One;

        await this.WaitForFrame();
        await this.WaitForFrame();
        smallSize = ((Vector2I)sizeSource.Size)+(Vector2I.Down*128);
        existingSize = GetWindow().Size;
        var sizeDiff = existingSize - smallSize;
        GetWindow().Size = smallSize;
        GetWindow().Position += sizeDiff / 2;
        await this.WaitForFrame();
        await this.WaitForFrame();

        ConnectButtons();
        await this.WaitForFrame();
        hasBanjoAssets = BanjoAssets.PreloadSourcesParalell();
        var loginTask = LoginRequests.TryLogin();
        await this.WaitForTimer(0.25f);
        isLoggedIn = await loginTask;

        if (!hasAutoLoggedIn && isLoggedIn)
        {
            hasAutoLoggedIn = true;
            SwitchToMainInterface();
        }
        else
        {
            loginText.Text = isLoggedIn ? "Logged In" : (LoginRequests.IsOffline ? "OFFLINE" : "Not Logged In");

            loginControls.Visible = !isLoggedIn;
            //banjoControls.Visible = !hasBanjoAssets;
            loginButton.Disabled = !hasBanjoAssets;

            if (isLoggedIn)
                loginButton.Text = "Continue";

            TweenWindowOpen();
        }
    }

    private void ApplyFolder(string dir)
    {
        gameFolderPath.Text = dir;
    }

    public async void RunBanjoGenerator()
    {
        string programFolder = "res://External/BanjoGenerator/Release/net8.0";

        JsonObject appSettingsObj = new();
        using (var appSettingsFile = FileAccess.Open(programFolder+"/appsettings.json", FileAccess.ModeFlags.Read))
        {
            appSettingsObj = JsonNode.Parse(appSettingsFile.GetAsText()).AsObject();
        }

        JsonArray gameDirArray = appSettingsObj["GameFileOptions"]["GameDirectories"].AsArray();
        gameDirArray.Clear();
        gameDirArray.Add(gameFolderPath.Text);

        using (var appSettingsFile = FileAccess.Open(programFolder + "/appsettings.json", FileAccess.ModeFlags.Write))
        {
            appSettingsFile.StoreString(appSettingsObj.ToString());
        }

        string realProgramFolder = Helpers.ProperlyGlobalisePath(programFolder);

        string generatorProgramPath = realProgramFolder + "/BanjoBotAssets.exe";
        var banjoProcess = Process.Start(new ProcessStartInfo()
        {
            FileName = generatorProgramPath,
            UseShellExecute = true,
            WorkingDirectory = realProgramFolder
        });

        await banjoProcess.WaitForExitAsync();

        hasBanjoAssets = BanjoAssets.PreloadSourcesParalell();
        //banjoControls.Visible = !hasBanjoAssets;
        loginButton.Disabled = !hasBanjoAssets;
    }

    void TweenWindowOpen()
    {
        var tween = GetTree().CreateTween().SetParallel();
        tween.TweenProperty(sizeTarget, "custom_minimum_size", sizeSource.Size, windowOpenDuration);
        tween.TweenProperty(logo, "scale", new Vector2(0.5f, 0.5f), windowOpenDuration);
    }

    public override async Task Login()
    {
        loginButton.Disabled = true;
        await base.Login();
        if (await LoginRequests.TryLogin())
            SwitchToMainInterface();
        else
            loginButton.Disabled = false;
    }

    async void SwitchToMainInterface()
    {
        var sizeDiff = existingSize - smallSize;
        GetWindow().Size = existingSize;
        GetWindow().Position -= sizeDiff / 2;
        await this.WaitForFrame();
        await this.WaitForFrame();
        GetTree().Root.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        GetTree().ChangeSceneToFile(desktopMainInterfacePath);
    }
}
