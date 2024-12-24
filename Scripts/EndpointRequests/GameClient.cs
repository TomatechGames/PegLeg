using Godot;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

static class GameClient
{
    static string ClientAuthHeaderFromKeys(string clientID, string clientSecret) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientID}:{clientSecret}"));

    const string fortPCClientId = "ec684b8c687f479fadea3cb2ad83f5c6";
    const string fortPCSecret = "e1f31c211f28413186262d37a13fc84d";
    static readonly string fortPCAuthString = ClientAuthHeaderFromKeys(fortPCClientId, fortPCSecret);

    const string fortIOSClientId = "3446cd72694c4a4485d81b77adbb2141";
    const string fortIOSSecret = "9209d4a5e25a457fb9b07489d313b41a";
    static readonly string fortIOSAuthString = ClientAuthHeaderFromKeys(fortIOSClientId, fortIOSSecret);

    const string fortAndroidClientId = "3f69e56c7649492c8cc29f1af08a8a12";
    const string fortAndroidSecret = "b51ee9cb12234f50a69efa67ef53812e";
    static readonly string fortAndroidAuthString = ClientAuthHeaderFromKeys(fortAndroidClientId, fortAndroidSecret);

    const string fortNewSwitchClientId = "98f7e42c2e3a4f86a74eb43fbb41ed39";
    const string fortNewSwitchSecret = "0a2449a2-001a-451e-afec-3e812901c4d7";
    static readonly string fortNewSwitchAuthString = ClientAuthHeaderFromKeys(fortAndroidClientId, fortAndroidSecret);

    public static string ClientID => fortNewSwitchClientId;
    public static string B64AuthString => fortNewSwitchAuthString;
    public static readonly AuthenticationHeaderValue clientHeader = new("Basic", B64AuthString);

    static AuthenticationHeaderValue clientTokenHeader;
    static int clientExpiresAt = -999;
    static bool ClientTokenExpired => clientExpiresAt <= (Time.GetTicksMsec() * 0.001) - 600;

    public static async Task<AuthenticationHeaderValue> GetClientTokenHeader()
    {
        if (!ClientTokenExpired)
            return clientTokenHeader;

        var tokenRequest = await Helpers.MakeRequest(
            HttpMethod.Post,
            FnEndpoints.loginEndpoint,
            "/account/api/oauth/token",
            "",
            clientHeader
        );

        if (tokenRequest is not null && tokenRequest["errorMessage"] is null)
        {
            clientExpiresAt = Mathf.FloorToInt(Time.GetTicksMsec() * 0.001) + tokenRequest["expires_in"].GetValue<int>();
            return clientTokenHeader = new("Bearer", tokenRequest["access_token"].ToString());
        }

        //show error

        return null;
    }

    public static async Task<JsonObject> LoginWithOneTimeCode(string oneTimeCode)
    {
        if(string.IsNullOrWhiteSpace(oneTimeCode))
            return null;
        return (await Helpers.MakeRequest(
            HttpMethod.Post,
            FnEndpoints.loginEndpoint,
            "account/api/oauth/token",
            $"grant_type=authorization_code&code={oneTimeCode}",
            clientHeader
        )).AsObject();
    }

    public static async Task<JsonObject> LoginWithRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;
        return (await Helpers.MakeRequest(
            HttpMethod.Post,
            FnEndpoints.loginEndpoint,
            "account/api/oauth/token",
            $"grant_type=refresh_token&" +
            $"refresh_token={refreshToken}&" +
            $"token_type=eg1",
            clientHeader
        )).AsObject();
    }

    public static async Task<JsonObject> LoginWithDeviceAuth(string accountId, string deviceId, string deviceSecret)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(deviceSecret))
            return null;
        return (await Helpers.MakeRequest(
            HttpMethod.Post,
            FnEndpoints.loginEndpoint,
            "account/api/oauth/token",
            $"grant_type=device_auth&" +
            $"account_id={accountId}&" +
            $"device_id={deviceId}&" +
            $"secret={deviceSecret}",
            clientHeader
        )).AsObject();
    }

    static JsonObject activeLinkCode;
    static int linkCodeExpiresAt = -999;
    static string deviceCode;
    static bool LinkCodeHalfExpired => linkCodeExpiresAt <= (Time.GetTicksMsec() * 0.001) - 300;
    static bool LinkCodeExpired => linkCodeExpiresAt <= (Time.GetTicksMsec() * 0.001) - 10;
    public static async Task<JsonObject> GetLoginLinkCode(bool force = false)
    {
        if (!LinkCodeHalfExpired && !force)
            return activeLinkCode;
        if(await GetClientTokenHeader() is not AuthenticationHeaderValue clientTokenHeader)
            return null;

        var linkGetResult = await Helpers.MakeRequest(
            HttpMethod.Post,
            FnEndpoints.loginEndpoint,
            "/account/api/oauth/token",
            "",
            clientTokenHeader
        );

        if(linkGetResult is not null && linkGetResult["errorMessage"] is null)
        {
            activeLinkCode = linkGetResult.AsObject();
            linkCodeExpiresAt = Mathf.FloorToInt(Time.GetTicksMsec() * 0.001) + activeLinkCode["expires_in"].GetValue<int>();
            activeLinkCode["expires_at"] = linkCodeExpiresAt;
            deviceCode = activeLinkCode["device_code"].ToString();
        }

        return linkGetResult?.AsObject();
    }

    public static async Task<JsonObject> CheckLoginLinkCode()
    {
        if (LinkCodeExpired)
            return null;
        var linkCheckResult = await Helpers.MakeRequest(
            HttpMethod.Post,
            FnEndpoints.loginEndpoint,
            "/account/api/oauth/token",
            $"grant_type=device_code&" +
            $"secret={deviceCode}",
            clientHeader
        );
        if (linkCheckResult is not null && linkCheckResult["errorMessage"] is null)
            linkCodeExpiresAt = -99;
        return linkCheckResult?.AsObject();
    }
}
