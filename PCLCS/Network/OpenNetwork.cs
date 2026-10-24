// SPDX-License-Identifier: MIT
using System.Net.Http;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace PCLCS;

/// <summary>Public upstreams, bounded mirror races, redirect isolation and publisher digest discovery.</summary>
public static class OpenNetwork {
    public const string ProductName = "Plain Craft Launcher Open";
    public const string MojangManifest = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    public const string Mirror = "https://bmclapi2.bangbang93.com";
    private static readonly ConcurrentDictionary<string, string> Digests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Task<string?>> Sidecars = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient MetadataClient = new(CreateHandler()) { Timeout = Timeout.InfiniteTimeSpan };

    public static bool IsAuthorService(string address) {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        return host == "meloong.com" || host.EndsWith(".meloong.com") || host == "pcl2.server" ||
            host.EndsWith(".pcl2.server") || host.Contains("pcl2-server") || host == "ltcat.net" || host.EndsWith(".ltcat.net");
    }
    public static void EnsureAllowed(string address) {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new HttpRequestException("下载地址必须使用 HTTPS：" + address);
        if (IsAuthorService(address)) throw new HttpRequestException("已断开 PCL 作者服务，取消请求：" + uri.Host);
    }
    public static HttpMessageHandler CreateHandler() => new RedirectGuard(new HttpClientHandler {
        AllowAutoRedirect = false, UseCookies = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });

    public static string Canonical(string address) {
        var u = new Uri(address); var path = u.AbsolutePath; var host = u.Host.ToLowerInvariant();
        if (host == "files.minecraftforge.net" && path.StartsWith("/maven/")) return "https://maven.minecraftforge.net/" + path.Substring(7) + u.Query;
        if (host != "bmclapi2.bangbang93.com") return address;
        string? root = null;
        if (path.StartsWith("/maven/net/minecraftforge/")) { root = "https://maven.minecraftforge.net"; path = path.Substring(6); }
        else if (path.StartsWith("/maven/net/neoforged/")) { root = "https://maven.neoforged.net/releases"; path = path.Substring(6); }
        else if (path.StartsWith("/maven/net/fabricmc/")) { root = "https://maven.fabricmc.net"; path = path.Substring(6); }
        else if (path.StartsWith("/fabric-meta/")) { root = "https://meta.fabricmc.net"; path = path.Substring(12); }
        else if (path.StartsWith("/assets/")) { root = "https://resources.download.minecraft.net"; path = path.Substring(7); }
        else if (path.StartsWith("/maven/")) { root = "https://libraries.minecraft.net"; path = path.Substring(6); }
        else if (path.StartsWith("/libraries/")) { root = "https://libraries.minecraft.net"; path = path.Substring(10); }
        else if (path.StartsWith("/v1/packages/") || path.StartsWith("/mc/game/")) root = "https://piston-meta.mojang.com";
        else if (path.StartsWith("/v1/objects/")) root = "https://piston-data.mojang.com";
        return root == null ? address : root + path + u.Query;
    }
    public static string[] Candidates(IEnumerable<string> addresses) {
        var result = new List<string>();
        foreach (var raw in addresses.Where(a => !string.IsNullOrWhiteSpace(a))) {
            var address = raw.Replace("{CDN}", "").Trim();
            // Historical manifests contain http URLs; upgrade known public sources before validation.
            if (address.StartsWith("http://") && !new Uri(address).IsLoopback) address = "https://" + address.Substring(7);
            EnsureAllowed(address);
            var canonical = Canonical(address); Add(canonical); var u = new Uri(canonical); var path = u.PathAndQuery;
            string? mirror = u.Host.ToLowerInvariant() switch {
                "piston-meta.mojang.com" or "piston-data.mojang.com" or "launchermeta.mojang.com" or "launcher.mojang.com" => Mirror + path,
                "resources.download.minecraft.net" => Mirror + "/assets" + path,
                "libraries.minecraft.net" or "maven.fabricmc.net" or "maven.minecraftforge.net" => Mirror + "/maven" + path,
                "maven.neoforged.net" when path.StartsWith("/releases/") => Mirror + "/maven" + path.Substring(9),
                "meta.fabricmc.net" => Mirror + "/fabric-meta" + path,
                _ => null
            };
            if (mirror != null) Add(mirror);
            Add(address);
        }
        return result.ToArray();
        void Add(string url) { if (!result.Contains(url, StringComparer.OrdinalIgnoreCase)) result.Add(url); }
    }

