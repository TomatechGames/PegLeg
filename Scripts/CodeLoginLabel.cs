using Godot;
using System;
using System.Net;
using System.Net.Http;
using System.Text;

public partial class CodeLoginLabel : Node
{
    [Signal]
    public delegate void UserCodeChangedEventHandler(string userCode);

    [Signal]
    public delegate void LoginStartedEventHandler();
    [Signal]
    public delegate void LoginEndedEventHandler();
    [Signal]
    public delegate void LoginSuccessEventHandler();
    [Signal]
    public delegate void LoginFailedEventHandler();

    [Signal]
    public delegate void LoginActiveChangedEventHandler(bool started);
    [Signal]
    public delegate void LoginResultChangedEventHandler(bool success);

    int codeExpiresAt = -99;
    bool CodeExpired => codeExpiresAt <= (Time.GetTicksMsec() * 0.001) - 10;
    bool gettingClient = false;
    bool started = false;

    public async void GenerateCode(bool force = false)
    {
        if (gettingClient)
            return;
        if (started)
        {
            if (!force)
                return;
            started = false;
            EmitSignal(SignalName.LoginEnded);
            EmitSignal(SignalName.LoginActiveChanged, false);
            EmitSignal(SignalName.UserCodeChanged, "");
        }

        gettingClient=true;
        var linkRequest = await GameClient.GetLoginLinkCode();
        gettingClient = false;

        if (linkRequest is not null && linkRequest["errorMessage"] is null)
        {
            cooldown = 0;
            started = true;
            codeExpiresAt = linkRequest["expires_at"].GetValue<int>();

            EmitSignal(SignalName.LoginStarted);
            EmitSignal(SignalName.LoginActiveChanged, true);

            EmitSignal(SignalName.UserCodeChanged, linkRequest["user_code"].ToString());
            return;
        }

        GD.Print(linkRequest?["errorMessage"]);
    }

    float cooldown = 0;
    public override void _Process(double delta)
    {
        if (!started)
            return;

        if (CodeExpired)
        {
            started = false;
            
            EmitSignal(SignalName.LoginFailed);
            EmitSignal(SignalName.LoginResultChanged, false);

            EmitSignal(SignalName.LoginEnded);
            EmitSignal(SignalName.LoginActiveChanged, false);

            EmitSignal(SignalName.UserCodeChanged, "");
            return;
        }

        cooldown -= (float)delta;
        if (cooldown < 0)
        {
            cooldown = 11;
            CheckForCode();
        }
    }

    async void CheckForCode()
    {
        var linkCheckRequest = await GameClient.CheckLoginLinkCode();

        if (linkCheckRequest is not null && linkCheckRequest["errorMessage"] is null)
        {
            started = false;

            EmitSignal(SignalName.LoginSuccess);
            EmitSignal(SignalName.LoginResultChanged, true);

            EmitSignal(SignalName.LoginEnded);
            EmitSignal(SignalName.LoginActiveChanged, false);

            EmitSignal(SignalName.UserCodeChanged, "");
            GameAccount.LoginToAccount(linkCheckRequest.AsObject());
            return;
        }

        if (linkCheckRequest["errorCode"].ToString() != "errors.com.epicgames.account.oauth.authorization_pending")
            GD.Print(linkCheckRequest);
    }
}
