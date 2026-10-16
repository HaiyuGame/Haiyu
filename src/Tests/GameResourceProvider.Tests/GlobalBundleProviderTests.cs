using System.Net;
using System.Text;
using Waves.Core.Contracts;
using Waves.Core.GameContext.ContextsV2.Waves;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Services;
using Waves.Core.Services.GameResourceProvider;

namespace Project.Test;

[TestClass]
public sealed class GlobalBundleProviderTests
{
    private const string NewKey = "P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e";

    [TestMethod]
    public async Task GlobalSnapshotSupportsAllBundlesAndKeepsLegacyConfiguration()
    {
        var config = KuroGameApiConfig.GlobalConfig;
        Assert.AreEqual("obOHXFrFanqsaIEOmuKroCcbZkQRBC7c", config.AppKey);
        Assert.IsTrue(config.ConfigUrl.Contains("/launcher/game/G153/50004_obOHX"));
        Assert.AreEqual(NewKey, config.BunleAppKey);
        Assert.AreEqual(NewKey, config.BunleLauncherAppKey);
        Assert.AreEqual("prod-volcdn-gamestarter.kurogame.net", new Uri(config.BunleBackUpConfigUrl!).Host);
        Assert.IsTrue(new WavesGlobalGameContextV2(config, "global", new Breaker()).IsBunle);
        using var handler = new SnapshotHandler();
        using var client = new HttpClient(handler);
        var provider = new BundleGameResourceProvider(client);
        provider.SetConfig(new GameLocalConfig(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), config);
        foreach (var bundle in new[] { "HD", "SD", "UHD" })
        {
            var summary = await provider.GetResourceSummaryAsync(new() { BundleName = bundle });
            Assert.AreEqual(bundle, summary.BundleName);
            Assert.AreEqual("3.7.0", summary.OfficialVersion);
            Assert.IsTrue(summary.Install.DownloadSize > 0);
            Assert.IsTrue(summary.Install.TargetSize > 0);
            CollectionAssert.AreEquivalent(new[] { "HD", "SD", "UHD" }, summary.AvailableBundles.ToArray());
        }
        Assert.AreEqual(3, handler.Requests.Count); // 摘要仅请求配置，没有索引下载。
        Assert.IsTrue(handler.Requests.All(u => u.AbsoluteUri == config.BunleConfigUrl));
        await provider.GetLauncherStarterAsync();
        Assert.AreEqual($"/launcher/app/50004_{NewKey}/G153/official/information/zh-Hant.json", handler.Requests.Last().AbsolutePath);
        var meta = await provider.GetDefaultLauncherValue();
        await provider.GetLauncherBackgroundDataAsync(meta!.FunctionCode.Background);
        Assert.AreEqual($"/launcher/game/50004_{NewKey}/G153/official/background/{meta.FunctionCode.Background}/zh-Hant.json", handler.Requests.Last().AbsolutePath);
    }

