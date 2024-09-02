using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
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
    protected CheckButton usePersistantLogin;

    protected static bool hasAutoLoggedIn = false;

    // Called when the node enters the scene tree for the first time.
    public override async void _Ready()
    {
        await ReadyTask();
    }

    protected virtual async Task ReadyTask()
    {
        ConnectButtons();
        await this.WaitForFrame();
        LoadingOverlay.Instance.AddLoadingKey("loginPreload");
        BanjoAssets.PreloadSourcesParalell();
        await LoginRequests.TryLogin();
        LoadingOverlay.Instance.RemoveLoadingKey("loginPreload");
    }

    protected void ConnectButtons()
    {
        if (generateCodeButton is not null)
            generateCodeButton.Pressed += RequestLoginCode;
        if (pasteButton is not null)
            pasteButton.Pressed += PasteLoginCode;
        if (loginButton is not null)
            loginButton.Pressed += () => Login().RunSafely();
        if (usePersistantLogin is not null && LoginRequests.HasDeviceDetails)
            usePersistantLogin.ButtonPressed = true;
    }

    /* WIP system to detect when an endurance run ends and auto-close the game, intended for automated AFKing
    public const string enduranceCardPackPrefix = "CardPack:ZCP_Endurance_T0";
    protected bool enduranceTimerRunning;
    protected virtual bool closeGameOnEnduranceCompletion => false;
    async void CheckForEnduranceCompletion()
    {
        if (enduranceTimerRunning)
            return;
        enduranceTimerRunning = true;
        while (closeGameOnEnduranceCompletion)
        {
            await Task.Delay(1000*60*5);
            if (!closeGameOnEnduranceCompletion)
                break;
            //force refresh account items profile
            ProfileRequests.InvalidateProfileCache(FnProfiles.AccountItems);
            var matchingCardpacks = await ProfileRequests.GetSumOfProfileItems(FnProfiles.AccountItems, enduranceCardPackPrefix);
            
            if (matchingCardpacks>0)
            {
                var processes = Process.GetProcessesByName("FortniteClient-Win64-Shipping");
                GD.Print("PROCESSES: \n" + processes.Select(val => $"{val.ProcessName} | {val.Id}").ToArray().Join("\n"));
                if (processes.Length > 0)
                {
                    processes[0].CloseMainWindow();
                }
                break;
            }
        }
        enduranceTimerRunning = false;
    }
    */

	public void RequestLoginCode()
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = $"https://www.epicgames.com/id/api/redirect?clientId={LoginRequests.ClientID}&responseType=code",
            UseShellExecute = true,
        });
    }

    public void PasteLoginCode()
    {
        if (oneTimeCodeLine is not null)
            oneTimeCodeLine.Text = DisplayServer.ClipboardGet();
    }

    public virtual async Task Login()
    {
        if (!await LoginRequests.TryLogin(false))
        {
            if (LoginRequests.HasDeviceDetails)
            {
                EmitSignal(SignalName.ErrorContextChanged, LoginRequests.LatestErrorMessage);
                EmitSignal(SignalName.ShowErrorPanel);
                if (!usePersistantLogin.ButtonPressed)
                    LoginRequests.DeleteDeviceDetails();
                return;
            }
            if(!await LoginRequests.LoginWithOneTimeCode(oneTimeCodeLine.Text.Trim()))
            {
                EmitSignal(SignalName.ErrorContextChanged, LoginRequests.LatestErrorMessage);
                EmitSignal(SignalName.ShowErrorPanel);
                return;
            }
        }
        if (usePersistantLogin is null)
            return;
        if (!LoginRequests.HasDeviceDetails)
        {
            if(usePersistantLogin.ButtonPressed)
                await LoginRequests.SetupDeviceAuth();
        }
        else if (!usePersistantLogin.ButtonPressed)
        {
            LoginRequests.DeleteDeviceDetails();
        }
    }

	public async Task ActivateDeviceAuth()
    {
		if (!LoginRequests.HasDeviceDetails)
            await LoginRequests.SetupDeviceAuth();
        await LoginRequests.TryLogin();
    }

    public void ClearDeviceAuth()
    {
		LoginRequests.DeleteDeviceDetails();
    }
}
