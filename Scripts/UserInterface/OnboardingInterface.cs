using Godot;

public partial class OnboardingInterface : Control
{
    [Export(PropertyHint.File, "*.tscn")]
    string mainInterfacePath;
    [Export]
    float curtainOpenDuration = 0.25f;
    [Export]
    ShaderHook curtain;
    [Export]
    AudioStreamPlayer music;
    //download fx

    [ExportGroup("Login Code")]
    [Export]
    CodeLoginLabel loginLabel;
    [Export]
    Button retryLoginButton;
    [Export]
    Button continueButton;

    [ExportGroup("Account Selection")]
    [Export]
    Control accountSelectionPanel;

    static bool firstLoad = true;

    public override async void _Ready()
    {

        retryLoginButton.Visible = false;
        continueButton.Disabled = true;
        continueButton.Text = "";
        curtain.SetShaderFloat(0, "RevealScale");
        curtain.Visible = true;

        var accounts = GameAccount.OwnedAccounts;

        if (firstLoad)
        {
            AppConfig.Clear("window");
            //todo: download and import external assets during runtime using resource pack(s)
            //ZipReader zr = new();
            //zr.Open("user://pack.zip");
            //bool isValid = !zr.GetFiles().Any(path => !path.StartsWith("External/"));
            //ProjectSettings.LoadResourcePack("user://pack.zip");
            bool hasBanjoAssets = await BanjoAssets.ReadAllSources();
            var lastUsedId = AppConfig.Get<string>("account", "lastUsed");
            if (lastUsedId is not null)
            {
                var lastUsedAccount = GameAccount.GetOrCreateAccount(lastUsedId);
                await lastUsedAccount.SetAsActiveAccount();
            }

            //TODO: if more than one account has device details, show account selector
            foreach (var a in accounts)
            {
                if (await a.SetAsActiveAccount())
                    break;
            }

            if (GameAccount.activeAccount.isOwned)
            {
                LoadMainScene();
                return;
            }
        }


        MusicController.StopMusic();

        music.VolumeDb = -80;
        var musicFadeout = GetTree().CreateTween().SetParallel();
        musicFadeout.TweenProperty(music, "volume_db", 0, 1)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        music.Play();

        TweenCurtain(true);
        await Helpers.WaitForTimer(curtainOpenDuration);
        curtain.Visible = false;

        StartLogin();
    }

    void TweenCurtain(bool open)
    {
        //var iconStart = panelIcon.GlobalPosition;
        //panelIcon.AnchorTop = panelIcon.AnchorBottom = open ? 0 : 0.5f;
        //panelIcon.ResetOffsets();
        //var iconEnd = panelIcon.GlobalPosition;
        //panelIcon.GlobalPosition = iconStart;

        var tween = GetTree().CreateTween().SetParallel();
        tween.TweenProperty(curtain, "SH_RevealScale", open ? 1 : 0, curtainOpenDuration);
        //tween.TweenProperty(panelIcon, "global_position", iconEnd, curtainOpenDuration);
    }

    public void StartLogin()
    {
        codeAccountId = "";
        retryLoginButton.Visible = false;
        loginLabel.GenerateCode();
        continueButton.Text = "Waiting for approval...";
        continueButton.Disabled = true;
    }

    public void LoginCodeFail()
    {
        retryLoginButton.Visible = true;
        continueButton.Text = "Approval Failed";
    }

    public void LoginCodeSuccess(string accountId)
    {
        codeAccountId = accountId;
        continueButton.Text = "Login";
        continueButton.Disabled = false;
    }

    string codeAccountId;
    public async void ComplateCodeLogin()
    {
        if (string.IsNullOrEmpty(codeAccountId))
            return;
        var account = GameAccount.GetOrCreateAccount(codeAccountId);
        curtain.Visible = true;
        TweenCurtain(false);
        var timer = Helpers.WaitForTimer(curtainOpenDuration);
        await account.SaveDeviceDetails();
        await account.SetAsActiveAccount();
        await timer;
        LoadMainScene();
    }

    public async void ContinueToMainScene()
    {
        curtain.Visible = true;
        TweenCurtain(false);
        await Helpers.WaitForTimer(curtainOpenDuration);
    }

    void LoadMainScene()
    {
        GetTree().ChangeSceneToFile(mainInterfacePath);
        MusicController.ResumeMusic();
    }
}
