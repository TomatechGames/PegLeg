using Godot;
using System;
using System.Linq;

public partial class OnboardingInterface : Control
{
    [Export]
    ShaderHook curtain;
    [Export]
    Control curtainIcon;
    //download fx
    [Export]
    CodeLoginLabel loginLabel;
    [Export]
    Button rememberLoginToggle;
    [Export]
    Control accountSelectionPanel;
    [Export]
    Button continueButton;

    public override async void _Ready()
    {
        //todo: download and import external assets during runtime using resource pack(s)
        //ZipReader zr = new();
        //zr.Open("user://pack.zip");
        //bool isValid = !zr.GetFiles().Any(path => !path.StartsWith("External/"));
        //ProjectSettings.LoadResourcePack("user://pack.zip");

        bool hasBanjoAssets = BanjoAssets.ReadAllSources();

        var authableAccounts = GameAccount.GetStoredAccounts().Where(a => a.GetLocalData("DeviceDetails") is not null).ToArray();
        //TODO: if more than one account has device details, show account selector
        foreach (var a in authableAccounts)
        {
            if(await a.SetAsActiveAccount())
                break;
        }
        var account = GameAccount.activeAccount;
        if(account is not null)
        {
            //go to main interface
            return;
        }
        //play window open animation (empty virtual method)
        //otherwise, show login code panel
    }
}
