using System.Net;
using System.Text;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Services.GameResourceProvider;

namespace Project.Test;

[TestClass]
public sealed class LauncherResourceProviderTests
{
    private const string BundleConfig = """
        {"cdnList":[],"bundles":{"HD":{"resourcePacks":["common"]}},"resourcePacks":{"common":{"version":"3.7.0"}},"config":{"functionCode":{"background":"bg-code"}}}
        """;
    private const string News = """
        {"guidance":{"notice":{"title":"公告","contents":[{"content":"notice-text","jumpUrl":"https://example.com/news","time":"10-08"}]}},"slideshow":[]}
        """;
    private const string Background = """
        {"functionSwitch":1,"backgroundFile":"https://assets.example/bg.mp4","backgroundFileType":2,"firstFrameImage":"https://assets.example/frame.webp","slogan":"https://assets.example/slogan.png"}
        """;

    [TestMethod]
    public async Task BundleConfigurationAcceptsGzipWithOrWithoutEncodingHeader()
    {
        foreach (var hasHeader in new[] { false, true })
        {
            using var h = new Handler { Gzip = true, GzipHeader = hasHeader };
            using var client = new HttpClient(h);
            var p = new BundleGameResourceProvider(client);
            p.SetConfig(new GameLocalConfig(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), Config());
            Assert.AreEqual("bg-code", (await p.GetDefaultLauncherValue())!.FunctionCode.Background);
            Assert.AreEqual(1, h.Requests.Count);
        }
    }

    [TestMethod]
    public async Task BundleNewsAndBackgroundUseNewKeyAppGameRoutesAndServerCode()
    {
        using var h = new Handler(); using var client = new HttpClient(h);
        var config = Config(); var p = new BundleGameResourceProvider(client);
        p.SetConfig(new GameLocalConfig(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), config);
        Assert.AreEqual("notice-text", (await p.GetLauncherStarterAsync())!.Guidance.Notice.Contents[0].TContent);
        Assert.AreEqual("/launcher/app/10003_launcher-key/G152/official/information/zh-Hans.json", h.Requests[0].AbsolutePath);
        var meta = await p.GetDefaultLauncherValue();
        Assert.AreEqual("bg-code", meta!.FunctionCode.Background);
        var bg = await p.GetLauncherBackgroundDataAsync(meta.FunctionCode.Background);
        Assert.AreEqual(2, bg!.BackgroundFileType);
        Assert.AreEqual("/launcher/game/10003_launcher-key/G152/official/background/bg-code/zh-Hans.json", h.Requests.Last().AbsolutePath);
        Assert.IsTrue(h.Requests.All(u => u.Host != "old.example"));
    }

    [TestMethod]
    public async Task LegacyContentRoutesStillUseOldKeyWithoutServerCode()
    {
        using var h = new Handler(); using var client = new HttpClient(h);
        var p = new LegacyGameResourceProvider(client);
        p.SetConfig(new GameLocalConfig(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), Config());
        await p.GetLauncherStarterAsync(); await p.GetLauncherBackgroundDataAsync("bg-code");
        Assert.AreEqual("/launcher/10003_old-key/G152/information/zh-Hans.json", h.Requests[0].AbsolutePath);
        Assert.AreEqual("/launcher/10003_old-key/G152/background/bg-code/zh-Hans.json", h.Requests[1].AbsolutePath);
        Assert.IsTrue(h.Requests.All(u => u.Host == "old.example"));
    }

