using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

static class LoginRequests
{
    const string deviceDetailsFilePath = "user://fishFingers.json";
    static string ClientAuthHeaderFromKeys(string clientID, string clientSecret) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientID}:{clientSecret}"));

    const string fortPCClientId = "ec684b8c687f479fadea3cb2ad83f5c6";
    const string fortPCSecret = "e1f31c211f28413186262d37a13fc84d";
    const string fortIOSClientId = "3446cd72694c4a4485d81b77adbb2141";
    const string fortIOSSecret = "9209d4a5e25a457fb9b07489d313b41a";

    static readonly string fortPCAuthString = ClientAuthHeaderFromKeys(fortPCClientId, fortPCSecret);
    static readonly string fortIOSAuthString = ClientAuthHeaderFromKeys(fortIOSClientId, fortIOSSecret);

    public static string ClientID => fortIOSClientId;
    public static string B64AuthString => fortIOSAuthString;

    static readonly AuthenticationHeaderValue clientHeader = new("Basic", B64AuthString);

    static AuthenticationHeaderValue accountAuthHeader;
    public static AuthenticationHeaderValue AccountAuthHeader => accountAuthHeader;
    static int authExpiresAt = -999999;
    public static bool AuthTokenValid => authExpiresAt > Time.GetTicksMsec() * 0.001;
    static int refreshExpiresAt = -999999;
    static bool RefreshTokenValid => refreshExpiresAt > Time.GetTicksMsec() * 0.001;

    static readonly AesContext deviceDetailEncryptor = new();

    static JsonObject accountAccessObj;
    static JsonObject deviceDetails;
    static string refreshToken;
    public static bool HasDeviceDetails => LoadDeviceDetails() is not null;
    public static string AccountID => accountAccessObj?["account_id"]?.ToString();
    public static string AccessToken = accountAccessObj?["access_token"]?.ToString();

    public static bool IsOffline { get; private set; }
    public static string LatestErrorMessage { get; private set; }

    public static void DebugClearToken()
    {
        accountAccessObj = null;
        authExpiresAt = 0;
    }
    public static void DebugClearRefresh()
    {
        refreshToken = null;
        refreshExpiresAt = 0;
    }

    public static async void TryLoginWithAlert()
    {
        await GenericConfirmationWindow.OpenConfirmation("Login Error", "Retry");
    }

    public static async Task<bool> LoginWithOneTimeCode(string oneTimeCode)
    {
        if(string.IsNullOrWhiteSpace(oneTimeCode))
            return false;

        await loginSephamore.WaitAsync();
        try
        {
            GD.Print("authorising via one-time code...");
            JsonNode accountAuth = await Helpers.MakeRequest(
                HttpMethod.Post,
                FNEndpoints.loginEndpoint,
                "account/api/oauth/token",
                $"grant_type=authorization_code&code={oneTimeCode}",
                clientHeader
            );

            if (accountAuth is null)
            {
                GD.Print("OFFLINE");
                LatestErrorMessage = "No Response";
                IsOffline = true;
                return false;
            }

            if (accountAuth["errorMessage"] is JsonValue errorMessage)
            {
                LatestErrorMessage = errorMessage.ToString();
                GD.Print("Error: " + LatestErrorMessage);
                return false;
            }

            OnRecieveAccountAuth(accountAuth);
            return true;
        }
        finally
        {
            loginSephamore.Release();
        }
    }

    public static async Task SetupDeviceAuth()
    {
        if (HasDeviceDetails)
            return;
        GD.Print($"Setting up device auth...");
        //needed for Account Id
        if (accountAccessObj is null)
        {
            GD.Print($"No access token has been provided");
            return;
        }

        //request device details
        JsonObject returnedDetails =
        (
            await Helpers.MakeRequest(
                HttpMethod.Post,
                FNEndpoints.loginEndpoint,
                $"account/api/public/account/{AccountID}/deviceAuth",
                "",
                AccountAuthHeader,
                ""
            )
        )?.AsObject();

        if(returnedDetails is null)
        {
            IsOffline = true;
            LatestErrorMessage = "No Response";
            GD.Print("OFFLINE");
            return;
        }

        if (returnedDetails["errorMessage"] is JsonValue errorMessage)
        {
            LatestErrorMessage = errorMessage.ToString();
            GD.Print("Error: " + LatestErrorMessage);
            return;
        }

        if (!DeviceDetailsSchemaValid(returnedDetails))
        {
            GD.Print("invalid devide auth recieved:\n" + returnedDetails?.ToString());
            return;
        }

        GD.Print("Device auth retrieved");
        deviceDetails = returnedDetails;
        //return on invalid response
        SaveDeviceDetails();
    }

    static void SaveDeviceDetails()
    {
        if (deviceDetails is null)
            return;

        GD.Print($"Saving device auth");

        //stringify and add padding
        string deviceDetalsString = deviceDetails.ToString();
        int remainder = deviceDetalsString.Length % 16;

        for (int i = 0; i < 16 - remainder; i++)
        {
            deviceDetalsString += "^";
        }

        string deviceDetailKey = System.Environment.MachineName;
        while (deviceDetailKey.Length < 32)
            deviceDetailKey += "#";
        deviceDetailKey = deviceDetailKey[..32];

        //encrypt
        deviceDetailEncryptor.Start(AesContext.Mode.EcbEncrypt, deviceDetailKey.ToUtf8Buffer());
        byte[] encryptedDetails = deviceDetailEncryptor.Update(deviceDetalsString.ToUtf8Buffer());
        deviceDetailEncryptor.Finish();

        //save bytes
        string fullPath = ProjectSettings.GlobalizePath(deviceDetailsFilePath);
        using FileStream fs = File.Create(fullPath);
        fs.Write(encryptedDetails, 0, encryptedDetails.Length);
        fs.Flush();
        GD.Print("device auth encrypted and saved");
    }

    static JsonObject LoadDeviceDetails()
    {
        if(deviceDetails is not null)
            return deviceDetails;
        string fullPath = ProjectSettings.GlobalizePath(deviceDetailsFilePath);
        if (!File.Exists(fullPath))
        {
            GD.Print($"Failed to load device auth");
            return null;
        }

        byte[] encryptedDetails = null;
        using (FileStream fs = File.OpenRead(fullPath))
        {
            //return if byte length isnt a multiple of 16
            if (fs.Length % 16 != 0)
                return null;
            encryptedDetails = new byte[fs.Length];
            fs.Read(encryptedDetails, 0, encryptedDetails.Length);
        }

        string deviceDetailKey = System.Environment.MachineName;
        while (deviceDetailKey.Length < 32)
            deviceDetailKey += "#";
        deviceDetailKey = deviceDetailKey[..32];

        //decrypt
        deviceDetailEncryptor.Start(AesContext.Mode.EcbDecrypt, deviceDetailKey.ToUtf8Buffer());
        byte[] decryptedDetails = deviceDetailEncryptor.Update(encryptedDetails);
        deviceDetailEncryptor.Finish();
        string deviceDetalsString = Encoding.UTF8.GetString(decryptedDetails, 0, decryptedDetails.Length);

        //remove padding and convert to json
        while (deviceDetalsString.EndsWith('^'))
        {
            deviceDetalsString = deviceDetalsString[..^1];
        }

        JsonObject resultDetails = null;
        try
        {
            resultDetails = JsonNode.Parse(deviceDetalsString).AsObject();
        }
        catch (Exception)
        {

        }

        //return on invalid data
        if (!DeviceDetailsSchemaValid(resultDetails))
        {
            GD.Print("invalid devide auth found");
            return null;
        }
        deviceDetails = resultDetails;
        GD.Print("device auth loaded");
        return deviceDetails;
    }

    public static void DeleteDeviceDetails()
    {
        string fullPath = ProjectSettings.GlobalizePath(deviceDetailsFilePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            deviceDetails = null;
            GD.Print("device auth cleared");
        }
    }

    static SemaphoreSlim loginSephamore = new(1);
    public static async Task<bool> TryLogin(bool withAlert = true)
    {
        await loginSephamore.WaitAsync();
        try
        {
            if (AuthTokenValid)
                return true;

            if (RefreshTokenValid)
            {
                GD.Print("attempting to refresh token");
                JsonNode refresh = await Helpers.MakeRequest(
                    HttpMethod.Post,
                    FNEndpoints.loginEndpoint,
                    "account/api/oauth/token",
                    $"grant_type=refresh_token&" +
                    $"refresh_token={refreshToken}&" +
                    $"token_type=eg1",
                    clientHeader
                );

                if(refresh is null)
                {
                    IsOffline = true;
                    if (withAlert)
                        ShowLoginFailAlert("Couldn't connect to the internet");
                    return false;
                }

                if (refresh["errorMessage"] is null)
                {
                    OnRecieveAccountAuth(refresh);
                    return true;
                }
            }

            if (!HasDeviceDetails)
            {
                GD.Print("no tokens to login with");
                if (withAlert)
                    ShowLoginFailAlert("Your Authentication Code has expired, please generate a new one");
                return false;
            }

            GD.Print("authorising via device auth...");

            JsonNode accountAuth = await Helpers.MakeRequest(
                HttpMethod.Post,
                FNEndpoints.loginEndpoint,
                "account/api/oauth/token",
                $"grant_type=device_auth&" +
                $"account_id={deviceDetails["accountId"]}&" +
                $"device_id={deviceDetails["deviceId"]}&" +
                $"secret={deviceDetails["secret"]}",
                clientHeader
            );
            
            if (accountAuth is null)
            {
                IsOffline = true;
                GD.Print("OFFLINE");
                if (withAlert)
                    ShowLoginFailAlert("Couldn't connect to the internet");
                return false;
            }

            if (accountAuth["errorMessage"] is JsonValue errorMessage)
            {
                LatestErrorMessage = errorMessage.ToString();
                GD.Print("Error: " + LatestErrorMessage);
                if (withAlert)
                    ShowLoginFailAlert();
                return AuthTokenValid;
            }

            OnRecieveAccountAuth(accountAuth);
            return true;
        }
        finally
        {
            loginSephamore.Release();
        }
    }

    public static event Action OnLoginFailAlertPressed;
    static async void ShowLoginFailAlert(string message = null)
    {
        message ??= LatestErrorMessage;
        await Task.Delay(100);
        await GenericConfirmationWindow.OpenConfirmation("Login Error", "Return to Login Screen", contextText: message, allowCancel: false);
        OnLoginFailAlertPressed?.Invoke();
    }


    static void OnRecieveAccountAuth(JsonNode accountAuth)
    {
        JsonObject accountAuthObj = accountAuth.AsObject();
        if (AccountAuthSchemaValid(accountAuthObj))
        {
            accountAccessObj = accountAuthObj;
            accountAuthHeader = new("Bearer", accountAccessObj["access_token"].ToString());
            authExpiresAt = Mathf.FloorToInt(Time.GetTicksMsec() * 0.001) + accountAccessObj["expires_in"].GetValue<int>() - 10;
            if (accountAccessObj["refresh_expires"]?.GetValue<int>() is int refreshExpires)
            {
                refreshExpiresAt = Mathf.FloorToInt(Time.GetTicksMsec() * 0.001) + refreshExpires - 10;
                refreshToken = accountAccessObj["refresh_token"].ToString();
            }
            GD.Print("authentication successful");
        }
    }

    static bool AccountAuthSchemaValid(JsonObject accountAuth) =>
        accountAuth is not null &&
        accountAuth.ContainsKey("access_token") &&
        accountAuth.ContainsKey("account_id") &&
        accountAuth.ContainsKey("expires_in");

    static bool DeviceDetailsSchemaValid(JsonObject deviceDetails) =>
        deviceDetails is not null &&
        deviceDetails.ContainsKey("deviceId") &&
        deviceDetails.ContainsKey("accountId") &&
        deviceDetails.ContainsKey("secret");
}
