using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Waves.Core.Common;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;
using Waves.Core.Models.Options;
using Waves.Core.Services;
using Waves.Core.Services.GameResourceProvider;

namespace Project.Test;

[TestClass]
public sealed class ResourceExecutionTests
{
    [TestMethod]
    public async Task CompletionResetsProgressAndRejectsLateCallbacks()
    {
        var tracker = new GameProgressTracker();
        await tracker.HandleEventAsync(new() { Generation = 1, Type = GameContextActionType.Verify,
            IsAction = true, TotalSize = 100, CurrentSize = 100, IsStepUpdate = true,
            AllSteps = ["校验", "保存"], StepName = "校验", TotalSteps = 2 });
        await tracker.HandleEventAsync(new() { Generation = 1, Type = GameContextActionType.None, Prod = true });
        foreach (var generation in new long[] { 0, 1 })
            await tracker.HandleEventAsync(new() { Generation = generation, Type = GameContextActionType.Verify, IsAction = true, TotalSize = 100 });
        Assert.AreEqual(GameContextActionType.None, tracker.CurrentAction);
        Assert.IsFalse(tracker.IsActive);
        Assert.AreEqual(0, tracker.AllSteps.Count);
        Assert.IsTrue(tracker.Prod);
        await tracker.HandleEventAsync(new() { Generation = 2, Type = GameContextActionType.Download, IsAction = true, TotalSize = 100 });
        Assert.IsTrue(tracker.IsActive);
        await tracker.HandleEventAsync(new() { Generation = 1, Type = GameContextActionType.None });
        await tracker.HandleEventAsync(new() { Generation = 1, Type = GameContextActionType.Verify });
        Assert.AreEqual(GameContextActionType.Download, tracker.CurrentAction);
        Assert.IsTrue(tracker.IsActive);
    }

    [TestMethod]
    public async Task FullInstallationConsumesUnifiedZipPlanAndVerifiesExtractedFiles()
    {
        await using var fixture = await Fixture.Create();
        fixture.Handler.ZipFullInstall = true;
        Assert.IsTrue(await fixture.Context.StartDownloadTaskAsync(fixture.Game));
        await fixture.WaitUntil(() => fixture.VersionIs("2"));
        await fixture.WaitUntil(() => Task.FromResult(!fixture.Context.IsResourceOperationActive));
        CollectionAssert.AreEqual(Handler.Payload, await File.ReadAllBytesAsync(Path.Combine(fixture.Game, "game.bin")));
    }

    [TestMethod]
    public async Task RepairUsesFullTargetManifest()
    {
        await using var fixture = await Fixture.Create();
        Assert.IsTrue(await fixture.Context.RepairGameAsync(isDelete: false));
        Assert.IsTrue(await fixture.VersionIs("2"));
        Assert.IsTrue(fixture.Handler.Paths.Contains("/full-index"));
        Assert.IsFalse(fixture.Handler.Paths.Contains("/patch-index"));
        await fixture.WaitUntil(() => Task.FromResult(fixture.Context.ProgressState.LastArgs.Type == GameContextActionType.None));
        Assert.IsNull(fixture.Context.DownloadState);
        Assert.IsFalse(fixture.Context.IsResourceOperationActive);
        Assert.IsFalse(fixture.Context.ProgressState.IsActive);
        Assert.AreEqual(0, fixture.Context.ProgressState.AllSteps.Count);
    }

    [TestMethod]
    public async Task RepairCanRunAgainAfterCompletion()
    {
        await using var fixture = await Fixture.Create();
        for (var i = 0; i < 2; i++)
        {
            Assert.IsTrue(await fixture.Context.RepairGameAsync(isDelete: false));
            await fixture.WaitUntil(() => Task.FromResult(fixture.Context.ProgressState.LastArgs.Type == GameContextActionType.None));
            Assert.IsFalse(fixture.Context.IsResourceOperationActive);
            Assert.IsNull(fixture.Context.DownloadState);
            Assert.IsFalse(fixture.Context.ProgressState.IsActive);
        }
    }

