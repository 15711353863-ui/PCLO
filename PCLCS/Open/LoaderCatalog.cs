// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.IO;
using System.IO.Compression;

namespace PCLCS.Open;

/// <summary>Official protocol adapters. No bundled credentials or author service requests.</summary>
public static class LoaderCatalog {
    private static JToken Get(string url) => JToken.Parse(OpenNetwork.RaceTextAsync(OpenNetwork.Candidates(new[] { url }), true).GetAwaiter().GetResult());
    public static JArray LegacyInstallers() => (JArray)Get("https://meta.legacyfabric.net/v2/versions/installer");
    public static JArray LegacyVersions(string minecraft) => (JArray)Get("https://meta.legacyfabric.net/v2/versions/loader/" + Uri.EscapeDataString(minecraft));
    public static JArray CleanroomInstallers() {
        var result = new JArray();
        foreach (var release in (JArray)Get("https://api.github.com/repos/CleanroomMC/Cleanroom/releases?per_page=30")) {
            if ((bool?)release["draft"] == true) continue;
            foreach (var asset in release["assets"] ?? new JArray()) {
                if (!((string?)asset["name"] ?? "").EndsWith("-installer.jar", StringComparison.Ordinal)) continue;
                var digest = (string?)asset["digest"];
                result.Add(new JObject { ["version"] = release["tag_name"], ["name"] = asset["name"], ["url"] = asset["browser_download_url"], ["sha256"] = digest?.StartsWith("sha256:", StringComparison.Ordinal) == true ? digest.Substring(7) : null, ["size"] = asset["size"] });
            }
        }
        return result;
    }
    public static JObject LabyRelease() => (JObject)Get("https://releases.r2.labymod.net/api/v1/manifest/production/latest.json");
    public static JObject CleanroomProfile(string installer, string id, string minecraftFolder, string digest) {
        // Verify before opening or extracting the official package. No installer code is executed.
        OpenNetwork.VerifyFile(installer, digest, -1, false);
        using var zip = ZipFile.OpenRead(installer);
        JObject Read(string name) {
            using var stream = (zip.GetEntry(name) ?? throw new InvalidDataException("Cleanroom 安装器缺少 " + name)).Open();
            using var reader = new StreamReader(stream);
            return JObject.Parse(reader.ReadToEnd());
        }
        var installation = Read("install_profile.json");
        if ((installation["processors"] as JArray)?.Count > 0) throw new NotSupportedException("此 Cleanroom 安装器需要额外处理器，当前版本无法直接安装。请保留已下载的官方安装器。");
        var minecraft = (string?)installation["minecraft"] ?? throw new InvalidDataException("Cleanroom 缺少 Minecraft 版本。");
        var profile = Vanilla(minecraft);
        profile.Merge(Read("version.json"), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        var libraryRoot = Path.GetFullPath(Path.Combine(minecraftFolder, "libraries")) + Path.DirectorySeparatorChar;
        foreach (var artifact in (profile["libraries"] ?? new JArray()).Select(l => l["downloads"]?["artifact"]).Where(a => a != null)) {
            var relative = (string?)artifact!["path"] ?? throw new InvalidDataException("Cleanroom 库缺少路径。");
            var target = Path.GetFullPath(Path.Combine(libraryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(libraryRoot, StringComparison.OrdinalIgnoreCase) || relative.Contains(':')) throw new InvalidDataException("Cleanroom 库路径无效。");
            if (!string.IsNullOrEmpty((string?)artifact["url"])) continue;
            var embedded = zip.GetEntry("maven/" + relative);
            if (embedded == null) throw new InvalidDataException("Cleanroom 安装器缺少内嵌依赖：" + relative);
            if (embedded.Length > 256L * 1024 * 1024) throw new InvalidDataException("Cleanroom 内嵌依赖过大。");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                embedded.ExtractToFile(temporary);
                OpenNetwork.VerifyFile(temporary, (string?)artifact["sha1"], (long?)artifact["size"] ?? -1, false);
                if (File.Exists(target)) File.Replace(temporary, target, target + ".bak"); else File.Move(temporary, target);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
            artifact["url"] = "https://maven.cleanroommc.com/" + relative;
        }
        profile["id"] = id; profile["clientVersion"] = minecraft;
        profile["pclOpenLoader"] = new JObject { ["name"] = "Cleanroom", ["version"] = installation["cleanroomVersion"]?.DeepClone() };
        OpenNetwork.CaptureMetadata(profile.ToString());
        return profile;
    }
    public static JObject Vanilla(string version) {
        var manifest = Get(OpenNetwork.MojangManifest);
        var entry = manifest["versions"]!.FirstOrDefault(x => (string?)x["id"] == version) ?? throw new ArgumentException("Mojang 中不存在该 Minecraft 版本。");
        var text = OpenNetwork.RaceTextAsync(OpenNetwork.Candidates(new[] { (string)entry["url"]! }), true).GetAwaiter().GetResult();
        using var sha1 = SHA1.Create();
        var actual = BitConverter.ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        if (!actual.Equals((string?)entry["sha1"], StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Mojang 版本 JSON 校验失败。");
        OpenNetwork.CaptureMetadata(text);
        return JObject.Parse(text);
    }
    public static JObject LegacyProfile(string minecraft, string id, string? selectedVersion = null) {
        var loaders = LegacyVersions(minecraft);
        var loader = loaders.FirstOrDefault(x => (bool?)x["loader"]?["stable"] == true) ?? loaders.FirstOrDefault() ?? throw new ArgumentException("Legacy Fabric 不支持该 Minecraft 版本。");
        var version = selectedVersion ?? (string)loader["loader"]!["version"]!;
        if (!loaders.Any(l => (string?)l["loader"]?["version"] == version)) throw new ArgumentException("Legacy Fabric 版本与 Minecraft 不兼容。");
        var profile = (JObject)Get("https://meta.legacyfabric.net/v2/versions/loader/" + Uri.EscapeDataString(minecraft) + "/" + Uri.EscapeDataString(version) + "/profile/json");
        var result = Vanilla(minecraft);
        result.Merge(profile, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union, MergeNullValueHandling = MergeNullValueHandling.Ignore });
        result.Remove("inheritsFrom"); result["id"] = id; result["clientVersion"] = minecraft;
        result["pclOpenLoader"] = new JObject { ["name"] = "Legacy Fabric", ["version"] = version };
        OpenNetwork.CaptureMetadata(result.ToString());
        return result;
    }
    public static JObject LabyProfile(string minecraft, string id, JObject release) {
        var commit = (string)release["commitReference"]!;
        var entry = release["minecraftVersions"]!.FirstOrDefault(v => (string?)v["version"] == minecraft) ?? throw new ArgumentException("LabyMod 不支持该 Minecraft 版本。");
        var url = (string?)entry["customManifestUrl"] ?? $"https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/production/{Uri.EscapeDataString(minecraft)}/{Uri.EscapeDataString(commit)}.json";
        var profile = (JObject)Get(url);
        var libraryIndex = Get("https://releases.r2.labymod.net/api/v1/libraries/production.json");
        var isolated = new HashSet<string>(libraryIndex["isolated_libraries"]!.Where(n => n["versions"]!.Values<string>().Contains(minecraft)).Select(n => (string)n["name"]!));
        var libraries = new JArray((profile["libraries"] ?? new JArray()).Where(l => !isolated.Contains(((string?)l["name"] ?? "").Split(':').ElementAtOrDefault(1) ?? "")).Select(l => l.DeepClone()));
        foreach (var lib in libraryIndex["libraries"] ?? new JArray()) {
            var game = (string?)lib["minecraftVersion"];
            if (game != null && game != "all" && game != minecraft) continue;
            var libUrl = (string)lib["url"]!;
            var uri = new Uri(libUrl);
            var marker = "/libraries/"; var offset = uri.AbsolutePath.IndexOf(marker, StringComparison.Ordinal);
            if (offset < 0) throw new InvalidOperationException("LabyMod 库路径无效。");
            libraries.Add(new JObject { ["name"] = lib["name"]?.DeepClone(), ["downloads"] = new JObject { ["artifact"] = new JObject {
                ["url"] = libUrl, ["path"] = uri.AbsolutePath.Substring(offset + marker.Length), ["sha1"] = lib["sha1"]?.DeepClone(), ["size"] = lib["size"]?.DeepClone()
            } } });
        }
        libraries.Add(new JObject { ["name"] = "net.labymod:LabyMod:4:" + commit, ["downloads"] = new JObject { ["artifact"] = new JObject {
            ["path"] = "net/labymod/LabyMod/4/" + commit + "/LabyMod-4-" + commit + ".jar", ["url"] = $"https://releases.r2.labymod.net/api/v1/download/labymod4/production/{Uri.EscapeDataString(commit)}.jar", ["sha1"] = release["sha1"]?.DeepClone(), ["size"] = release["size"]?.DeepClone()
        } } });
        profile["libraries"] = libraries; profile["id"] = id; profile["clientVersion"] = minecraft;
        profile["labymod_data"] = new JObject { ["channelType"] = "production", ["commitReference"] = commit, ["version"] = release["labyModVersion"]?.DeepClone(), ["versionType"] = "release" };
        profile["pclOpenLoader"] = new JObject { ["name"] = "LabyMod", ["version"] = release["labyModVersion"]?.DeepClone() };
        OpenNetwork.CaptureMetadata(profile.ToString());
        return profile;
    }
}
