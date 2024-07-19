using Godot;
using System;
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
    Control logo;
    [Export]
    float windowOpenDuration = 0.25f;

    protected override async Task ReadyTask()
    {
        sizeTarget.CustomMinimumSize = Vector2.Zero;
        logo.Scale = Vector2.One;
        ConnectButtons();
        await this.WaitForFrame();
        BanjoAssets.PreloadSourcesParalell();
        bool isLoggedIn = await LoginRequests.TryLogin();
        if (!hasAutoLoggedIn && isLoggedIn)
        {
            hasAutoLoggedIn = true;
            GetTree().ChangeSceneToFile(desktopMainInterfacePath);
        }
        else
        {
            if(isLoggedIn)
            {
                oneTimeCodeLine.Editable = false;
                generateCodeButton.Disabled = true;
                pasteButton.Disabled = true;
                loginButton.Text = "Continue";
            }

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
        await base.Login();
        if (await LoginRequests.TryLogin())
        {
            GetTree().ChangeSceneToFile(desktopMainInterfacePath);
        }
    }
}