    [TestMethod]
    public async Task LauncherBootstrapOnlyChangesBunleFields()
    {
        var folder = Path.Combine(Path.GetTempPath(), "haiyu-bootstrap-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, "Assets"));
        var file = Path.Combine(folder, "Assets", "KRApp.conf");
        var json = """
            {"appId":"10003","appKey":"launcher-key","launcherConfigUrl":"https://new.example/launcher/app/10003_launcher-key/index.json","games":[{"gameId":"G152","resId":"10003","appKey":"resource-key","serverCode":"official","configUrl":"https://new.example/launcher/game/10003_resource-key/G152/official/index.json","backUpConfigUrl":"https://backup.example/config","defaultBundleName":"HD"}]}
            """;
        var bytes = Encoding.UTF8.GetBytes(json);
        for(int i = 0; i < bytes.Length; i++) bytes[i] ^= 99;
        await File.WriteAllTextAsync(file, Convert.ToBase64String(bytes));
        try
        {
            using var h = new Handler(); using var client = new HttpClient(h);
            var config = Config(); var p = new BundleGameResourceProvider(client);
            p.SetConfig(new GameLocalConfig(Path.Combine(folder, "settings.json")), config);
            await p.ConfigureFromLauncherAsync(folder);
            Assert.AreEqual("resource-key", config.BunleAppKey);
            Assert.AreEqual("launcher-key", config.BunleLauncherAppKey);
            Assert.AreEqual("HD", config.BunleDefaultBundleName);
            Assert.AreEqual("old-key", config.AppKey);
            Assert.AreEqual("https://old.example/config", config.ConfigUrl);
            Assert.AreEqual("bg-code", (await p.GetDefaultLauncherValue())!.FunctionCode.Background);
        }
        finally { File.Delete(file); }
    }

    [TestMethod]
    public async Task BackgroundDisabledDoesNotRequestEmptyCodeUrl()
    {
        using var h = new Handler(); using var client = new HttpClient(h);
        var p = new BundleGameResourceProvider(client);
        p.SetConfig(new GameLocalConfig(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), Config());
        Assert.AreEqual(0, (await p.GetLauncherBackgroundDataAsync(""))!.FunctionSwitch);
        Assert.AreEqual(0, h.Requests.Count);
    }

    [TestMethod]
    public async Task BundleConfigurationAndContentRetryBackupHost()
    {
        using var h = new Handler { FailPrimary = true }; using var client = new HttpClient(h);
        var config = Config(); config.BunleBackUpConfigUrl = "https://backup.example/config";
        config.BunleLauncherBackUpConfigUrl = "https://backup.example/app";
        var p = new BundleGameResourceProvider(client);
        p.SetConfig(new GameLocalConfig(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), config);
        Assert.AreEqual("bg-code", (await p.GetDefaultLauncherValue())!.FunctionCode.Background);
        Assert.AreEqual("notice-text", (await p.GetLauncherStarterAsync())!.Guidance.Notice.Contents[0].TContent);
        Assert.AreEqual(2, h.Requests.Count(u => u.Host == "backup.example"));
    }

    [TestMethod]
    public async Task BundleDoesNotFallBackToLegacyConfigurationAddress()
    {
        using var h = new Handler(); using var client = new HttpClient(h);
        var config = Config(); config.BunleConfigUrl = null;
        var p = new BundleGameResourceProvider(client);
        p.SetConfig(new GameLocalConfig(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), config);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => p.GetDefaultLauncherValue());
        Assert.AreEqual(0, h.Requests.Count);
    }

    [TestMethod]
    public async Task LiveOfficialProviderReadsPatchIndexNewsAndBackground()
    {
        if (Environment.GetEnvironmentVariable("HAIYU_LIVE_PROVIDER_TEST") != "1")
            Assert.Inconclusive("显式开启联网 Provider 冒烟测试后运行。");
        var folder = Path.Combine(Path.GetTempPath(), "haiyu-live-provider-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var local = new GameLocalConfig(Path.Combine(folder, "settings.json"));
        await local.SaveConfigsAsync(new Dictionary<string, string> { [GameLocalSettingName.BunlePackVersion_Common] = "3.6.1", [GameLocalSettingName.BunlePackVersion_HD] = "3.6.1" });
        await local.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, folder);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var p = new BundleGameResourceProvider(client); p.SetConfig(local, KuroGameApiConfig.MainAPiConfig);
        try
        {
            var summary = await p.GetResourceSummaryAsync();
            Assert.AreEqual("3.7.0", summary.OfficialVersion);
            var plan = await p.GetUpdateGameResourceAsync();
            Assert.AreEqual(GameResourceAvailability.Ready, plan.Availability);
            Assert.IsTrue(plan.PatchResources.Any(x => x.IsSelected && x.IsGroup));
            Assert.AreEqual("3.6.1", plan.ResourcePacks.First(x => x.Name == "common").LocalVersion);
            Assert.IsTrue((await p.GetLauncherStarterAsync())!.Slideshow.Count > 0);
            var meta = await p.GetDefaultLauncherValue();
            Assert.IsFalse(string.IsNullOrWhiteSpace((await p.GetLauncherBackgroundDataAsync(meta!.FunctionCode.Background))!.BackgroundFile));
        }
        finally { File.Delete(local.SettingPath); }
    }

    private static KuroGameApiConfig Config() => new()
    {
        AppId = "10003", AppKey = "old-key", GameID = "G152", Language = "zh-Hans",
        ConfigUrl = "https://old.example/config", LauncherConfigUrl = "https://old.example/launcher-index",
        BunleAppKey = "resource-key", BunleLauncherAppKey = "launcher-key", BunleLauncherAppId = "10003",
        BunleServerCode = "official", BunleDefaultBundleName = "HD",
        BunleConfigUrl = "https://new.example/config", BunleLauncherConfigUrl = "https://new.example/app"
    };
    private sealed class Handler : HttpMessageHandler
    {
        public bool Gzip { get; set; }
        public bool GzipHeader { get; set; }
        public bool FailPrimary;
        public List<Uri> Requests = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); var uri = request.RequestUri!; Requests.Add(uri);
            if (FailPrimary && uri.Host == "new.example") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var body = uri.AbsolutePath.Contains("information/") ? News : uri.AbsolutePath.Contains("background/") ? Background : BundleConfig;
            HttpContent content = new StringContent(body, Encoding.UTF8, "application/json");
            if (Gzip)
            {
                using var output = new MemoryStream();
                using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
                    gzip.Write(Encoding.UTF8.GetBytes(body));
                content = new ByteArrayContent(output.ToArray());
                if (GzipHeader) content.Headers.ContentEncoding.Add("gzip");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
