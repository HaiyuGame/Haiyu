namespace Waves.Core.Services.GameResourceProvider;

/// <summary>旧版单资源包协议的请求和统一资源适配。</summary>
public sealed class LegacyGameResourceProvider : IGameResourceProvider
{
    private static readonly HttpClient DefaultClient = new();
    private readonly HttpClient _httpClient;

    public LegacyGameResourceProvider() : this(DefaultClient) { }

    public LegacyGameResourceProvider(IHttpClientService service) : this(service.HttpClient) { }

    public LegacyGameResourceProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public GameLocalConfig LocalConfig { get; private set; } = null!;
    public KuroGameApiConfig ApiConfig { get; private set; } = null!;

    public string ApplyMethod { get; set; } = "default";

    public void SetConfig(GameLocalConfig gameLocal, KuroGameApiConfig apiConfig)
    {
        LocalConfig = gameLocal ?? throw new ArgumentNullException(nameof(gameLocal));
        ApiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
    }

    private void EnsureConfigured()
    {
        if (LocalConfig is null || ApiConfig is null)
            throw new InvalidOperationException("请先调用 SetConfig 初始化资源管理器。");
    }

    public async Task<GameLauncherSource> GetGameLauncherSourceAsync(CancellationToken token = default)
    {
        EnsureConfigured();
        return await ReadAsync(ApiConfig.ConfigUrl,
            GameLauncherSourceContext.Default.GameLauncherSource, token);
    }

    public Task<IndexGameResource> GetGameResourceAsync(string url, CancellationToken token = default) =>
        ReadAsync(url, IndexGameResourceContext.Default.IndexGameResource, token);

    public Task<PatchIndexGameResource> GetPatchGameResourceAsync(string url, CancellationToken token = default) =>
        ReadAsync(url, PathIndexGameResourceContext.Default.PatchIndexGameResource, token);

    private async Task<T> ReadAsync<T>(string url,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken token)
    {
        using var response = await _httpClient.GetAsync(url, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(typeInfo, token)
            ?? throw new JsonException($"资源响应为空：{url}");
    }

    public async Task<bool> CheckUpdateAsync(CancellationToken token = default)
    {
        var source = await GetGameLauncherSourceAsync(token);
        var local = await LocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion,token);
        token.ThrowIfCancellationRequested();
        return !string.Equals(local, source.ResourceDefault.Version, StringComparison.Ordinal);
    }

    public async Task<GameVersionInfo> GetInstallGameResourceAsync(CancellationToken token = default)
    {
        var source = await GetGameLauncherSourceAsync(token);
        var resource = source.ResourceDefault;
        return await BuildAsync(resource.Config, resource.CdnList, string.Empty, resource.Version, false, token);
    }

    public async Task<GameVersionInfo> GetUpdateGameResourceAsync(CancellationToken token = default)
    {
        var source = await GetGameLauncherSourceAsync(token);
        var local = await LocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion) ?? string.Empty;
        var resource = source.ResourceDefault;
        if (string.Equals(local, resource.Version, StringComparison.Ordinal))
            return new GameVersionInfo { ResourceVersion = "1", OldGameVersion = local, NewGameVersion = resource.Version };
        return await BuildAsync(resource.Config, resource.CdnList, local, resource.Version, true, token);
    }

    public async Task<GameVersionInfo> GetGameProdownloadResourceAsync(CancellationToken token = default)
    {
        var source = await GetGameLauncherSourceAsync(token);
        var predownload = source.Predownload
            ?? throw new InvalidOperationException("当前没有预下载版本配置。");
        var local = await LocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion) ?? string.Empty;
        return await BuildAsync(predownload.Config, source.ResourceDefault.CdnList,
            local, predownload.Version, true, token);
    }

    private async Task<GameVersionInfo> BuildAsync(global::Waves.Api.Models.Config config, List<CdnList> cdns,
        string localVersion, string targetVersion, bool usePatch, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var patch = usePatch ? config.PatchConfig?.FirstOrDefault(x => x.Version == localVersion) : null;
        // 更新/预下载没有匹配补丁时直接跳过，不回退完整包，也不请求资源索引。
        if (usePatch && patch is null)
            return new GameVersionInfo
            {
                ResourceVersion = "1",
                OldGameVersion = localVersion,
                NewGameVersion = targetVersion
            };
        var cdn = cdns?.Where(x => x.P != 0).OrderBy(x => x.P).FirstOrDefault()
            ?? cdns?.FirstOrDefault()
            ?? throw new InvalidOperationException("资源配置没有 CDN。");
        var baseUrl = JoinUrl(cdn.Url, patch?.BaseUrl ?? config.BaseUrl);
        // 同一索引协议同时承载普通文件、ZIP、Patch 和 Group 信息。
        var index = await GetPatchGameResourceAsync(JoinUrl(cdn.Url, patch?.IndexFile ?? config.IndexFile), token);
        var useGroup = ApplyMethod == "group" && index.ApplyTypes?.Contains("group") == true
            && index.GroupResource is not null;
        var result = new GameVersionInfo
        {
            ResourceVersion = "1",
            OldGameVersion = localVersion,
            NewGameVersion = targetVersion,
            DefaultResource = MapFiles(useGroup ? index.GroupResource : index.Resource, baseUrl),
            DeleteFiles = index.DeleteFiles?.ToList() ?? []
        };
        result.ZipResources = index.ZipFileInfos?.Select(x => new PatchGameFileInfo
        {
            Dest = x.Dest, Entries = MapFiles(x.Entries, baseUrl)
        }).ToList() ?? [];
        result.PatchResources = index.PatchInfos?.Select(x => new PatchGameFileInfo
        {
            Dest = x.Dest, Entries = MapFiles(x.Entries, baseUrl)
        }).ToList() ?? [];
        foreach (var group in index.GroupInfos ?? [])
            result.PatchResources.Add(new PatchGameFileInfo
            {
                Dest = group.Dest,
                SrcFiles = MapFiles(group.SrcFiles, baseUrl, false),
                DstFiles = MapFiles(group.DstFiles, baseUrl, false)
            });
        return result;
    }

    private static List<GameFileInfo> MapFiles(IEnumerable<IndexResource>? files, string baseUrl, bool downloadable = true) =>
        files?.Select(x => new GameFileInfo
        {
            Dest = x.Dest, Size = x.Size, Hash = x.Md5,
            Start = x.Start, End = x.End, Chunks = x.ChunkInfos?.ToList() ?? [],
            FromFolder = x.FromFolder,
            Url = downloadable ? JoinUrl(
                string.IsNullOrWhiteSpace(x.FromFolder) ? baseUrl : JoinUrl(new Uri(baseUrl).GetLeftPart(UriPartial.Authority), x.FromFolder),
                x.Dest) : string.Empty
        }).ToList() ?? [];

    private static string JoinUrl(string baseUrl, string path) =>
        Uri.TryCreate(path, UriKind.Absolute, out var absolute) &&
        (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp)
            ? absolute.AbsoluteUri : baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
}
