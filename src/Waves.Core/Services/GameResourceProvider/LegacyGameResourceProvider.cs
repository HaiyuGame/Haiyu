using Waves.Core.Models.Options;

namespace Waves.Core.Services.GameResourceProvider;

/// <summary>旧版单资源包协议的请求和统一资源适配。</summary>
public sealed partial class LegacyGameResourceProvider : IGameResourceProvider
{
    private static readonly HttpClient DefaultClient = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
    private readonly HttpClient? _httpClient;
    private readonly IHttpClientService? _httpClientService;

    public LegacyGameResourceProvider()
        : this(DefaultClient) { }

    public LegacyGameResourceProvider(IHttpClientService service)
    {
        _httpClientService = service ?? throw new ArgumentNullException(nameof(service));
    }

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

    private async Task<GameLauncherSource> GetGameLauncherSourceAsync(
        CancellationToken token = default
    )
    {
        EnsureConfigured();
        return await ReadAsync(
            ApiConfig.ConfigUrl,
            GameLauncherSourceContext.Default.GameLauncherSource,
            token
        );
    }

    private Task<PatchIndexGameResource> GetPatchGameResourceAsync(
        string url,
        CancellationToken token = default
    ) => ReadAsync(url, PathIndexGameResourceContext.Default.PatchIndexGameResource, token);

    private async Task<T> ReadAsync<T>(
        string url,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken token
    )
    {
        using var response = await (_httpClientService?.HttpClient ?? _httpClient!).GetAsync(
            url,
            token
        );
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(typeInfo, token)
            ?? throw new JsonException($"资源响应为空：{url}");
    }

