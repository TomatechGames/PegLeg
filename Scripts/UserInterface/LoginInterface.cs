using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class LoginInterface : Control
{
    [Signal]
    public delegate void ShowErrorPanelEventHandler();
    [Signal]
    public delegate void ErrorHeaderChangedEventHandler(string header);
    [Signal]
    public delegate void ErrorContextChangedEventHandler(string context);

    [Export]
    protected Button generateCodeButton;
	[Export]
    protected Button pasteButton;
    [Export]
    protected LineEdit oneTimeCodeLine;
    [Export]
    protected Button loginButton;
    [Export]
    protected Button usePersistantLogin;

    protected static bool hasAutoLoggedIn = false;

    // Called when the node enters the scene tree for the first time.
    public override async void _Ready()
    {
        await ReadyTask();
    }

    protected virtual async Task ReadyTask()
    {
        ConnectButtons();
        await Helpers.WaitForFrame();
        LoadingOverlay.AddLoadingKey("loginPreload");
        BanjoAssets.ReadAllSources();
        LoadingOverlay.RemoveLoadingKey("loginPreload");
    }

    protected void ConnectButtons()
    {
        if (generateCodeButton is not null)
            generateCodeButton.Pressed += RequestLoginCode;
        if (pasteButton is not null)
            pasteButton.Pressed += PasteLoginCode;
        if (loginButton is not null)
            loginButton.Pressed += async () =>
            {
                await Login();
            };
        if (usePersistantLogin is not null)
            usePersistantLogin.ButtonPressed = true;
    }

	public void RequestLoginCode()
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = $"https://www.epicgames.com/id/api/redirect?clientId={GameClient.ClientID}&responseType=code",
            UseShellExecute = true,
        });
    }

    public void PasteLoginCode()
    {
        if (oneTimeCodeLine is not null)
            oneTimeCodeLine.Text = DisplayServer.ClipboardGet();
    }

    protected void OnLoginSucceeded()
    {

    }

    public virtual async Task Login()
    {

    }

	public async Task ActivateDeviceAuth()
    {

    }

    public void ClearDeviceAuth()
    {

    }
}
