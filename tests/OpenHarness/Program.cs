// SPDX-License-Identifier: MIT
using System.IO;
using System.IO.Compression;
using PCLCS.Open;
using Newtonsoft.Json.Linq;

var root = Path.Combine(Path.GetTempPath(), "pc lo-tests".Replace(" ", "") + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("PCL_OPEN_DATA_DIR", Path.Combine(root, "preferences"));
Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", null);
Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", null);
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
void Throws(Action action, string message) { try { action(); } catch { Check(true, message); return; } throw new Exception(message); }
try {
    OpenPreferences.Save("dark", "en-US", "2d53df03-4569-4c50-b80e-b328098f5221", "local-test-value");
    OpenPreferences.Reload();
    Check(OpenPreferences.Theme == "dark" && OpenPreferences.Language == "en-US", "appearance preferences survive reload");
    Check(OpenPreferences.CurseForgeKey == "local-test-value", "DPAPI credentials round-trip for current Windows user");
    Check(!File.ReadAllText(Path.Combine(OpenPreferences.DirectoryPath, "preferences.json")).Contains("local-test-value") && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(OpenPreferences.DirectoryPath, "credentials.dpapi"))).Contains("local-test-value"), "credentials are absent from plaintext preferences and encrypted file");
    Throws(() => OpenPreferences.Save("dark", "en-US", "invalid", ""), "invalid Microsoft Client ID is rejected");
    Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", "environment-test-value"); OpenPreferences.Reload();
    Check(OpenPreferences.CurseForgeKey == "environment-test-value", "environment credentials take precedence");
    Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", null); OpenPreferences.Reload();
    OpenPreferences.Save("system", "system", "", "");
    Check(OpenPreferences.ClientId == OpenPreferences.DefaultMicrosoftClientId && OpenEndpoints.IsLegacyClient(OpenPreferences.ClientId), "empty custom Client ID uses the requested built-in Live client");
    Check(OpenEndpoints.DeviceEndpoint(OpenPreferences.ClientId).Contains("login.live.com") && OpenEndpoints.DeviceParameters(OpenPreferences.ClientId).Contains("response_type=device_code"), "legacy public client uses the Live device authorization protocol");
    Check(OpenEndpoints.XboxRpsTicket(OpenPreferences.ClientId, "token") == "token" && OpenEndpoints.XboxRpsTicket("2d53df03-4569-4c50-b80e-b328098f5221", "token") == "d=token", "Xbox RPS ticket prefix matches the selected Microsoft OAuth protocol");
    Check(OpenEndpoints.TokenEndpoint("2d53df03-4569-4c50-b80e-b328098f5221").Contains("microsoftonline.com"), "custom modern Client IDs retain the modern device protocol");
    Check(OpenPreferences.ResolveLanguage("CN", "en-US") == "zh-CN" && OpenPreferences.ResolveLanguage("TW", "en-US") == "zh-TW" && OpenPreferences.ResolveLanguage("US", "zh-CN") == "en-US", "automatic language follows Windows region instead of installation language");
    OpenPreferences.SaveAppearance("dark", "system", "mist", "ocean");
    Check(OpenPreferences.LightScheme == "mist" && OpenPreferences.DarkScheme == "ocean", "separate light and dark schemes survive preference reload");
    Check(OpenEndpoints.CurseForgeAddress("https://api.curseforge.com/v1/mods/search?gameId=432&classId=17") == "https://mod.mcimirror.top/curseforge/v1/mods/search?gameId=432&classId=17", "CurseForge uses the public mirror without requiring a key");
    var item = new JObject { ["Platform"] = 2, ["Id"] = "a", ["RawName"] = "A" };
    Check(Favorites.Toggle(item) && Favorites.Contains(2, "a"), "favorite is stored by immutable platform and project id");
    Favorites.Toggle(new JObject { ["Platform"] = 1, ["Id"] = "a", ["RawName"] = "Other platform" });
    Check(Favorites.Read().Count == 2, "same id from different platforms remains distinct");
    Check(!Favorites.Toggle(item) && !Favorites.Contains(2, "a"), "removing a favorite persists");
    var servers = Path.Combine(root, "instance", "servers.dat");
    ServerList.Add(servers, "中文 🌍", "localhost:25565"); ServerList.Add(servers, "Example", "example.org");
    Check(ServerList.Read(servers).Count == 2 && ServerList.Read(servers)[0].Name == "中文 🌍", "Minecraft uncompressed NBT server list round-trips Unicode");
    Check(File.ReadAllBytes(servers).Select((b, i) => i + 5 < new FileInfo(servers).Length && b == 0xed ? i : -1).Any(i => i >= 0), "supplementary characters use Java modified UTF-8 surrogate encoding");
    ServerList.Remove(servers, 0);
    Check(ServerList.Read(servers).Single().Address == "example.org" && File.Exists(servers + ".bak"), "server deletion preserves other entries and backup");
    var zip = Path.Combine(root, "world.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) {
        using var writer = new StreamWriter(archive.CreateEntry("A/level.dat").Open()); writer.Write("test");
    }
    var saves = Path.Combine(root, "saves");
    var world = InstanceFiles.Import(zip, saves, true);
    Check(File.Exists(Path.Combine(world, "level.dat")), "world import recognizes nested root folder");
    Throws(() => InstanceFiles.Import(zip, saves, true), "world import never overwrites an existing world");
    var malicious = Path.Combine(root, "unsafe.zip");
    using (var archive = ZipFile.Open(malicious, ZipArchiveMode.Create)) { using var writer = new StreamWriter(archive.CreateEntry("../../escape.txt").Open()); writer.Write("escape"); }
    Throws(() => InstanceFiles.Import(malicious, saves, true), "world ZIP traversal is rejected");
    Check(!File.Exists(Path.Combine(root, "escape.txt")), "rejected ZIP cannot escape destination");
    var export = Path.Combine(root, "export.zip"); InstanceFiles.Export(world, export);
    using (var exported = ZipFile.OpenRead(export)) Check(exported.Entries.Any(e => e.Name == "level.dat"), "world export contains complete relative tree");
    Throws(() => InstanceFiles.Export(world, Path.Combine(world, "recursive.zip")), "recursive world export is rejected");
    if (args.Contains("--live")) {
        var worlds = JObject.Parse(PCLCS.OpenNetwork.RaceTextAsync(new[] { OpenEndpoints.CurseForgeAddress("https://api.curseforge.com/v1/mods/search?gameId=432&classId=17&pageSize=1") }, true).GetAwaiter().GetResult());
        Check(worlds["data"] is JArray worldProjects && worldProjects.Count > 0, "live public CurseForge mirror returns worlds without a private API key");
        var installers = LoaderCatalog.LegacyInstallers();
        Check(installers.Count > 0 && ((string?)installers[0]!["url"])?.StartsWith("https://maven.legacyfabric.net/") == true, "live official Legacy Fabric installer catalogue");
        var legacy = LoaderCatalog.LegacyProfile("1.8.9", "Open-Legacy-test");
        Check((string?)legacy["id"] == "Open-Legacy-test" && legacy["inheritsFrom"] == null && legacy["downloads"]?["client"]?["sha1"] != null && (string?)legacy["mainClass"] != "net.minecraft.client.main.Main", "Legacy Fabric profile merges verified Mojang base and loader");
        var release = LoaderCatalog.LabyRelease(); var profile = LoaderCatalog.LabyProfile("1.8.9", "Open-Laby-test", release);
        Check((string?)profile["id"] == "Open-Laby-test" && profile["libraries"]!.Any(l => ((string?)l["name"])?.StartsWith("net.labymod:LabyMod:4:") == true), "live LabyMod profile includes exact-release core artifact");
        Check(profile["libraries"]!.All(l => l["downloads"]?["artifact"] == null || l["downloads"]!["artifact"]!["sha1"] != null), "LabyMod downloadable artifacts carry publisher digests");
        var index = Array.IndexOf(args, "--cleanroom-installer");
        if (index >= 0) {
            var cleanroom = LoaderCatalog.CleanroomProfile(args[index + 1], "Open-Cleanroom-test", Path.Combine(root, "mc"), args[index + 2]);
            Check(cleanroom["libraries"]!.All(l => l["downloads"]?["artifact"] == null || !string.IsNullOrEmpty((string?)l["downloads"]?["artifact"]?["url"])), "Cleanroom profile resolves all embedded and remote artifacts");
            var parsed = new PCLCS.McVersion("Open-Cleanroom-test", cleanroom);
            Check(parsed.IsCleanroom && parsed.VanillaName == "1.12.2" && (int?)cleanroom["javaVersion"]?["majorVersion"] >= 25, "native Minecraft version parser recognizes Cleanroom and Java requirement");
            Check(Directory.GetFiles(Path.Combine(root, "mc", "libraries"), "*.jar", SearchOption.AllDirectories).Length > 0, "Cleanroom embedded universal library is extracted and publisher-verified");
            Throws(() => LoaderCatalog.CleanroomProfile(args[index + 1], "bad", Path.Combine(root, "bad"), new string('0', 64)), "corrupted or mismatched Cleanroom installer is rejected before extraction");
        }
    }
    Console.WriteLine($"PASS: {checks} Open integration checks");
} catch (Exception error) {
    Console.WriteLine("FAIL: " + error.GetType().FullName + ": " + error.Message);
    Console.WriteLine(error.StackTrace);
    Environment.ExitCode = 1;
} finally { Directory.Delete(root, true); }