    public async Task<bool> CheckUpdateAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    )
    {
        var source = await GetGameLauncherSourceAsync(token);
        var local = await LocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion, token);
        token.ThrowIfCancellationRequested();
        return !string.Equals(local, source.ResourceDefault.Version, StringComparison.Ordinal);
    }

    public async Task<GameVersionInfo> GetInstallGameResourceAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    )
    {
        var source = await GetGameLauncherSourceAsync(token);
        var resource = source.ResourceDefault;
        return await BuildAsync(
            resource.Config,
            resource.CdnList,
            string.Empty,
            resource.Version,
            false,
            token,
            resource.ResourcesBasePath
        );
    }

    public async Task<GameVersionInfo> GetUpdateGameResourceAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    )
    {
        var source = await GetGameLauncherSourceAsync(token);
        var local =
            await LocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion, token)
            ?? string.Empty;
        var resource = source.ResourceDefault;
        if (string.Equals(local, resource.Version, StringComparison.Ordinal))
            return new GameVersionInfo
            {
                ResourceVersion = "1",
                OldGameVersion = local,
                NewGameVersion = resource.Version,
                Availability = GameResourceAvailability.AlreadyCurrent,
            };
        return await BuildAsync(
            resource.Config,
            resource.CdnList,
            local,
            resource.Version,
            true,
            token,
            resource.ResourcesBasePath
        );
    }

    public async Task<GameVersionInfo> GetGameProdownloadResourceAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    )
    {
        var source = await GetGameLauncherSourceAsync(token);
        var predownload = source.Predownload;
        if (predownload is null)
            return new GameVersionInfo
            {
                ResourceVersion = "1",
                Availability = GameResourceAvailability.NoPredownload,
            };
        var local =
            await LocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion, token)
            ?? string.Empty;
        if (local == predownload.Version)
            return new GameVersionInfo
            {
                ResourceVersion = "1",
                OldGameVersion = local,
                NewGameVersion = predownload.Version,
                Availability = GameResourceAvailability.AlreadyCurrent,
            };
        return await BuildAsync(
            predownload.Config,
            source.ResourceDefault.CdnList,
            local,
            predownload.Version,
            true,
            token,
            predownload.ResourcesBasePath
        );
    }

    private async Task<GameVersionInfo> BuildAsync(
        global::Waves.Api.Models.Config config,
        List<CdnList> cdns,
        string localVersion,
        string targetVersion,
        bool usePatch,
        CancellationToken token,
        string? resourcesBasePath = null
    )
    {
        token.ThrowIfCancellationRequested();
        var patch = usePatch
            ? config.PatchConfig?.FirstOrDefault(x => x.Version == localVersion)
            : null;
        // 更新/预下载没有匹配补丁时直接跳过，不回退完整包，也不请求资源索引。
        if (usePatch && patch is null)
            return new GameVersionInfo
            {
                ResourceVersion = "1",
                OldGameVersion = localVersion,
                NewGameVersion = targetVersion,
                Availability = GameResourceAvailability.MissingPatch,
            };
        var cdn =
            cdns?.Where(x => x.P != 0).OrderBy(x => x.P).FirstOrDefault()
            ?? cdns?.FirstOrDefault()
            ?? throw new InvalidOperationException("资源配置没有 CDN。");
        var baseUrl = JoinUrl(cdn.Url, patch?.BaseUrl ?? config.BaseUrl);
        // 同一索引协议同时承载普通文件、ZIP、Patch 和 Group 信息。
        var index = await ReadIndexAsync(cdns, patch?.IndexFile ?? config.IndexFile, token);
        var useGroup =
            ApplyMethod == "group"
            && index.ApplyTypes?.Contains("group") == true
            && index.GroupResource?.Count > 0;
        var files = MapFiles(index.Resource, baseUrl);
        if (index.GroupResource is not null)
        {
            foreach (var file in files)
                file.IsSelected = !useGroup;
            var alternatives = MapFiles(index.GroupResource, baseUrl);
            foreach (var file in alternatives)
                file.IsSelected = useGroup;
            files = files
                .Concat(alternatives)
                .GroupBy(x => x.Dest, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.FirstOrDefault(f => f.IsSelected) ?? x.First())
                .ToList();
        }
        files = files
            .GroupBy(x => x.Dest, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.FirstOrDefault(f => f.IsSelected) ?? x.First())
            .ToList();
        foreach (var file in files)
        {
            var carrier =
                file.Dest.Contains("krdiff", StringComparison.OrdinalIgnoreCase)
                || file.Dest.Contains("krpdiff", StringComparison.OrdinalIgnoreCase)
                || file.Dest.Contains("krzip", StringComparison.OrdinalIgnoreCase);
            if (
                !carrier
                && !string.IsNullOrWhiteSpace(resourcesBasePath)
                && string.IsNullOrWhiteSpace(file.FromFolder)
            )
                file.Url = JoinUrl(JoinUrl(cdn.Url, resourcesBasePath), file.Dest);
            file.UrlCandidates = cdns.OrderBy(x => x.P == 0 ? int.MaxValue : x.P)
                .Select(candidate => ReplaceOrigin(file.Url, cdn.Url, candidate.Url))
                .Distinct()
                .ToList();
        }
        var result = new GameVersionInfo
        {
            ResourceVersion = "1",
            OldGameVersion = localVersion,
            NewGameVersion = targetVersion,
            DownloadSize = patch?.Size ?? config.Size,
            TargetSize = config.UnCompressSize,
            CdnCandidates = cdns.OrderBy(x => x.P == 0 ? int.MaxValue : x.P)
                .Select(x => new GameResourceCdn
                {
                    Url = x.Url,
                    Priority = x.P,
                    SpeedWeight = x.K1,
                    PriorityWeight = x.K2,
                })
                .ToList(),
            DeleteFiles = index.DeleteFiles?.ToList() ?? [],
        };
        foreach (var file in files)
        {
            var zip = index.ZipFileInfos?.FirstOrDefault(x => x.Dest == file.Dest);
            var diff = index.PatchInfos?.FirstOrDefault(x => x.Dest == file.Dest);
            var group = index.GroupInfos?.FirstOrDefault(x => x.Dest == file.Dest);
            bool isZip =
                zip is not null || file.Dest.Contains("krzip", StringComparison.OrdinalIgnoreCase);
            bool isPatch =
                diff is not null
                || group is not null
                || file.Dest.Contains("krdiff", StringComparison.OrdinalIgnoreCase)
                || file.Dest.Contains("krpdiff", StringComparison.OrdinalIgnoreCase);
            if (!isZip && !isPatch)
            {
                result.DefaultResource.Add(file);
                continue;
            }
            var task = new PatchGameFileInfo
            {
                Dest = file.Dest,
                Url = file.Url,
                UrlCandidates = file.UrlCandidates,
                IsSelected = file.IsSelected,
                Size = file.Size,
                Hash = file.Hash,
                Chunks = file.Chunks,
                FromFolder = file.FromFolder,
                Start = file.Start,
                End = file.End,
                IsGroup =
                    group is not null
                    || file.Dest.Contains("krpdiff", StringComparison.OrdinalIgnoreCase),
                Entries = MapFiles(isZip ? zip?.Entries : diff?.Entries, baseUrl, false),
                SrcFiles = MapFiles(group?.SrcFiles, baseUrl, false),
                DstFiles = MapFiles(group?.DstFiles, baseUrl, false),
            };
            if (isZip)
                result.ZipResources.Add(task);
            else
                result.PatchResources.Add(task);
        }
        return result;
    }

    public async Task<GameResourceSummary> GetResourceSummaryAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    )
    {
        var source = await GetGameLauncherSourceAsync(token);
        var local =
            await LocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion, token)
            ?? string.Empty;
        var current = source.ResourceDefault;
        var patch = current.Config.PatchConfig?.FirstOrDefault(x => x.Version == local);
        var pre = source.Predownload;
        var prePatch = pre?.Config.PatchConfig?.FirstOrDefault(x => x.Version == local);
        return new GameResourceSummary
        {
            LocalVersion = local,
            OfficialVersion = current.Version,
            PredownloadVersion = pre?.Version,
            HistoricalVersions = HistoricalVersions(current.Config, current.Version),
            PredownloadEnabled = source.PredownloadSwitch != 0 && pre is not null,
            Install = Size(
                GameResourceAvailability.Ready,
                current.Config.Size,
                current.Config.UnCompressSize
            ),
            Update = Size(
                local == current.Version ? GameResourceAvailability.AlreadyCurrent
                    : patch is null ? GameResourceAvailability.MissingPatch
                    : GameResourceAvailability.Ready,
                local == current.Version ? 0 : patch?.Size ?? 0,
                current.Config.UnCompressSize
            ),
            Predownload = Size(
                pre is null ? GameResourceAvailability.NoPredownload
                    : local == pre.Version ? GameResourceAvailability.AlreadyCurrent
                    : prePatch is null ? GameResourceAvailability.MissingPatch
                    : GameResourceAvailability.Ready,
                prePatch?.Size ?? 0,
                pre?.Config.UnCompressSize ?? 0
            ),
        };
    }

    public async Task<GameVersionInfo> GetVerificationResourceAsync(
        string targetVersion,
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    )
    {
        var source = await GetGameLauncherSourceAsync(token);
        global::Waves.Api.Models.Config config;
        string resourcesBasePath;
        if (source.ResourceDefault.Version == targetVersion)
        {
            config = source.ResourceDefault.Config;
            resourcesBasePath = source.ResourceDefault.ResourcesBasePath;
        }
        else if (source.Predownload?.Version == targetVersion)
        {
            config = source.Predownload.Config;
            resourcesBasePath = source.Predownload.ResourcesBasePath;
        }
        else
            throw new InvalidOperationException($"目标版本配置已失效：{targetVersion}");
        var result = await BuildAsync(
            config,
            source.ResourceDefault.CdnList,
            "",
            targetVersion,
            false,
            token,
            resourcesBasePath
        );
        // 完整索引若带压缩载体描述，校验的是其目标 Entries/DstFiles，而非缓存中的载体。
        var targets = result
            .ZipResources.Where(x => x.IsSelected)
            .SelectMany(x => x.Entries)
            .Concat(
                result
                    .PatchResources.Where(x => x.IsSelected)
                    .SelectMany(x => x.IsGroup ? x.DstFiles : x.Entries)
            )
            .ToList();
        foreach (var file in targets)
        {
            file.UrlCandidates = result
                .CdnCandidates.Select(cdn =>
                    JoinUrl(
                        JoinUrl(
                            cdn.Url,
                            string.IsNullOrWhiteSpace(file.FromFolder)
                                ? (
                                    string.IsNullOrWhiteSpace(resourcesBasePath)
                                        ? config.BaseUrl
                                        : resourcesBasePath
                                )
                                : file.FromFolder
                        ),
                        file.Dest
                    )
                )
                .ToList();
            file.Url = file.UrlCandidates.First();
        }
        result.DefaultResource = result
            .DefaultResource.Concat(targets)
            .GroupBy(x => x.Dest, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.FirstOrDefault(f => f.IsSelected) ?? x.First())
            .ToList();
        result.ZipResources = [];
        result.PatchResources = [];
        return result;
    }

    private async Task<PatchIndexGameResource> ReadIndexAsync(
        IEnumerable<CdnList> cdns,
        string path,
        CancellationToken token
    )
    {
        Exception? last = null;
        foreach (var cdn in cdns.OrderBy(x => x.P == 0 ? int.MaxValue : x.P))
        {
            try
            {
                return await GetPatchGameResourceAsync(JoinUrl(cdn.Url, path), token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                last = ex;
            }
        }
        throw new IOException("资源索引请求失败。", last);
    }

    private static List<string> HistoricalVersions(
        global::Waves.Api.Models.Config config,
        string officialVersion
    )
    {
        // 保留原历史版本选择的路径版本提取规则，配置未带路径版本时使用匹配版本。
        return (config.PatchConfig ?? [])
            .Select(patch =>
            {
                var matches = Regex
                    .Matches(patch.IndexFile ?? "", @"\d+(?:\.\d+)+")
                    .Select(x => x.Value)
                    .Distinct()
                    .ToList();
                return matches.Count > 1 ? matches[1] : patch.Version;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x) && x != officialVersion)
            .Distinct()
            .ToList();
    }

    private static GameResourceSize Size(
        GameResourceAvailability availability,
        long download,
        long target
    ) =>
        new()
        {
            Availability = availability,
            DownloadSize = download,
            TargetSize = target,
            RequiredSpace = checked(download + target),
        };

    private static string ReplaceOrigin(string url, string original, string candidate) =>
        url.StartsWith(original.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)
            ? candidate.TrimEnd('/') + url[original.TrimEnd('/').Length..]
            : url;

    private static List<GameFileInfo> MapFiles(
        IEnumerable<IndexResource>? files,
        string baseUrl,
        bool downloadable = true
    ) =>
        files
            ?.Select(x => new GameFileInfo
            {
                Dest = x.Dest,
                Size = x.Size,
                Hash = x.Md5,
                Start = x.Start,
                End = x.End,
                Chunks =
                    x.ChunkInfos?.Select(c => new GameFileChunkInfo
                        {
                            Start = c.Start,
                            End = c.End,
                            Hash = c.Md5,
                        })
                        .ToList()
                    ?? [],
                FromFolder = x.FromFolder,
                Url = downloadable
                    ? JoinUrl(
                        string.IsNullOrWhiteSpace(x.FromFolder)
                            ? baseUrl
                            : JoinUrl(
                                new Uri(baseUrl).GetLeftPart(UriPartial.Authority),
                                x.FromFolder
                            ),
                        x.Dest
                    )
                    : string.Empty,
            })
            .ToList()
        ?? [];

    private static string JoinUrl(string baseUrl, string path) =>
        Uri.TryCreate(path, UriKind.Absolute, out var absolute)
        && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp)
            ? absolute.AbsoluteUri
            : baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
}
