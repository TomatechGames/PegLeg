using Godot;
using System;
using System.Runtime.InteropServices;

public partial class Bootstrap : Node
{
    [Export]
    bool pauseAtBoot;
    [Export]
    Vector2I windowSize = new(1350, 720);
	[ExportGroup("Scenes")]
	[Export]
	PackedScene desktopOnboarding;
    [Export]
    PackedScene desktopInterface;


    public override async void _Ready()
    {
        var window = GetWindow();
        window.Position = new(-100, -100);
        Helpers.SetMainWindowVisible(false);

        AppConfig.Clear("window");
        //todo: download and import external assets during runtime using resource pack(s)
        //ZipReader zr = new();
        //zr.Open("user://pack.zip");
        //bool isValid = !zr.GetFiles().Any(path => !path.StartsWith("External/"));
        //ProjectSettings.LoadResourcePack("user://pack.zip");
        bool hasBanjoAssets = await BanjoAssets.ReadAllSources();
        var lastUsedId = AppConfig.Get<string>("account", "lastUsed");
        bool hasAccount = false;
        if (lastUsedId is not null)
        {
            GD.Print("last: " + lastUsedId);
            var lastUsedAccount = GameAccount.GetOrCreateAccount(lastUsedId);
            hasAccount = await lastUsedAccount.SetAsActiveAccount();
        }

        //TODO: if more than one account has device details, show account selector
        if (!hasAccount)
        {
            foreach (var a in GameAccount.OwnedAccounts)
            {
                if (!await a.SetAsActiveAccount())
                    continue;
                hasAccount = true;
                break;
            }
        }

        if (pauseAtBoot)
        {
            GD.Print("boot complete");
            return;
        }

        Helpers.SetMainWindowVisible(true);
        window.Size = windowSize;
        window.MoveToCenter();
        window.Transparent = false;
        window.Borderless = false;
        window.Unfocusable = false;
        await Helpers.WaitForFrame();
        await Helpers.WaitForFrame();

        //todo: autoselect desktop/mobile scenes here
        if (hasAccount)
            GetTree().ChangeSceneToPacked(desktopInterface);
        else
            GetTree().ChangeSceneToPacked(desktopOnboarding);

    }
}
