using Waves.Core.Contracts;
using Waves.Core.Services;
using Waves.Core.Services.GameResourceProvider;

namespace Project.Test;

[TestClass]
public sealed class HttpClientLifetimeTests
{
    [TestMethod]
    public void RepeatedBuildPreservesClientsAndActiveRequests()
    {
        var service = new HttpClientService();
        service.BuildClient();
        var configClient = service.HttpClient;
        var downloadClient = service.GameDownloadClient;
        Parallel.For(0, 20, _ => service.BuildClient());
        Assert.AreSame(configClient, service.HttpClient);
        Assert.AreSame(downloadClient, service.GameDownloadClient);
        // 已释放的客户端在设置 Timeout 时抛 ObjectDisposedException。
        configClient.Timeout = TimeSpan.FromSeconds(30);
        downloadClient.Timeout = Timeout.InfiniteTimeSpan;
        configClient.Dispose();
        downloadClient.Dispose();
    }

    [TestMethod]
    public async Task ProviderUsesCurrentServiceClientAfterReplacement()
    {
        using var service = new ReplaceableService();
        var provider = new LegacyGameResourceProvider(service);
        provider.SetConfig(new Waves.Core.Models.GameLocalConfig(
            Path.Combine(Path.GetTempPath(), $"client-lifetime-{Guid.NewGuid():N}.json")),
            new Waves.Core.Models.CoreApi.KuroGameApiConfig { ConfigUrl = "https://fixture.example/config" });
        service.BuildClient();
        await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(
            async () => await provider.CheckUpdateAsync());
        Assert.AreEqual(1, service.Requests);
    }

    private sealed class ReplaceableService : IHttpClientService, IDisposable
    {
        public int Requests;
        public HttpClient HttpClient { get; private set; }
        public HttpClient GameDownloadClient => HttpClient;
        public ReplaceableService() => HttpClient = Create();
        private HttpClient Create() => new(new Handler(this));
        public void BuildClient() { HttpClient.Dispose(); HttpClient = Create(); }
        public void Dispose() => HttpClient.Dispose();
        private sealed class Handler(ReplaceableService owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.Requests++;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("invalid-json") });
            }
        }
    }
}
