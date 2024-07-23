using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
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

    public static string fortPCAuthString = ClientAuthHeaderFromKeys(fortPCClientId, fortPCSecret);
    public static string fortIOSAuthString = ClientAuthHeaderFromKeys(fortIOSClientId, fortIOSSecret);

    public static string ClientID => fortIOSClientId;
    public static string B64AuthString => fortIOSAuthString;


    static AuthenticationHeaderValue clientHeader = new("Basic", B64AuthString);

    static AuthenticationHeaderValue accountAuthHeader;
    public static AuthenticationHeaderValue AccountAuthHeader => accountAuthHeader;
    static int authExpiresAt = -999999;
    public static bool AuthTokenValid => authExpiresAt > Time.GetTicksMsec() * 0.001;

    static readonly AesContext deviceDetailEncryptor = new();

    static JsonObject accountAccessToken;
    static JsonObject deviceDetails;
    public static bool HasDeviceDetails => LoadDeviceDetails() is not null;
    public static string AccountID => accountAccessToken["account_id"].ToString();
    public static string AccessToken = accountAccessToken?["access_token"]?.ToString();

    static bool loginInProgress;

    public static async Task<bool> LoginWithOneTimeCode(string oneTimeCode)
    {
        if(string.IsNullOrWhiteSpace(oneTimeCode))
            return AuthTokenValid;

        if (await InterruptLogin())
            return AuthTokenValid;
        try
        {
            loginInProgress = true;

            GD.Print("authorising via one-time code...");
            JsonNode accountAuth = await Helpers.MakeRequest(
                HttpMethod.Post,
                FNEndpoints.loginEndpoint,
                "account/api/oauth/token",
                $"grant_type=authorization_code&code={oneTimeCode}",
                clientHeader
            );
            OnRecieveAccountAuth(accountAuth);
        }
        finally
        {
            loginInProgress = false;
        }
        return AuthTokenValid;
    }

    public static async Task SetupDeviceAuth()          
    {
        if (HasDeviceDetails)
            return;
        GD.Print($"Setting up device auth...");
        //needed for Account Id
        if (accountAccessToken is null)
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

        if (!DeviceDetailsSchemaValid(returnedDetails))
        {
            GD.Print("invalid devide auth recieved:\n" + returnedDetails?.ToString());
            return;
        }

        GD.Print($"Device auth retrieved");
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
            GD.Print($"Failed to load device auth: {fullPath}");
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
            GD.Print("invalid devide auth loaded:\n" + deviceDetalsString);
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

    public static async Task<bool> TryLogin()
    {
        //return if already logged in or currently logging in
        if (await InterruptLogin())
            return AuthTokenValid;
        try
        {
            loginInProgress = true;
            if (deviceDetails is null)
                LoadDeviceDetails();

            if (deviceDetails is null)
            {
                GD.Print("something oopsied");
                loginInProgress = false;
                return AuthTokenValid;
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

            OnRecieveAccountAuth(accountAuth);
        }
        finally
        {
            loginInProgress = false;
        }
        return AuthTokenValid;
    }


    static async Task<bool> InterruptLogin()
    {
        int loginIterations = 0;
        while (loginInProgress)
        {
            await Task.Delay(500);
            loginIterations++;
            if (loginIterations > 100)
                return true;
        }
        return AuthTokenValid;
    }


    static void OnRecieveAccountAuth(JsonNode accountAuth)
    {
        JsonObject accountAuthObj = accountAuth.AsObject();
        if (AccountAuthSchemaValid(accountAuthObj))
        {
            accountAccessToken = accountAuthObj;
            GD.Print("tok:"+accountAccessToken["access_token"].ToString());
            accountAuthHeader = new("Bearer", accountAccessToken["access_token"].ToString());
            authExpiresAt = Mathf.FloorToInt(Time.GetTicksMsec() * 0.001) + accountAccessToken["expires_in"].GetValue<int>()-10;
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
