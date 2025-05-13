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

    public override async void _Ready()
    {
        retryLoginButton.Visible = false;
        continueButton.Disabled = true;
        continueButton.Text = "";
        curtain.SetShaderFloat(0, "RevealScale");
        curtain.Visible = true;

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
