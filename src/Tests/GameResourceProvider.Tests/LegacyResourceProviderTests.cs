using System.Net;
using System.Text;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Options;
using Waves.Core.Services.GameResourceProvider;

namespace Project.Test;

[TestClass]
public sealed class LegacyResourceProviderTests
{
    private const string Config = """
        {"default":{"version":"2","resourcesBasePath":"/full/","cdnList":[{"P":1,"url":"https://one.example"},{"P":2,"url":"https://two.example"}],"config":{"size":100,"unCompressSize":200,"indexFile":"/index","baseUrl":"/full/","patchConfig":[{"version":"1","size":20,"baseUrl":"/patch/","indexFile":"/patch-index"}]}},"predownloadSwitch":1,"predownload":{"version":"3","resourcesBasePath":"/next/","config":{"size":150,"unCompressSize":250,"indexFile":"/next-index","baseUrl":"/next/","patchConfig":[{"version":"1","size":30,"indexFile":"/next-patch","baseUrl":"/next-patch/"}]}}}
        """;
    private const string Index = """
        {"resource":[{"dest":"direct.bin","size":10,"md5":"a","fromFolder":"/shared/","chunkInfos":[{"start":0,"end":9,"md5":"b"}]},{"dest":"a.krzip","size":20,"md5":"zip"},{"dest":"a.krdiff","size":30,"md5":"patch"},{"dest":"a.krpdiff","size":40,"md5":"group"}],"zipInfos":[{"dest":"a.krzip","entries":[{"dest":"out.bin","size":50}]}],"patchInfos":[{"dest":"a.krdiff","entries":[{"dest":"patched.bin","size":60}]}],"groupInfos":[{"dest":"a.krpdiff","srcFiles":[{"dest":"old.bin"}],"dstFiles":[{"dest":"new.bin"}]}],"deleteFiles":["removed.bin"]}
        """;

