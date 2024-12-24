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
    Control loginContent;
    [Export]
    Control loadingIcon;
    [Export]
    AudioStreamPlayer music;
    [Export]
    float windowOpenDuration = 0.25f;

    Vector2I existingSize;
    Vector2I smallSize;

    bool hasShrunk = false;
    bool isLoggedIn = false;
    bool hasBanjoAssets = false;
    protected override async Task ReadyTask()
    {
        loginContent.Visible = true;
        loadingIcon.Visible = false;
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
        window.ContentScaleFactor = 1;

        //smallSize = ((Vector2I)sizeSource.Size) + (Vector2I.Down * 128);
        //existingSize = window.Size;
        //var sizeDiff = existingSize - smallSize;
        //window.Size = smallSize;
        //window.Position += sizeDiff / 2;
        hasShrunk = true;

        sizeTarget.CustomMinimumSize = Vector2.Zero;
        logo.Scale = Vector2.One;

        ConnectButtons();
        await Helpers.WaitForFrame();
        hasBanjoAssets = BanjoAssets.ReadAllSources();
        //var loginTask = LoginRequests.TryLogin(false);
        await Helpers.WaitForTimer(0.25f);
        //isLoggedIn = await loginTask;

        if (!hasAutoLoggedIn && isLoggedIn && hasBanjoAssets)
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

            loginText.Text = isLoggedIn ? "Logged In" : (GameClient.IsOffline ? "OFFLINE" : "Not Logged In");

            loginControls.Visible = !isLoggedIn && !GameClient.IsOffline;
            banjoControls.Visible = false;
            loginButton.Disabled = !hasBanjoAssets || GameClient.IsOffline;
            loginButton.TooltipText = !hasBanjoAssets ? "Banjo Assets are missing or incomplete, please\nplace banjo assets in the \"External/Banjo\" folder and restart" : "";

            if (GameClient.IsOffline)
                loginButton.TooltipText = "Could not connect to Epic Games.\nPlease ensure you have an internet connection, and restart PegLeg.";

            if (isLoggedIn)
                loginButton.Text = "Continue";

            TweenWindowOpen();
        }
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

        if (false)
        {
            var musicFadeout = GetTree().CreateTween().SetParallel();
            musicFadeout.TweenProperty(music, "volume_db", -80, 1)
                .SetTrans(Tween.TransitionType.Expo)
                .SetEase(Tween.EaseType.In);
            await Helpers.WaitForTimer(1);
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
        OnLoginSucceeded();
        hasAutoLoggedIn = true;
        MusicController.ResumeMusic();
        if (hasShrunk)
        {
            var sizeDiff = existingSize - smallSize;
            GetWindow().Size = existingSize;
            GetWindow().Position -= sizeDiff / 2;
        }
        await Helpers.WaitForFrame();
        GetTree().Root.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        GetTree().ChangeSceneToFile(desktopMainInterfacePath);
    }
}
