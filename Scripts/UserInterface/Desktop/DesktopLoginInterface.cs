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
    Control loginContent;
    [Export]
    Control loadingIcon;
    [Export]
    AudioStreamPlayer music;
    [Export]
    float windowOpenDuration = 0.25f;

    Vector2I existingSize;
    Vector2I smallSize;

    bool isLoggedIn = false;
    bool hasBanjoAssets = false;
    protected override async Task ReadyTask()
    {
        loginContent.Visible = true;
        loadingIcon.Visible = false;
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
            SwitchToMainInterface();
        else
        {
            MusicController.StopMusic();

            music.VolumeDb = -80;
            var musicFadeout = GetTree().CreateTween().SetParallel();
            musicFadeout.TweenProperty(music, "volume_db", 0, 1)
                .SetTrans(Tween.TransitionType.Expo)
                .SetEase(Tween.EaseType.Out);
            music.Play();

            loginText.Text = isLoggedIn ? "Logged In" : (LoginRequests.IsDisconnected ? "OFFLINE" : "Not Logged In");

            loginControls.Visible = !isLoggedIn;
            banjoControls.Visible = !hasBanjoAssets;
            loginButton.Disabled = !hasBanjoAssets;
            loginButton.TooltipText = !hasBanjoAssets ? "Game Assets must be\ngenerated before continuing" : "";

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
        loginContent.Visible = false;
        loadingIcon.Visible = true;
        await BanjoAssets.GenerateAssets(gameFolderPath.Text);
        loginContent.Visible = true;
        loadingIcon.Visible = false;

        hasBanjoAssets = BanjoAssets.PreloadSourcesParalell();
        banjoControls.Visible = !hasBanjoAssets;
        loginButton.Disabled = !hasBanjoAssets;
        loginButton.TooltipText = !hasBanjoAssets ? "Game Assets must be\ngenerated before continuing" : "";
    }

    void TweenWindowOpen()
    {
        var tween = GetTree().CreateTween().SetParallel();
        tween.TweenProperty(sizeTarget, "custom_minimum_size", sizeSource.Size, windowOpenDuration);
        tween.TweenProperty(logo, "scale", new Vector2(0.5f, 0.5f), windowOpenDuration);
    }

    public override async Task Login()
    {
        loginContent.Visible = false;
        loadingIcon.Visible = true;
        await base.Login();
        if (await LoginRequests.TryLogin())
        {
            var musicFadeout = GetTree().CreateTween().SetParallel();
            musicFadeout.TweenProperty(music, "volume_db", -80, 1)
                .SetTrans(Tween.TransitionType.Expo)
                .SetEase(Tween.EaseType.In);
            await this.WaitForTimer(1);
            SwitchToMainInterface();
        }
        else
        {
            loginContent.Visible = true;
            loadingIcon.Visible = false;
        }
    }

    async void SwitchToMainInterface()
    {
        hasAutoLoggedIn = true;
        MusicController.ResumeMusic();
        var sizeDiff = existingSize - smallSize;
        GetWindow().Size = existingSize;
        GetWindow().Position -= sizeDiff / 2;
        await this.WaitForFrame();
        await this.WaitForFrame();
        GetTree().Root.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        GetTree().ChangeSceneToFile(desktopMainInterfacePath);
    }
}
