using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Waves.Core.Common;
using Waves.Core.Common.Downloads;
using Waves.Core.Contracts;
using Waves.Core.GameContext.KruoGameContextBaseV2.Common;
using Waves.Core.Models;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Project.Test;

[TestClass]
public sealed class ResourceDownloadTests
{
    [TestMethod]
    public async Task ZipEstimatesRemainingTimeAndResetsForNextArchive()
    {
        using var cts = new CancellationTokenSource();
        await using var events = new GameEventPublisher();
        var state = new DownloadState { CancelToken = cts, IsActive = true };
        var zip = new InstallKrZipResource(new LoggerService());
        zip.SetParam(new Dictionary<string, object>
        {
            ["zipInfos"] = new List<PatchGameFileInfo>(), ["baseGamePath"] = Path.GetTempPath(),
            ["zipDownFolder"] = Path.GetTempPath(), ["downloadState"] = state
        }, events);
        Assert.IsTrue(zip.Check());
        zip.InitZipProgress(1000);
        Assert.AreEqual("----", zip.UpdateFileProgress(Waves.Core.Models.Enums.GameContextActionType.ZipDecompress, 100).RemainingTimeText);
        await Task.Delay(1100);
        Assert.IsTrue(zip.UpdateFileProgress(Waves.Core.Models.Enums.GameContextActionType.Decompress, 100).RemainingTime > TimeSpan.Zero);
        await state.PauseAsync();
        Assert.IsNull(zip.UpdateFileProgress(Waves.Core.Models.Enums.GameContextActionType.ZipDecompress, 0).RemainingTime);
        await state.ResumeAsync();
        Assert.AreEqual(TimeSpan.Zero, zip.UpdateFileProgress(Waves.Core.Models.Enums.GameContextActionType.ZipDecompress, 800).RemainingTime);
        zip.InitZipProgress(500);
        Assert.AreEqual("----", zip.UpdateFileProgress(Waves.Core.Models.Enums.GameContextActionType.ZipDecompress, 100).RemainingTimeText);
        cts.Cancel();
        Assert.IsNull(zip.UpdateFileProgress(Waves.Core.Models.Enums.GameContextActionType.ZipDecompress, 0).RemainingTime);
    }

    [TestMethod]
    public async Task DownloadAndVerificationEstimateTimeAfterSpeedSampling()
    {
        foreach (var type in new[] { Waves.Core.Models.Enums.GameContextActionType.Download, Waves.Core.Models.Enums.GameContextActionType.Verify })
        {
            using var client = new HttpClient();
            using var cts = new CancellationTokenSource();
            await using var events = new GameEventPublisher();
            var state = new DownloadState { CancelToken = cts, IsActive = true };
            var action = new DownloadAndVerifyResource(new LoggerService());
            action.SetParam(new Dictionary<string, object>
            {
                ["resource"] = new[] { new GameFileInfo { Dest = "test.bin", Size = 1000 } },
                ["isDelete"] = false, ["folder"] = Path.GetTempPath(),
                ["httpClient"] = new Service(client), ["downloadState"] = state, ["isProd"] = false
            }, events);
            Assert.IsTrue(await action.CheckAsync());
            var isAdd = type == Waves.Core.Models.Enums.GameContextActionType.Download;
            Assert.AreEqual("----", action.UpdateFileProgress(type, 100, isAdd).RemainingTimeText);
            await Task.Delay(1100);
            var sampled = action.UpdateFileProgress(type, 100, isAdd);
            Assert.IsTrue(sampled.RemainingTime > TimeSpan.Zero);
            Assert.AreEqual(isAdd ? 200L : 0L, sampled.CurrentSize);
            await state.PauseAsync();
            Assert.IsNull(action.UpdateFileProgress(type, 0, isAdd).RemainingTime);
            await state.ResumeAsync();
            var completed = action.UpdateFileProgress(Waves.Core.Models.Enums.GameContextActionType.Verify, isAdd ? 800 : 1000, true);
            Assert.AreEqual(TimeSpan.Zero, completed.RemainingTime);
            cts.Cancel();
            Assert.IsNull(action.UpdateFileProgress(type, 0, isAdd).RemainingTime);
        }
    }