    [TestMethod]
    public async Task GlobalBootstrapMatchesHardcodedBundleEndpoints()
    {
        var config = KuroGameApiConfig.GlobalConfig;
        var root = Path.Combine(Path.GetTempPath(), "haiyu-global-bootstrap-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        var file = Path.Combine(root, "Assets", "KRApp.conf");
        // 2026-10-13 国际服 3.0.1.0 的配置字段摘录，不依赖安装目录。
        var json = """
            {"appId":"50004","appKey":"P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e","launcherConfigUrl":"https://prod-alicdn-gamestarter.kurogame.com/launcher/app/50004_P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e/index.json","launcherBackUpConfigUrl":"https://prod-volcdn-gamestarter.kurogame.net/launcher/app/50004_P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e/index.json","games":[{"gameId":"G153","resId":"50004","appKey":"P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e","serverCode":"official","configUrl":"https://prod-alicdn-gamestarter.kurogame.com/launcher/game/50004_P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e/G153/official/index.json","backUpConfigUrl":"https://prod-volcdn-gamestarter.kurogame.net/launcher/game/50004_P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e/G153/official/index.json","defaultBundleName":"HD"}]}
            """;
        var bytes = Encoding.UTF8.GetBytes(json);
        for (var i = 0; i < bytes.Length; i++) bytes[i] ^= 99;
        await File.WriteAllTextAsync(file, Convert.ToBase64String(bytes));
        try
        {
            var expected = KuroGameApiConfig.GlobalConfig;
            using var handler = new SnapshotHandler();
            using var client = new HttpClient(handler);
            var provider = new BundleGameResourceProvider(client);
            provider.SetConfig(new GameLocalConfig(Path.Combine(root, "settings.json")), config);
            await provider.ConfigureFromLauncherAsync(root);
            Assert.AreEqual(expected.BunleConfigUrl, config.BunleConfigUrl);
            Assert.AreEqual(expected.BunleBackUpConfigUrl, config.BunleBackUpConfigUrl);
            Assert.AreEqual(expected.BunleLauncherConfigUrl, config.BunleLauncherConfigUrl);
            Assert.AreEqual(expected.BunleLauncherBackUpConfigUrl, config.BunleLauncherBackUpConfigUrl);
            Assert.AreEqual(expected.BunleAppKey, config.BunleAppKey);
            Assert.AreEqual(expected.BunleLauncherAppId, config.BunleLauncherAppId);
            Assert.AreEqual(expected.BunleServerCode, config.BunleServerCode);
            Assert.AreEqual(expected.BunleDefaultBundleName, config.BunleDefaultBundleName);
            Assert.AreEqual(expected.AppKey, config.AppKey);
            Assert.AreEqual(expected.ConfigUrl, config.ConfigUrl);
        }
        finally { File.Delete(file); }
    }

    [TestMethod]
    public async Task LiveGlobalProviderReadsConfigurationIndexesNewsAndBackground()
    {
        if (Environment.GetEnvironmentVariable("HAIYU_LIVE_GLOBAL_PROVIDER_TEST") != "1")
            Assert.Inconclusive("显式开启国际服联网 Provider 测试后运行。");
        var root = Path.Combine(Path.GetTempPath(), "haiyu-global-live-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var local = new GameLocalConfig(Path.Combine(root, "settings.json"));
        await local.SaveConfigsAsync(new Dictionary<string, string>
        {
            [GameLocalSettingName.GameLauncherBassFolder] = root,
            [GameLocalSettingName.LocalGameVersion] = "3.6.1"
        });
        using var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
            { Timeout = TimeSpan.FromSeconds(60) };
        var provider = new BundleGameResourceProvider(client);
        provider.SetConfig(local, KuroGameApiConfig.GlobalConfig);
        try
        {
            var summary = await provider.GetResourceSummaryAsync();
            Assert.AreEqual("HD", summary.BundleName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.OfficialVersion));
            var install = await provider.GetInstallGameResourceAsync();
            Assert.AreEqual(summary.OfficialVersion, install.NewGameVersion);
            CollectionAssert.AreEquivalent(new[] { "common", "hd" }, install.ResourcePacks.Select(p => p.Name).ToArray());
            Assert.IsTrue(install.DefaultResource.Count + install.ZipResources.Count > 0);
            var update = await provider.GetUpdateGameResourceAsync();
            Assert.AreEqual(GameResourceAvailability.Ready, update.Availability);
            Assert.AreEqual(summary.OfficialVersion, update.NewGameVersion);
            Assert.IsTrue(update.DefaultResource.Count + update.ZipResources.Count + update.PatchResources.Count > 0);
            Assert.IsNotNull(await provider.GetLauncherStarterAsync());
            var meta = await provider.GetDefaultLauncherValue();
            Assert.IsFalse(string.IsNullOrWhiteSpace((await provider.GetLauncherBackgroundDataAsync(meta!.FunctionCode.Background))!.BackgroundFile));
            // 备用配置域名同样通过真正的 Provider 解析。
            var backup = KuroGameApiConfig.GlobalConfig;
            backup.BunleConfigUrl = backup.BunleBackUpConfigUrl;
            provider.SetConfig(local, backup);
            Assert.AreEqual(summary.OfficialVersion, (await provider.GetResourceSummaryAsync()).OfficialVersion);
        }
        finally { File.Delete(local.SettingPath); }
    }

    private sealed class Breaker : IIoCircuitBreaker
    {
        public bool TryAcquire() => true;
        public void Release() { }
    }

    private sealed class SnapshotHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var uri = request.RequestUri!;
            Requests.Add(uri);
            var body = uri.AbsolutePath.Contains("information/") ? "{\"guidance\":{},\"slideshow\":[]}"
                : uri.AbsolutePath.Contains("background/") ? "{\"functionSwitch\":1,\"backgroundFile\":\"https://assets.example/bg.mp4\"}"
                : await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OfficialGlobalBundleConfig.json"), token);
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