    [TestMethod]
    public async Task CancelledRepairClearsUpdateFlagWithoutCommittingVersion()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Context.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");
        fixture.Handler.DelayDownloads = true;
        var repair = fixture.Context.RepairGameAsync(isDelete: false);
        await fixture.Handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.IsTrue(await fixture.Context.StopCannelTaskAsync());
        Assert.IsFalse(await repair);
        Assert.AreEqual("False", await fixture.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameUpdateing));
        Assert.IsTrue(await fixture.VersionIs("1"));
        Assert.IsNull(fixture.Context.DownloadState);
        Assert.IsFalse(fixture.Context.IsResourceOperationActive);
    }

    [TestMethod]
    public async Task ExistingCacheFilesAreVerifiedAndReusedDuringUpdate()
    {
        await using var fixture = await Fixture.Create();
        var cached = Path.Combine(fixture.Cache, "resources", "game.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        await File.WriteAllBytesAsync(cached, Handler.Payload);
        var unrelated = Path.Combine(fixture.Cache, "keep.txt");
        await File.WriteAllTextAsync(unrelated, "keep");
        Assert.IsTrue(await fixture.Context.UpdateGameResourceAsync(fixture.Cache));
        await fixture.WaitUntil(() => fixture.VersionIs("2"));
        await fixture.WaitUntil(() => Task.FromResult(fixture.Context.DownloadState is null && !fixture.Context.IsResourceOperationActive));
        Assert.AreEqual(0, fixture.Handler.FileDownloads);
        Assert.IsTrue(File.Exists(unrelated));
        CollectionAssert.AreEqual(Handler.Payload, await File.ReadAllBytesAsync(Path.Combine(fixture.Game, "game.bin")));
    }

    [TestMethod]
    public async Task PredownloadAndStandaloneInstallShareSelectedCache()
    {
        await using var fixture = await Fixture.Create();
        Assert.IsTrue(await fixture.Context.StartProdDownloadGameResourceAsync(fixture.Cache));
        await fixture.WaitUntil(async () => await fixture.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadFolderDone) == "True");
        await fixture.WaitUntil(() => Task.FromResult(fixture.Context.ProdDownloadState is null && !fixture.Context.IsResourceOperationActive));
        Assert.IsTrue(await fixture.VersionIs("1"));
        Assert.AreEqual(fixture.Cache, await fixture.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadPath));
        await fixture.WaitUntil(() => Task.FromResult(fixture.Context.ProgressState.LastArgs.Type == GameContextActionType.None));
        Assert.IsTrue(fixture.Context.ProgressState.LastArgs.Prod);
        Assert.IsFalse(fixture.Context.ProgressState.IsActive);
        fixture.Handler.OfficialVersion = "3";
        fixture.Handler.HasPredownload = false;
        await fixture.Context.StartInstallGameResource(InstallOption.CreateProdownlad());
        Assert.IsTrue(await fixture.VersionIs("3"));
        Assert.IsTrue(File.Exists(Path.Combine(fixture.Game, "game.bin")));
    }

    [TestMethod]
    public async Task AdvanceInstallVerifiesPredownloadTargetInsteadOfOfficialTarget()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Context.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameVersion, "2");
        Assert.IsTrue(await fixture.Context.StartProdDownloadGameResourceAsync(fixture.Cache));
        await fixture.WaitUntil(async () => await fixture.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadFolderDone) == "True");
        await fixture.WaitUntil(() => Task.FromResult(fixture.Context.ProdDownloadState is null && !fixture.Context.IsResourceOperationActive));
        await fixture.Context.AdvanceInstallGameResourceAsync();
        await fixture.WaitUntil(() => fixture.VersionIs("3"));
        Assert.IsTrue(fixture.Handler.Paths.Contains("/next-index"));
        Assert.IsFalse(fixture.Handler.Paths.Contains("/full-index"));
    }

    [TestMethod]
    public async Task FinalVerificationFailureDoesNotWriteTargetVersion()
    {
        await using var fixture = await Fixture.Create();
        fixture.Handler.InvalidFinalHash = true;
        fixture.Handler.DeleteObsolete = true;
        await File.WriteAllTextAsync(Path.Combine(fixture.Game, "obsolete.bin"), "old");
        Assert.IsTrue(await fixture.Context.UpdateGameResourceAsync(fixture.Cache));
        await fixture.WaitUntil(() => Task.FromResult(fixture.Handler.Paths.Contains("/full-index")));
        await fixture.WaitUntil(() => Task.FromResult(fixture.Context.DownloadState is null && !fixture.Context.IsResourceOperationActive));
        Assert.IsTrue(await fixture.VersionIs("1"));
        Assert.IsTrue(File.Exists(Path.Combine(fixture.Game, "obsolete.bin")));
    }

    [TestMethod]
    public async Task MissingPatchOrGroupCarrierDoesNotCompleteInstallation()
    {
        foreach (var isGroup in new[] { false, true })
        {
            await using var fixture = await Fixture.Create();
            var plan = new GameVersionInfo
            {
                OldGameVersion = "1", NewGameVersion = "2",
                PatchResources = [new PatchGameFileInfo { Dest = isGroup ? "missing.krpdiff" : "missing.krdiff", IsGroup = isGroup }]
            };
            await fixture.Context.StartInstallGameResource(plan, new InstallOption { DownloadFolder = fixture.Cache });
            Assert.IsTrue(await fixture.VersionIs("1"));
        }
    }

    [TestMethod]
    public async Task MissingPatchStopsBeforeIndexRequestsAndStateChanges()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Context.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameVersion, "unknown");
        Assert.IsFalse(await fixture.Context.UpdateGameResourceAsync(fixture.Cache));
        Assert.IsFalse(await fixture.Context.StartProdDownloadGameResourceAsync(fixture.Cache));
        Assert.IsTrue(fixture.Handler.Paths.All(x => x == "/config"));
        Assert.IsNull(fixture.Context.DownloadState);
        Assert.IsNull(fixture.Context.ProdDownloadState);
        Assert.IsTrue(await fixture.VersionIs("unknown"));
    }

    [TestMethod]
    public async Task CancellationAndDuplicateInvocationDoNotCompleteOperation()
    {
        await using var fixture = await Fixture.Create();
        fixture.Handler.DelayDownloads = true;
        Assert.IsTrue(await fixture.Context.UpdateGameResourceAsync(fixture.Cache));
        await fixture.Handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.IsFalse(await fixture.Context.UpdateGameResourceAsync(fixture.Cache));
        Assert.IsTrue(await fixture.Context.PauseDownloadAsync());
        Assert.IsTrue(fixture.Context.DownloadState!.IsPaused);
        Assert.IsTrue(await fixture.Context.ResumeDownloadAsync());
        Assert.IsFalse(fixture.Context.DownloadState.IsPaused);
        Assert.IsTrue(await fixture.Context.StopCannelTaskAsync());
        await fixture.WaitUntil(() => Task.FromResult(fixture.Context.DownloadState is null && !fixture.Context.IsResourceOperationActive));
        Assert.IsTrue(await fixture.VersionIs("1"));
    }

    [TestMethod]
    public void BundleRouteNeverCreatesLegacyProvider()
    {
        Assert.IsInstanceOfType<BundleGameResourceProvider>(new BundleContext().GameResourceProvider);
        Assert.IsInstanceOfType<LegacyGameResourceProvider>(new Context().GameResourceProvider);
    }

    [TestMethod]
    public void UnifiedManifestUsesSourceGeneratedSerialization()
    {
        Assert.IsFalse(JsonSerializer.IsReflectionEnabledByDefault);
        var plan = new GameVersionInfo { NewGameVersion = "3", DefaultResource = [new GameFileInfo { Dest = "game.bin", Chunks = [new GameFileChunkInfo { Start = 0, End = 9, Hash = "hash" }] }] };
        var json = JsonSerializer.Serialize(plan, GameResourceJsonContext.Default.GameVersionInfo);
        var decoded = JsonSerializer.Deserialize(json, GameResourceJsonContext.Default.GameVersionInfo)!;
        Assert.AreEqual("hash", decoded.DefaultResource[0].Chunks[0].Hash);
    }

    private class Context : KuroGameContextBaseV2
    {
        public Context() : base(new KuroGameApiConfig { ConfigUrl = "https://fixture.example/config", GameExeName = "game.bin" }, "fixture", "fixture", new Breaker()) { }
        public override bool IsBunle => false;
        public override string GameContextNameKey => "fixture";
        public override GameType GameType => GameType.Punish;
        public override Type ContextType => GetType();
    }
    private sealed class BundleContext : Context { public override bool IsBunle => true; }
    private sealed class Breaker : IIoCircuitBreaker { public bool TryAcquire() => true; public void Release() { } }
    private sealed class Service(HttpClient client) : IHttpClientService
    {
        public HttpClient HttpClient => client;
        public HttpClient GameDownloadClient => client;
        public void BuildClient() { }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public static readonly byte[] Payload = Encoding.UTF8.GetBytes("target-data");
        private static readonly string Hash = Convert.ToHexString(MD5.HashData(Payload));
        public ConcurrentBag<string> Paths { get; } = [];
        public string OfficialVersion { get; set; } = "2";
        public bool HasPredownload { get; set; } = true;
        public bool InvalidFinalHash { get; set; }
        public bool DelayDownloads { get; set; }
        public bool ZipFullInstall { get; set; }
        public bool DeleteObsolete { get; set; }
        private static readonly byte[] ZipPayload = CreateZip();
        public TaskCompletionSource DownloadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int FileDownloads;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            if (path == "/config")
            {
                var pre = HasPredownload ? ",\"predownload\":{\"version\":\"3\",\"resourcesBasePath\":\"/next/\",\"config\":{\"size\":11,\"unCompressSize\":11,\"baseUrl\":\"/next/\",\"indexFile\":\"/next-index\",\"patchConfig\":[{\"version\":\"1\",\"size\":11,\"baseUrl\":\"/diff/\",\"indexFile\":\"/next-patch\"},{\"version\":\"2\",\"size\":11,\"baseUrl\":\"/diff/\",\"indexFile\":\"/next-patch\"}]}}" : "";
                return Json("{\"default\":{\"version\":\"" + OfficialVersion + "\",\"resourcesBasePath\":\"/full/\",\"cdnList\":[{\"P\":1,\"K1\":1,\"url\":\"https://fixture.example\"}],\"config\":{\"size\":11,\"unCompressSize\":11,\"baseUrl\":\"/full/\",\"indexFile\":\"/full-index\",\"patchConfig\":[{\"version\":\"1\",\"size\":11,\"baseUrl\":\"/diff/\",\"indexFile\":\"/patch-index\"}]}},\"predownloadSwitch\":1" + pre + "}");
            }
            if (ZipFullInstall && path == "/full-index")
                return Json("{\"resource\":[{\"dest\":\"game.krzip\",\"size\":" + ZipPayload.Length + ",\"md5\":\"" + Convert.ToHexString(MD5.HashData(ZipPayload)) + "\"}],\"zipInfos\":[{\"dest\":\"game.krzip\",\"entries\":[{\"dest\":\"game.bin\",\"size\":11,\"md5\":\"" + Hash + "\"}]}]}");
            if (!path.EndsWith("game.bin") && !path.EndsWith("game.krzip"))
            {
                var hash = InvalidFinalHash && path == "/full-index" ? "bad-hash" : Hash;
                return Json("{\"resource\":[{\"dest\":\"game.bin\",\"md5\":\"" + hash + "\",\"size\":11}],\"deleteFiles\":" + (DeleteObsolete ? "[\"obsolete.bin\"]" : "[]") + "}");
            }
            var range = request.Headers.Range?.Ranges.Single();
            var payload = path.EndsWith("game.krzip") ? ZipPayload : Payload;
            var from = range?.From ?? 0;
            var requestedEnd = range?.To ?? payload.Length - 1;
            if (requestedEnd < 2 * 1024 * 1024 - 1)
            {
                Interlocked.Increment(ref FileDownloads);
                DownloadStarted.TrySetResult();
                if (DelayDownloads) await Task.Delay(Timeout.Infinite, token);
            }
            var end = Math.Min(requestedEnd, payload.Length - 1);
            var content = new ByteArrayContent(payload[(int)from..((int)end + 1)]);
            content.Headers.ContentRange = new ContentRangeHeaderValue(from, end, payload.Length);
            return new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content };
        }
        private static byte[] CreateZip()
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            using (var entry = archive.CreateEntry("game.bin").Open()) entry.Write(Payload);
            return stream.ToArray();
        }
        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Context Context { get; } = new();
        public Handler Handler { get; } = new();
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "haiyu-execution-" + Guid.NewGuid().ToString("N"));
        public string Game => Path.Combine(Root, "game");
        public string Cache => Path.Combine(Root, "cache");
        private HttpClient Client = null!;
        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            Directory.CreateDirectory(fixture.Game);
            fixture.Client = new HttpClient(fixture.Handler);
            fixture.Context.HttpClientService = new Service(fixture.Client);
            fixture.Context.GameEventPublisher = new GameEventPublisher();
            fixture.Context.SystemEventPublisher = new SystemEventPublisher();
            fixture.Context.GamerConfigPath = Path.Combine(fixture.Root, "settings");
            await fixture.Context.InitAsync();
            fixture.Context.CDNSpeedTester._client.Dispose();
            fixture.Context.CDNSpeedTester._client = fixture.Client;
            await fixture.Context.GameLocalConfig.SaveConfigsAsync(new Dictionary<string, string>
            {
                [GameLocalSettingName.GameLauncherBassFolder] = fixture.Game,
                [GameLocalSettingName.LocalGameVersion] = "1"
            });
            return fixture;
        }
        public async Task<bool> VersionIs(string version) => await Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion) == version;
        public async Task WaitUntil(Func<Task<bool>> predicate)
        {
            var timeout = Stopwatch.StartNew();
            while (!await predicate())
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(10)) Assert.Fail("资源任务未在预期时间完成。");
                await Task.Delay(20);
            }
        }
        public async ValueTask DisposeAsync()
        {
            await Context.StopCannelTaskAsync();
            await WaitUntil(() => Task.FromResult(!Context.IsResourceOperationActive));
            await Context.ProgressState.DisposeAsync();
            await ((GameEventPublisher)Context.GameEventPublisher).DisposeAsync();
            await Context.SystemEventPublisher.DisposeAsync();
            ((IDisposable)Context.Logger.ILogger).Dispose();
            Client.Dispose();
            // 临时目录保留在测试输出中；不影响用户选择的任何目录。
        }
    }
}