    [TestMethod]
    public async Task ChunkValidationReadsOnlyRequestedRange()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "helloWORLD");
            using var cts = new CancellationTokenSource();
            var chunk = new GameFileChunkInfo { Start = 0, End = 4, Hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("hello"))) };
            Assert.IsFalse(await VerifyTask.ValidateFileChunks(chunk, path, downloadCts: cts));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public async Task CompleteChunkHashesDoNotTriggerAnotherWholeFileHashScan()
    {
        var folder = Path.Combine(Path.GetTempPath(), "haiyu-chunk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var payload = Encoding.UTF8.GetBytes("good");
        await File.WriteAllBytesAsync(Path.Combine(folder, "data.bin"), payload);
        var handler = new Handler();
        using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        var logger = new LoggerService();
        logger.InitLogger(Path.Combine(folder, "log.txt"), Serilog.RollingInterval.Infinite);
        await using var events = new GameEventPublisher();
        var file = new GameFileInfo
        {
            Dest = "data.bin", Size = 4,
            // Full coverage must be validated by the chunk hashes, not this extra whole-file field.
            Hash = "not-used-by-chunk-verification",
            Chunks =
            [
                new() { Start = 0, End = 1, Hash = Convert.ToHexString(MD5.HashData(payload[..2])) },
                new() { Start = 2, End = 3, Hash = Convert.ToHexString(MD5.HashData(payload[2..])) }
            ]
        };
        var action = new DownloadAndVerifyResource(logger);
        action.SetParam(new Dictionary<string, object>
        {
            ["resource"] = new[] { file }, ["isDelete"] = false, ["folder"] = folder,
            ["httpClient"] = new Service(client), ["downloadState"] = new DownloadState { CancelToken = cts },
            ["isProd"] = false, ["fastVerify"] = false
        }, events);
        try
        {
            Assert.IsTrue(await action.ExecuteAsync(true) is true);
            Assert.AreEqual(0, handler.Hosts.Count);
            var final = action.UpdateFileProgress(GameContextActionType.Verify, 0, false);
            Assert.AreEqual(4L, final.CurrentSize);
            Assert.AreEqual(4L, final.TotalSize);
            Assert.AreEqual(1, final.FileTotal);
        }
        finally { ((IDisposable)logger.ILogger).Dispose(); }
    }

    [TestMethod]
    public async Task VerificationBatchesReadNotificationsInsteadOfPostingPerRead()
    {
        var path = Path.GetTempFileName();
        try
        {
            var payload = new byte[3 * 1024 * 1024 + 17];
            await File.WriteAllBytesAsync(path, payload);
            using var cts = new CancellationTokenSource();
            var progress = new ReadProgress();
            Assert.IsFalse(await VerifyTask.ValidateGameFileAsync(Convert.ToHexString(MD5.HashData(payload)),
                path, new DownloadState { CancelToken = cts }, cts, progress));
            Assert.AreEqual((long)payload.Length, progress.Reports.Sum(x => x.Item3));
            Assert.IsTrue(progress.Reports.Count <= 4);
            Assert.AreEqual((long)payload.Length, progress.Reports[^1].Item5);
        }
        finally { File.Delete(path); }
    }

    private sealed class ReadProgress : IProgress<(GameContextActionType, bool, long, string, long, long)>
    {
        public List<(GameContextActionType, bool, long, string, long, long)> Reports { get; } = [];
        public void Report((GameContextActionType, bool, long, string, long, long) value) => Reports.Add(value);
    }

    [TestMethod]
    public async Task HashMismatchRetriesNextCdnInsteadOfCompleting()
    {
        var folder = Path.Combine(Path.GetTempPath(), "haiyu-cdn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var handler = new Handler();
        using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        var logger = new LoggerService();
        logger.InitLogger(Path.Combine(folder, "log.txt"), Serilog.RollingInterval.Infinite);
        await using var events = new GameEventPublisher();
        var payload = Encoding.UTF8.GetBytes("good");
        var file = new GameFileInfo
        {
            Dest = "data.bin", Size = 4, Hash = Convert.ToHexString(MD5.HashData(payload)),
            Url = "https://first.example/data.bin", UrlCandidates = ["https://first.example/data.bin", "https://second.example/data.bin"]
        };
        var download = new DownloadAndVerifyResource(logger);
        download.SetParam(new Dictionary<string, object>
        {
            ["resource"] = new[] { file }, ["isDelete"] = false, ["folder"] = folder,
            ["httpClient"] = new Service(client), ["downloadState"] = new DownloadState { CancelToken = cts }, ["isProd"] = false
        }, events);
        Assert.IsTrue(await download.ExecuteAsync(true) is true);
        CollectionAssert.AreEqual(payload, await File.ReadAllBytesAsync(Path.Combine(folder, file.Dest)));
        CollectionAssert.AreEqual(new[] { "first.example", "second.example" }, handler.Hosts);
        Assert.AreEqual(4L, download.UpdateFileProgress(GameContextActionType.Verify, 0, false).CurrentSize);
        ((IDisposable)logger.ILogger).Dispose();
    }

    private sealed class Service(HttpClient client) : IHttpClientService
    {
        public HttpClient HttpClient => client;
        public HttpClient GameDownloadClient => client;
        public void BuildClient() { }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Hosts { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Hosts.Add(request.RequestUri!.Host);
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(request.RequestUri.Host == "first.example" ? "bad!" : "good"));
            content.Headers.ContentRange = new ContentRangeHeaderValue(0, 3, 4);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content });
        }
    }
}
