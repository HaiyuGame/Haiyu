using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Waves.Core.Common;
using Waves.Core.Common.Downloads;
using Waves.Core.Contracts;
using Waves.Core.GameContext.KruoGameContextBaseV2.Common;
using Waves.Core.Models;
using Waves.Core.Services;

namespace Project.Test;

[TestClass]
public sealed class ResourceDownloadTests
{
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