    [TestMethod]
    public async Task SummaryRequestsOnlyConfiguration()
    {
        using var fixture = await Fixture.Create("1");
        var summary = await fixture.Provider.GetResourceSummaryAsync(new GameResourceParameter { BundleName = "ignored" });
        Assert.AreEqual("2", summary.OfficialVersion);
        Assert.AreEqual("3", summary.PredownloadVersion);
        Assert.AreEqual(20L, summary.Update.DownloadSize);
        Assert.AreEqual(30L, summary.Predownload.DownloadSize);
        Assert.AreEqual(1, fixture.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task MissingPatchDoesNotRequestIndexOrWriteVersion()
    {
        using var fixture = await Fixture.Create("unknown");
        Assert.AreEqual(GameResourceAvailability.MissingPatch, (await fixture.Provider.GetUpdateGameResourceAsync()).Availability);
        Assert.AreEqual(GameResourceAvailability.MissingPatch, (await fixture.Provider.GetGameProdownloadResourceAsync()).Availability);
        Assert.AreEqual(2, fixture.Handler.Requests.Count);
        Assert.AreEqual("unknown", await fixture.Local.GetConfigAsync(GameLocalSettingName.LocalGameVersion));
    }

    [TestMethod]
    public async Task ContainersAreNotDuplicatedAndKeepDownloadMetadata()
    {
        using var fixture = await Fixture.Create("1");
        var plan = await fixture.Provider.GetUpdateGameResourceAsync();
        Assert.AreEqual(1, plan.DefaultResource.Count);
        Assert.AreEqual(1, plan.ZipResources.Count);
        Assert.AreEqual(2, plan.PatchResources.Count);
        Assert.AreEqual("https://one.example/shared/direct.bin", plan.DefaultResource[0].Url);
        Assert.AreEqual(2, plan.DefaultResource[0].UrlCandidates.Count);
        Assert.AreEqual(1, plan.DefaultResource[0].Chunks.Count);
        Assert.AreEqual(20L, plan.ZipResources[0].Size);
        Assert.AreEqual("zip", plan.ZipResources[0].Hash);
        Assert.AreEqual("https://one.example/patch/a.krzip", plan.ZipResources[0].Url);
        Assert.AreEqual(1, plan.ZipResources[0].Entries.Count);
        Assert.IsTrue(plan.PatchResources.Single(x => x.IsGroup).SrcFiles.Count == 1);
        Assert.AreEqual("removed.bin", plan.DeleteFiles.Single());
    }

    [TestMethod]
    public async Task AlreadyCurrentHasExplicitStateAndNoIndexRequest()
    {
        using var fixture = await Fixture.Create("2");
        Assert.AreEqual(GameResourceAvailability.AlreadyCurrent, (await fixture.Provider.GetUpdateGameResourceAsync()).Availability);
        Assert.AreEqual(1, fixture.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task NoPredownloadIsExplicitAndDoesNotFetchIndex()
    {
        using var fixture = await Fixture.Create("1");
        fixture.Handler.Configuration = "{\"default\":{\"version\":\"2\",\"cdnList\":[],\"config\":{}}}";
        Assert.AreEqual(GameResourceAvailability.NoPredownload, (await fixture.Provider.GetGameProdownloadResourceAsync()).Availability);
        Assert.AreEqual(1, fixture.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task GroupApplyRetainsBothDescriptionsAndSelectsOnlyOneCarrierSet()
    {
        using var fixture = await Fixture.Create("1");
        fixture.Provider.ApplyMethod = "group";
        fixture.Handler.FileIndex = """
            {"applyTypes":["group"],"resource":[{"dest":"regular.krdiff","size":30}],"groupResource":[{"dest":"group.krpdiff","size":40}],"patchInfos":[{"dest":"regular.krdiff","entries":[{"dest":"out.bin","size":10}]}],"groupInfos":[{"dest":"group.krpdiff","srcFiles":[{"dest":"old.bin"}],"dstFiles":[{"dest":"new.bin"}]}]}
            """;
        var plan = await fixture.Provider.GetUpdateGameResourceAsync();
        Assert.AreEqual(2, plan.PatchResources.Count);
        Assert.AreEqual(1, plan.PatchResources.Count(x => x.IsSelected));
        Assert.IsTrue(plan.PatchResources.Single(x => x.IsSelected).IsGroup);
        Assert.AreEqual(1, plan.PatchResources.Single(x => !x.IsSelected).Entries.Count);
    }

    [TestMethod]
    public async Task VerificationUsesPredownloadTarget()
    {
        using var fixture = await Fixture.Create("1");
        var plan = await fixture.Provider.GetVerificationResourceAsync("3");
        Assert.AreEqual("3", plan.NewGameVersion);
        Assert.AreEqual("/next-index", fixture.Handler.Requests.Last().AbsolutePath);
        Assert.AreEqual("1", await fixture.Local.GetConfigAsync(GameLocalSettingName.LocalGameVersion));
    }

    [TestMethod]
    public void DownloadFolderIsSharedAcrossDownloadAndInstallationModes()
    {
        var game = Path.Combine(Path.GetTempPath(), "game");
        var cache = Path.Combine(Path.GetTempPath(), "selected-cache");
        foreach (var option in new[] { InstallOption.CreateDefault(), InstallOption.CreateProdownlad(), InstallOption.CreateAdvance() })
        {
            option.DownloadFolder = cache;
            Assert.AreEqual(Path.GetFullPath(cache), option.ResolveDownloadFolder(game));
        }
        Assert.AreEqual(Path.Combine(game, "Diff"), InstallOption.CreateDefault().ResolveDownloadFolder(game));
        Assert.AreEqual(Path.Combine(game, "prodDownloads"), InstallOption.CreateAdvance().ResolveDownloadFolder(game));
    }

    [TestMethod]
    public void SelectedRootSeparatesUpdateAndPredownloadCaches()
    {
        var root = Path.Combine(Path.GetTempPath(), "selected-root");
        var update = InstallOption.BuildCacheFolder(root, false);
        var predownload = InstallOption.BuildCacheFolder(root, true);
        Assert.AreEqual(Path.Combine(root, "Diff"), update);
        Assert.AreEqual(Path.Combine(root, "prodDownloads"), predownload);
        Assert.AreEqual(predownload, new InstallOption { IsProd = true, DownloadFolder = predownload }.ResolveDownloadFolder(root));
    }
    [TestMethod]
    public void CacheFolderRejectsGameRootAndDiskRoot()
    {
        var game = Path.Combine(Path.GetTempPath(), "game");
        Assert.ThrowsException<ArgumentException>(() => new InstallOption { DownloadFolder = game }.ResolveDownloadFolder(game));
        Assert.ThrowsException<ArgumentException>(() => new InstallOption { DownloadFolder = Path.GetPathRoot(game) }.ResolveDownloadFolder(game));
    }

    [TestMethod]
    public async Task SuccessfulAdvanceInstallWritesExplicitTargetVersion()
    {
        using var fixture = await Fixture.Create("1");
        var writer = new global::Waves.Core.GameContext.KruoGameContextBaseV2.Common.WriteGameResourceConfig(
            fixture.Local, "3", fixture.Provider.ApiConfig, new global::Waves.Core.Services.LoggerService());
        await writer.WriteDownloadAndUpDateResultAsync(InstallOption.CreateAdvance());
        Assert.AreEqual("3", await fixture.Local.GetConfigAsync(GameLocalSettingName.LocalGameVersion));
        Assert.AreEqual("True", await fixture.Local.GetConfigAsync(GameLocalSettingName.ProdIsAdvance));
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string Configuration { get; set; } = Config;
        public string FileIndex { get; set; } = Index;
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath == "/config" ? Configuration : FileIndex, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Fixture : IDisposable
    {
        public Handler Handler { get; } = new();
        public GameLocalConfig Local { get; } = new(Path.Combine(Path.GetTempPath(), $"haiyu-resource-{Guid.NewGuid():N}.json"));
        public LegacyGameResourceProvider Provider { get; private set; } = null!;
        private HttpClient Client { get; set; } = null!;
        public static async Task<Fixture> Create(string version)
        {
            var fixture = new Fixture();
            fixture.Client = new HttpClient(fixture.Handler);
            fixture.Provider = new LegacyGameResourceProvider(fixture.Client);
            fixture.Provider.SetConfig(fixture.Local, new KuroGameApiConfig { ConfigUrl = "https://config.example/config", GameExeName = "game.exe" });
            await fixture.Local.SaveConfigAsync(GameLocalSettingName.LocalGameVersion, version);
            await fixture.Local.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, Path.GetTempPath());
            return fixture;
        }
        public void Dispose()
        {
            Client.Dispose();
            if (File.Exists(Local.SettingPath)) File.Delete(Local.SettingPath);
        }
    }
}
