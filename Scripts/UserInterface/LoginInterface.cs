using Godot;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class LoginInterface : VBoxContainer
{
	[Export] NodePath loginButtonPath;
	Button loginButton;

	[Export] NodePath pasteButtonPath;
	Button pasteButton;

	[Export] NodePath codeLabelPath;
	TextEdit codeLabel;

	[Export] NodePath deviceAuthButtonPath;
	Button deviceAuthButton;

    [Export] NodePath clearDeviceAuthButtonPath;
    Button clearDeviceAuthButton;

    [Export]
    bool autoRefreshSAC = false;

    [Export] NodePath supportACreatorLinePath;
    LineEdit supportACreatorLine;

    [Export]
    bool closeGameOnEnduranceCompletion;

    [Export] string deviceDetailsFilePath = "user://settings.json";


	//static string clientAccessToken;
	//static JsonNode clientCredResponse;
	//public static string ClientToken =>clientAccessToken;



	// Called when the node enters the scene tree for the first time.
	public override async void _Ready()
    {
        codeLabel = GetNode<TextEdit>(codeLabelPath);

        GetNode<Button>(loginButtonPath).Pressed += OnRequestLoginCode;
		GetNode<Button>(pasteButtonPath).Pressed += OnPasteLoginCode;
		GetNode<Button>(deviceAuthButtonPath).Pressed += OnActivateDeviceAuth;
        GetNode<Button>(clearDeviceAuthButtonPath).Pressed += OnClearDeviceAuth;

		this.GetNodeOrNull(supportACreatorLinePath, out supportACreatorLine);

        await this.WaitForFrame();
        LoadingOverlay.Instance.AddLoadingKey("loginPreload");
        BanjoAssets.PreloadSourcesParalell();
        await LoginRequests.LoginWithDeviceDetails();
        await ProfileRequests.GetProfile(FnProfiles.AccountItems);
        LoadingOverlay.Instance.RemoveLoadingKey("loginPreload");

        supportACreatorLine.Text = await ProfileRequests.GetSACCode();
        if (autoRefreshSAC && await ProfileRequests.IsSACExpired())
            SetCreatorCode();
        GD.Print("Item Count: "+await ProfileRequests.GetProfileItemsCount(FnProfiles.AccountItems));
    }


    const string enduranceCardPackPrefix = "CardPack:ZCP_Endurance_T0";
    bool enduranceTimerRunning;
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

	void OnRequestLoginCode()
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = $"https://www.epicgames.com/id/api/redirect?clientId={LoginRequests.ClientID}&responseType=code",
            UseShellExecute = true,
        });
    }

	async void OnPasteLoginCode()
    {
		await LoginRequests.LoginWithOneTimeCode(DisplayServer.ClipboardGet());
    }

	async void OnActivateDeviceAuth()
    {
		if (!LoginRequests.HasDeviceDetails)
            await LoginRequests.SetupDeviceAuth();
        await LoginRequests.LoginWithDeviceDetails();
    }

    private void OnClearDeviceAuth()
    {
		LoginRequests.DeleteDeviceDetails();
    }

    void UpdateLabel()
	{
		codeLabel.Text = LoginRequests.B64AuthString + "\n" + LoginRequests.AccountAuthHeader?.ToString();
	}

    public async void SetCreatorCode()
    {
        await ProfileRequests.SetSACCode(supportACreatorLine.Text.Replace(" (Expired)", ""));
        supportACreatorLine.Text = await ProfileRequests.GetSACCode();
    }
}
