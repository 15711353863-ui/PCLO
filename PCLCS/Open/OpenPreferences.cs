// SPDX-License-Identifier: MIT
using System.IO;
using System.Security.Cryptography;

namespace PCLCS.Open;

/// <summary>User preferences and credentials are deliberately separate from the source tree.</summary>
public static class OpenPreferences {
    private static readonly object Gate = new();
    public static string DirectoryPath => Environment.GetEnvironmentVariable("PCL_OPEN_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Plain Craft Launcher Open");
    private static string SettingsPath => Path.Combine(DirectoryPath, "preferences.json");
    public static string MutexName {
        get {
            if (Environment.GetEnvironmentVariable("PCL_OPEN_DATA_DIR") == null) return "PlainCraftLauncherOpen_SingletonMutex";
            using var hash = SHA256.Create();
            return "PlainCraftLauncherOpen_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(DirectoryPath).ToUpperInvariant()))).Replace("-", "");
        }
    }
    public static string Theme { get; private set; } = "system";
    public const string DefaultMicrosoftClientId = "00000000402b5328";
    public static string Language { get; private set; } = "system";
    public static string LightScheme { get; private set; } = "blue";
    public static string DarkScheme { get; private set; } = "blue";
    public static bool CurseForgeMirror { get; private set; } = true;
    public static string ClientId { get; private set; } = DefaultMicrosoftClientId;
    public static string CurseForgeKey { get; private set; } = "";
    public static string CredentialError { get; private set; } = "";
    static OpenPreferences() { Reload(); }
    public static void Reload() {
        lock (Gate) {
            CredentialError = "";
            if (File.Exists(SettingsPath)) {
                try {
                    var json = JObject.Parse(File.ReadAllText(SettingsPath));
                    Theme = (string?)json["theme"] ?? "system";
                    Language = (string?)json["language"] ?? "system";
                    if (((int?)json["schema"] ?? 1) < 2 && Language == "zh-CN") Language = "system";
                    LightScheme = (string?)json["lightScheme"] ?? "blue";
                    DarkScheme = (string?)json["darkScheme"] ?? "blue";
                    CurseForgeMirror = (bool?)json["curseForgeMirror"] ?? true;
                } catch { /* A broken preference must never prevent startup. */ }
            }
            ClientId = ""; CurseForgeKey = "";
            var path = Path.Combine(DirectoryPath, "credentials.dpapi");
            if (File.Exists(path)) {
                try {
                    var json = JObject.Parse(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)));
                    ClientId = (string?)json["microsoftClientId"] ?? "";
                    CurseForgeKey = (string?)json["curseForgeKey"] ?? "";
                } catch { CredentialError = "本地凭据无法解密，请重新填写并保存。"; }
            }
            ClientId = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID") ?? ClientId;
            if (string.IsNullOrWhiteSpace(ClientId)) ClientId = DefaultMicrosoftClientId;
            CurseForgeKey = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY") ?? CurseForgeKey;
        }
    }
    public static void Save(string theme, string language, string clientId, string curseForgeKey) {
        if (theme != "system" && theme != "light" && theme != "dark") throw new ArgumentException("Unknown theme");
        if (language != "zh-CN" && language != "zh-TW" && language != "en-US" && language != "system") throw new ArgumentException("Unknown language");
        clientId = clientId.Trim(); curseForgeKey = curseForgeKey.Trim();
        if (clientId.Length > 0 && !Guid.TryParse(clientId, out _) && !Regex.IsMatch(clientId, "^[0-9a-fA-F]{16}$")) throw new ArgumentException("微软 Client ID 应为 GUID 或 16 位 Live 应用 ID。");
        lock (Gate) {
            Directory.CreateDirectory(DirectoryPath);
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(new JObject {
                ["microsoftClientId"] = clientId, ["curseForgeKey"] = curseForgeKey
            }.ToString()), null, DataProtectionScope.CurrentUser);
            AtomicWrite(Path.Combine(DirectoryPath, "credentials.dpapi"), encrypted);
            WritePreferences(theme, language, LightScheme, DarkScheme, CurseForgeMirror);
            Reload();
        }
    }
    public static void SaveAppearance(string theme, string language, string lightScheme, string darkScheme) {
        if (!new[] { "system", "light", "dark" }.Contains(theme) || !new[] { "system", "zh-CN", "zh-TW", "en-US" }.Contains(language)) throw new ArgumentException("Unknown appearance setting");
        if (!new[] { "blue", "mist", "ocean" }.Contains(lightScheme) || !new[] { "blue", "mist", "ocean" }.Contains(darkScheme)) throw new ArgumentException("Unknown blue color scheme");
        lock (Gate) { WritePreferences(theme, language, lightScheme, darkScheme, CurseForgeMirror); Reload(); }
    }
    public static void SaveCommunityMode(bool mirror) {
        lock (Gate) { WritePreferences(Theme, Language, LightScheme, DarkScheme, mirror); Reload(); }
    }
    private static void WritePreferences(string theme, string language, string lightScheme, string darkScheme, bool mirror) => AtomicWrite(SettingsPath, Encoding.UTF8.GetBytes(new JObject {
        ["schema"] = 2, ["theme"] = theme, ["language"] = language, ["lightScheme"] = lightScheme, ["darkScheme"] = darkScheme, ["curseForgeMirror"] = mirror
    }.ToString()));
    public static string ResolveLanguage(string region, string culture) => region.ToUpperInvariant() switch {
        "CN" => "zh-CN", "TW" or "HK" or "MO" => "zh-TW",
        "SG" or "MY" when culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) => "zh-CN",
        _ => "en-US"
    };
    public static string AutoLanguage {
        get {
            var region = System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName;
            try {
                var configured = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\International\Geo", "Name", null) as string;
                if (configured != null && Regex.IsMatch(configured, "^[A-Za-z]{2}$")) region = configured;
            } catch (System.Security.SecurityException) { }
            return ResolveLanguage(region, System.Globalization.CultureInfo.CurrentUICulture.Name);
        }
    }
    internal static void AtomicWrite(string path, byte[] bytes) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

/// <summary>Persist the original project snapshot, keyed by platform and immutable project id.</summary>
public static class Favorites {
    private static readonly object Gate = new();
    private static string FilePath => Path.Combine(OpenPreferences.DirectoryPath, "favorites.json");
    public static JArray Read() {
        lock (Gate) {
            if (!File.Exists(FilePath)) return new JArray();
            return JArray.Parse(File.ReadAllText(FilePath));
        }
    }
    public static bool Contains(int platform, string id) {
        try { return Read().OfType<JObject>().Any(x => (int?)x["Platform"] == platform && (string?)x["Id"] == id); }
        catch (JsonException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    public static bool Toggle(JObject project) {
        lock (Gate) {
            var entries = Read();
            var existing = entries.OfType<JObject>().FirstOrDefault(x => (int?)x["Platform"] == (int?)project["Platform"] && (string?)x["Id"] == (string?)project["Id"]);
            if (existing != null) entries.Remove(existing); else entries.Insert(0, project.DeepClone());
            OpenPreferences.AtomicWrite(FilePath, Encoding.UTF8.GetBytes(entries.ToString()));
            return existing == null;
        }
    }
}