    public static void CaptureMetadata(string text) {
        JToken root;
        try { root = JToken.Parse(text); } catch { return; }
        if (root is not JContainer container) return;
        foreach (var obj in container.DescendantsAndSelf().OfType<JObject>()) {
            var url = obj["url"]?.Value<string>();
            var hash = obj["sha512"]?.Value<string>() ?? obj["sha256"]?.Value<string>() ?? obj["sha1"]?.Value<string>();
            if (url != null && IsDigest(hash) && Uri.TryCreate(url, UriKind.Absolute, out _)) Digests[Canonical(url)] = hash!;
        }
    }
    private static bool IsDigest(string? value) => value != null && new[] { 32, 40, 64, 128 }.Contains(value.Length) && value.All(Uri.IsHexDigit);
    public static string? KnownDigest(IEnumerable<string> urls) {
        foreach (var url in urls) {
            if (Digests.TryGetValue(Canonical(url), out var hash)) return hash;
            var path = new Uri(url).AbsolutePath;
            var match = Regex.Match(path, @"/(?:packages|objects)/([0-9a-f]{40})/|/assets/[0-9a-f]{2}/([0-9a-f]{40})$", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (new Uri(Canonical(url)).Host == "resources.download.minecraft.net") {
                match = Regex.Match(path, @"/[0-9a-f]{2}/([0-9a-f]{40})$", RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups[1].Value;
            }
            if (new Uri(url).Host == "textures.minecraft.net") {
                match = Regex.Match(path, @"/texture/([0-9a-f]{64})$", RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups[1].Value;
            }
        }
        return null;
    }
    public static bool RequiresDigest(string file, IEnumerable<string> urls) =>
        !new[] { ".json", ".txt", ".xml", ".xaml", ".ini", ".html", ".htm", ".sha1", ".sha256", ".sha512", ".md5", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".ico", ".bmp" }.Contains(Path.GetExtension(file).ToLowerInvariant()) &&
        urls.Any(u => IsManagedArtifact(u));
    private static bool IsManagedArtifact(string url) {
        var host = new Uri(Canonical(url)).Host;
        return host.EndsWith("mojang.com") || host.EndsWith("minecraft.net") || host.StartsWith("maven.") || host == "bmclapi2.bangbang93.com" || host.EndsWith("forgecdn.net") || host == "cdn.modrinth.com";
    }
    public static async Task<string?> ResolveDigestAsync(IEnumerable<string> urls) {
        var known = KnownDigest(urls); if (known != null) return known;
        foreach (var url in urls.Select(Canonical).Distinct()) {
            if (!new Uri(url).Host.StartsWith("maven.")) continue;
            var result = await Sidecars.GetOrAdd(url, FetchSidecarAsync).ConfigureAwait(false);
            if (result != null) return result;
            Sidecars.TryRemove(url, out _);
        }
        return null;
    }
    private static async Task<string?> FetchSidecarAsync(string url) {
        foreach (var suffix in new[] { ".sha256", ".sha1" }) {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Get, url + suffix);
                using var cancellation = new CancellationTokenSource(8000);
                request.Headers.TryAddWithoutValidation("User-Agent", "PlainCraftLauncherOpen/0.2");
                using var response = await MetadataClient.SendAsync(request,cancellation.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;
                var value = Regex.Match((await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim(), @"^[0-9a-fA-F]+").Value;
                if (IsDigest(value) && (value.Length == 40 || value.Length == 64)) { Digests[url] = value; return value; }
            } catch (HttpRequestException) { } catch (TaskCanceledException) { }
        }
        return null;
    }

    public sealed class SourceResponse {
        public string SourceUrl { get; }
        public HttpResponseMessage Response { get; }
        internal SourceResponse(string url, HttpResponseMessage response) { SourceUrl = url; Response = response; }
    }
    /// <summary>Race real GETs through the first body bytes; cancel and dispose losing responses.</summary>
    public static async Task<SourceResponse> RaceAsync(HttpClient client, HttpRequestMessage template, IEnumerable<string> urls, long expectedSize, CancellationToken token) {
        var addresses = urls.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
        if (addresses.Length == 0) throw new HttpRequestException("没有可用下载源");
        var cancellations = addresses.Select(_ => CancellationTokenSource.CreateLinkedTokenSource(token)).ToArray();
        var pending = addresses.Select((url, i) => Send(url, cancellations[i].Token)).ToList();
        var errors = new List<Exception>();
        try {
            while (pending.Count > 0) {
                var completed = await Task.WhenAny(pending).ConfigureAwait(false); pending.Remove(completed);
                try {
                    var winner = await completed.ConfigureAwait(false);
                    for (var i = 0; i < addresses.Length; i++) if (addresses[i] != winner.SourceUrl) cancellations[i].Cancel();
                    foreach (var loser in pending) _ = loser.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Response.Dispose(); else _ = t.Exception; }, TaskScheduler.Default);
                    return winner;
                } catch (Exception ex) { errors.Add(ex); }
            }
            token.ThrowIfCancellationRequested();
            throw new HttpRequestException("所有下载源均失败：" + string.Join("；", errors.Select(e => e.Message)), new AggregateException(errors));
        } finally { foreach (var c in cancellations) c.Dispose(); }

        async Task<SourceResponse> Send(string url, CancellationToken cancellation) {
            HttpResponseMessage? response = null;
            try {
                using var request = CloneRequest(template, new Uri(url));
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (expectedSize >= 0 && response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength != expectedSize)
                    throw new InvalidDataException("下载源返回的文件大小与元数据不符：" + url);
                var original = response.Content;
                var stream = await original.ReadAsStreamAsync().ConfigureAwait(false);
                var first = new byte[4096]; var count = await stream.ReadAsync(first, 0, first.Length, cancellation).ConfigureAwait(false);
                if (count == 0 && expectedSize != 0) throw new InvalidDataException("下载源返回空文件：" + url);
                var content = new PrefixContent(original, stream, first, count);
                response.Content = content;
                cancellation.ThrowIfCancellationRequested();
                return new SourceResponse(url, response);
            } catch { response?.Dispose(); throw; }
        }
    }
    public static async Task<string> RaceTextAsync(IEnumerable<string> urls, bool json, int timeoutMilliseconds = 20000, string? curseForgeApiKey = null) {
        var addresses = Candidates(urls); using var cts = new CancellationTokenSource(timeoutMilliseconds);
        var tasks = addresses.Take(4).Select(Read).ToList(); var errors = new List<Exception>();
        try {
            while (tasks.Count > 0) {
                var done = await Task.WhenAny(tasks).ConfigureAwait(false); tasks.Remove(done);
                try { var result = await done.ConfigureAwait(false); cts.Cancel(); CaptureMetadata(result); return result; }
                catch (Exception ex) { errors.Add(ex); }
            }
            throw new HttpRequestException("全部元数据源均失败", new AggregateException(errors));
        } finally { foreach (var task in tasks) _ = task.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default); }
        async Task<string> Read(string url) {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "PlainCraftLauncherOpen/0.2");
            if (new Uri(url).Host == "api.curseforge.com" && !string.IsNullOrWhiteSpace(curseForgeApiKey)) request.Headers.TryAddWithoutValidation("x-api-key",curseForgeApiKey);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            using var response = await MetadataClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode(); var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (json) JToken.Parse(result);
            return result;
        }
    }
    public static string VerifyFile(string path, string? expectedHash, long expectedSize, bool json) {
        var info = new FileInfo(path); if (!info.Exists) throw new InvalidDataException("文件不存在：" + path);
        if (expectedSize >= 0 && info.Length != expectedSize) throw new InvalidDataException("文件长度校验失败：" + path);
        if (json) CaptureAndValidate(File.ReadAllText(path));
        if (new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".ico", ".bmp" }.Contains(info.Extension.ToLowerInvariant())) ValidateImage(path);
        using var stream = File.OpenRead(path); using var sha256 = SHA256.Create();
        var digest = Hex(sha256.ComputeHash(stream));
        if (!string.IsNullOrWhiteSpace(expectedHash)) {
            using HashAlgorithm algorithm = expectedHash!.Length switch { 32 => MD5.Create(), 40 => SHA1.Create(), 64 => SHA256.Create(), 128 => SHA512.Create(), _ => throw new InvalidDataException("无法识别的校验摘要") };
            stream.Position = 0;
            var actual = expectedHash.Length == 64 ? digest : Hex(algorithm.ComputeHash(stream));
            if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("文件哈希校验失败：" + path);
        }
        return digest;
        static void CaptureAndValidate(string text) { JToken.Parse(text); CaptureMetadata(text); }
    }
    private static void ValidateImage(string path) {
        using var stream = File.OpenRead(path); var header = new byte[16]; var count = stream.Read(header,0,header.Length);
        var valid = count >= 12 && (
            (header[0] == 137 && header[1] == 80 && header[2] == 78 && header[3] == 71) ||
            (header[0] == 255 && header[1] == 216 && header[2] == 255) ||
            Encoding.ASCII.GetString(header,0,4) == "GIF8" ||
            (header[0] == 0 && header[1] == 0 && header[2] == 1 && header[3] == 0) ||
            (header[0] == 66 && header[1] == 77));
        if (count >= 12 && Encoding.ASCII.GetString(header,0,4) == "RIFF" && Encoding.ASCII.GetString(header,8,4) == "WEBP")
            valid = (long)BitConverter.ToUInt32(header,4) + 8 == stream.Length;
        if (!valid) throw new InvalidDataException("图片格式/长度校验失败：" + path);
    }
    private static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original, Uri destination) {
        var result = new HttpRequestMessage(original.Method, destination);
        foreach (var header in original.Headers) {
            if (!destination.Host.Equals(original.RequestUri!.Host, StringComparison.OrdinalIgnoreCase) &&
                new[] { "Authorization", "Cookie", "x-api-key" }.Contains(header.Key, StringComparer.OrdinalIgnoreCase)) continue;
            result.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return result;
    }
    private sealed class RedirectGuard : DelegatingHandler {
        public RedirectGuard(HttpMessageHandler inner) : base(inner) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            var current = request;
            try {
                for (var i = 0; i <= 8; i++) {
                    EnsureAllowed(current.RequestUri!.AbsoluteUri);
                    var response = await base.SendAsync(current, token).ConfigureAwait(false);
                    var status = (int)response.StatusCode;
                    if (!(status is 301 or 302 or 303 or 307 or 308) || response.Headers.Location == null) return response;
                    var target = new Uri(current.RequestUri, response.Headers.Location); response.Dispose(); EnsureAllowed(target.AbsoluteUri);
                    if (current.Method != HttpMethod.Get && current.Method != HttpMethod.Head) throw new HttpRequestException("拒绝自动重定向非下载请求");
                    var next = CloneRequest(current, target); if (current != request) current.Dispose(); current = next;
                }
                throw new HttpRequestException("下载重定向次数过多");
            } finally { if (current != request) current.Dispose(); }
        }
    }
    private sealed class PrefixContent : HttpContent {
        private readonly HttpContent original; private readonly Stream stream; private readonly byte[] prefix; private readonly int count;
        public PrefixContent(HttpContent original, Stream stream, byte[] prefix, int count) {
            this.original = original; this.stream = stream; this.prefix = prefix; this.count = count;
            foreach (var header in original.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new PrefixStream(stream, prefix, count));
        protected override async Task SerializeToStreamAsync(Stream target, TransportContext? context) {
            await target.WriteAsync(prefix, 0, count).ConfigureAwait(false); await stream.CopyToAsync(target).ConfigureAwait(false);
        }
        protected override bool TryComputeLength(out long length) { length = original.Headers.ContentLength ?? -1; return length >= 0; }
        protected override void Dispose(bool disposing) { if (disposing) original.Dispose(); base.Dispose(disposing); }
    }
    private sealed class PrefixStream : Stream {
        private readonly Stream stream; private readonly byte[] prefix; private readonly int count; private int position;
        public PrefixStream(Stream stream, byte[] prefix, int count) { this.stream = stream; this.prefix = prefix; this.count = count; }
        public override int Read(byte[] buffer, int offset, int size) { if (position >= count) return stream.Read(buffer, offset, size); var n = Math.Min(size, count-position); Array.Copy(prefix,position,buffer,offset,n); position+=n; return n; }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken token) => position < count ? Task.FromResult(Read(buffer,offset,size)) : stream.ReadAsync(buffer,offset,size,token);
        protected override void Dispose(bool disposing) { if (disposing) stream.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer,int offset,int count) => throw new NotSupportedException();
    }
}
