// SPDX-License-Identifier: MIT
namespace PCLCS.Open;

public static class OpenEndpoints {
    public static bool IsLegacyClient(string clientId) => Regex.IsMatch(clientId, "^[0-9a-fA-F]{16}$");
    public static string DeviceEndpoint(string clientId) => IsLegacyClient(clientId) ? "https://login.live.com/oauth20_connect.srf" : "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    public static string TokenEndpoint(string clientId) => IsLegacyClient(clientId) ? "https://login.live.com/oauth20_token.srf" : "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    public static string Scope(string clientId) => IsLegacyClient(clientId) ? "service::user.auth.xboxlive.com::MBI_SSL" : "XboxLive.signin offline_access";
    public static string XboxRpsTicket(string clientId, string accessToken) =>
        IsLegacyClient(clientId) || accessToken.StartsWith("d=", StringComparison.Ordinal) ? accessToken : "d=" + accessToken;
    public static string DeviceParameters(string clientId) => "client_id=" + Uri.EscapeDataString(clientId) + "&scope=" + Uri.EscapeDataString(Scope(clientId)) + (IsLegacyClient(clientId) ? "&response_type=device_code" : "&tenant=/consumers");
    public static string CurseForgeAddress(string address) {
        var uri = new Uri(address);
        return uri.Host.Equals("api.curseforge.com", StringComparison.OrdinalIgnoreCase) && (OpenPreferences.CurseForgeMirror || string.IsNullOrWhiteSpace(OpenPreferences.CurseForgeKey))
            ? "https://mod.mcimirror.top/curseforge" + uri.PathAndQuery : address;
    }
}
