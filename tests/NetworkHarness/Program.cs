// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using PCLCS;
using Newtonsoft.Json.Linq;

internal static class Program {
    private static int passed;
    private static async Task Main(string[] args) {
        ServicePointManager.DefaultConnectionLimit = 32;
        using var server = new TestServer();
        using var client = new HttpClient(OpenNetwork.CreateHandler());
        var payload = TestServer.Payload;
        using (var request = new HttpRequestMessage(HttpMethod.Get, server.Url("slow"))) {
            var started = DateTime.UtcNow;
            var result = await OpenNetwork.RaceAsync(client,request,new[] { server.Url("slow"),server.Url("fast") },payload.Length,CancellationToken.None);
            using (result.Response) {
                Assert(result.SourceUrl.EndsWith("fast"), "slower official analogue loses race");
                Assert((await result.Response.Content.ReadAsByteArrayAsync()).SequenceEqual(payload), "first-byte race preserves full response body");
            }
            Assert((DateTime.UtcNow-started).TotalSeconds < 1, "race avoids waiting for slow source");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get,server.Url("error"))) {
            var winner = await OpenNetwork.RaceAsync(client,request,new[] { server.Url("error"),server.Url("fast") },payload.Length,CancellationToken.None);
            using (winner.Response) Assert(winner.SourceUrl.EndsWith("fast"), "HTTP failure falls back to alternate source");
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get,server.Url("wrong-size"))) {
            var winner = await OpenNetwork.RaceAsync(client,request,new[] { server.Url("wrong-size"),server.Url("fast") },payload.Length,CancellationToken.None);
            using (winner.Response) Assert(winner.SourceUrl.EndsWith("fast"), "wrong content length cannot win");
        }
        var json = await OpenNetwork.RaceTextAsync(new[] {server.Url("invalid-json"),server.Url("json")},true);
        Assert(json.Contains("versions"), "invalid JSON response cannot win metadata race");
        using (var request = new HttpRequestMessage(HttpMethod.Get,server.Url("slow"))) {
            using var cts = new CancellationTokenSource(50);
            await ThrowsAsync<OperationCanceledException>(async () => { var r=await OpenNetwork.RaceAsync(client,request,new[]{server.Url("slow")},payload.Length,cts.Token); r.Response.Dispose(); }, "race cancellation propagates");
        }
        await ThrowsAsync<HttpRequestException>(async () => { using var r=await client.GetAsync("https://mcnews.meloong.com/"); }, "author host is blocked before network access");
        await ThrowsAsync<HttpRequestException>(async () => { using var r=await client.GetAsync(server.Url("redirect")); }, "redirect to author host is blocked");
        var tmp=Path.GetTempFileName();
        try {
            File.WriteAllBytes(tmp,payload); string sha1;
            using (var hash=SHA1.Create()) sha1=BitConverter.ToString(hash.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
            Assert(OpenNetwork.VerifyFile(tmp,sha1,payload.Length,false).Length==64,"publisher SHA1 and computed SHA256 verification");
            File.AppendAllText(tmp,"corruption");
            await ThrowsAsync<InvalidDataException>(()=>Task.Run(()=>OpenNetwork.VerifyFile(tmp,sha1,-1,false)),"corrupted download is rejected");
        } finally { File.Delete(tmp); }
        var digest=new string('a',40);
        OpenNetwork.CaptureMetadata("{\"versions\":[{\"url\":\"https://piston-meta.mojang.com/v1/packages/"+digest+"/test.json\",\"sha1\":\""+digest+"\"}]}");
        Assert(OpenNetwork.KnownDigest(new[]{"https://piston-meta.mojang.com/v1/packages/"+digest+"/test.json"})==digest,"manifest digest follows mirrored version metadata");
        var sources=OpenNetwork.Candidates(new[]{"https://bmclapi2.bangbang93.com/maven/net/fabricmc/fabric-loader/0.16.0/fabric-loader-0.16.0.jar"});
        Assert(sources.Any(u=>u.StartsWith("https://maven.fabricmc.net/"))&&sources.Any(u=>u.StartsWith(OpenNetwork.Mirror)),"loader mirrors retain canonical official source");
        Assert(OpenNetwork.RequiresDigest("loader.jar",sources),"managed loader binaries require publisher digest");
        Assert(!OpenNetwork.RequiresDigest("cover.png",new[]{"https://cdn.modrinth.com/data/example/cover.webp"}),"unhashed cover images are not treated as installers");
        var imagePath=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");
        try {
            File.WriteAllText(imagePath,"<html>corrupt cover response</html>");
            await ThrowsAsync<InvalidDataException>(()=>Task.Run(()=>OpenNetwork.VerifyFile(imagePath,null,-1,false)),"HTML error pages cannot pass image verification");
        } finally { File.Delete(imagePath); }
        if (args.Contains("--live")) await LiveSmoke(client);
        Console.WriteLine($"PASS: {passed} network and integrity checks on .NET Framework {Environment.Version}");
    }
    private static async Task LiveSmoke(HttpClient client) {
        var manifest=JObject.Parse(await OpenNetwork.RaceTextAsync(new[]{OpenNetwork.MojangManifest},true,30000));
        var version=manifest["versions"]!.First(v=>v["id"]!.Value<string>()=="1.21.1");
        var url=version["url"]!.Value<string>()!;
        var metadata=await OpenNetwork.RaceTextAsync(new[]{url},true,30000);
        var temp=Path.GetTempFileName();
        try {
            File.WriteAllText(temp,metadata,new UTF8Encoding(false));
            OpenNetwork.VerifyFile(temp,version["sha1"]!.Value<string>(),-1,true);
            Assert(JObject.Parse(metadata)["downloads"]?["client"]?["sha1"]!=null,"LIVE Mojang manifest and version JSON SHA1 verification");
            var fabric=JArray.Parse(await OpenNetwork.RaceTextAsync(new[]{"https://meta.fabricmc.net/v2/versions/loader"},true,30000));
            var loader=fabric[0]["version"]!.Value<string>();
            var artifact=$"https://maven.fabricmc.net/net/fabricmc/fabric-loader/{loader}/fabric-loader-{loader}.jar";
            var candidates=OpenNetwork.Candidates(new[]{artifact});
            var hash=await OpenNetwork.ResolveDigestAsync(candidates);
            Assert(hash!=null,"LIVE Fabric publisher sidecar digest discovery");
            using var request=new HttpRequestMessage(HttpMethod.Get,artifact);
            using var cts=new CancellationTokenSource(45000);
            var winner=await OpenNetwork.RaceAsync(client,request,candidates,-1,cts.Token);
            using(winner.Response) {
                File.WriteAllBytes(temp,await winner.Response.Content.ReadAsByteArrayAsync());
                OpenNetwork.VerifyFile(temp,hash,-1,false);
                Assert(new FileInfo(temp).Length>10000,"LIVE official/mirror Fabric JAR download and digest verification");
                Console.WriteLine("LIVE source: "+winner.SourceUrl);
            }
        } finally { File.Delete(temp); }
    }
    private static void Assert(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); passed++; Console.WriteLine("PASS: "+name); }
    private static async Task ThrowsAsync<T>(Func<Task> action,string name) where T:Exception { try { await action(); } catch(T) { Assert(true,name); return; } throw new Exception("FAIL: "+name); }
    private sealed class TestServer:IDisposable {
        public static readonly byte[] Payload=Encoding.UTF8.GetBytes(new string('x',12000)+"plain-craft-launcher-open");
        private readonly HttpListener listener=new(); private readonly string root;
        public TestServer() {
            var probe=new TcpListener(IPAddress.Loopback,0); probe.Start(); var port=((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            root=$"http://127.0.0.1:{port}/"; listener.Prefixes.Add(root); listener.Start(); _=Run();
        }
        public string Url(string path)=>root+path;
        private async Task Run() { while(listener.IsListening) { try { var context=await listener.GetContextAsync(); _=Reply(context); } catch(ObjectDisposedException) { break; } catch(HttpListenerException) { break; } } }
        private static async Task Reply(HttpListenerContext c) {
            try {
                var path=c.Request.Url!.AbsolutePath;
                if(path=="/slow") await Task.Delay(1300);
                if(path=="/fast") await Task.Delay(80);
                if(path=="/json") await Task.Delay(80);
                if(path=="/error") { c.Response.StatusCode=503; c.Response.Close(); return; }
                if(path=="/redirect") { c.Response.StatusCode=302; c.Response.RedirectLocation="https://mcnews.meloong.com/"; c.Response.Close(); return; }
                var bytes=path=="/wrong-size"?new byte[]{1,2}:path=="/invalid-json"?Encoding.UTF8.GetBytes("<html>bad mirror</html>"):path=="/json"?Encoding.UTF8.GetBytes("{\"versions\":[]}"):Payload;
                c.Response.ContentLength64=bytes.Length; await c.Response.OutputStream.WriteAsync(bytes,0,bytes.Length); c.Response.Close();
            } catch { c.Response.Abort(); }
        }
        public void Dispose() { listener.Stop(); listener.Close(); }
    }
}
