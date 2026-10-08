using Waves.Core.Models.Options;

namespace Waves.Core.Services.GameResourceProvider;

/// <summary>分包协议：配置查询和逐包索引适配，不下载游戏文件，也不写安装状态。</summary>
public sealed partial class BundleGameResourceProvider : IGameResourceProvider
{
    private static readonly HttpClient DefaultClient = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
    private readonly Func<HttpClient> _getClient;

    public BundleGameResourceProvider()
        : this(DefaultClient) { }

    public BundleGameResourceProvider(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _getClient = () => client;
    }

    public BundleGameResourceProvider(IHttpClientService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _getClient = () => service.HttpClient;
    }

    public GameLocalConfig LocalConfig { get; private set; } = null!;
    public KuroGameApiConfig ApiConfig { get; private set; } = null!;

    /// <summary>null 使用服务端组合/全局配置中的 Apply Method。</summary>
    public string? ApplyMethod { get; set; }
    public string? CurrentBundleName { get; set; }
    private string? _savedBundleName;

    public void SetConfig(GameLocalConfig gameLocal, KuroGameApiConfig apiConfig)
    {
        LocalConfig = gameLocal ?? throw new ArgumentNullException(nameof(gameLocal));
        ApiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
    }

    private async Task<GameLauncherBunleSource> ReadConfigAsync(CancellationToken token)
    {
        if (LocalConfig is null || ApiConfig is null)
            throw new InvalidOperationException("请先调用 SetConfig 初始化资源管理器。");
        if (string.IsNullOrWhiteSpace(ApiConfig.BunleConfigUrl))
            throw new InvalidOperationException("请配置 BunleConfigUrl，或读取新版启动器的 Assets/KRApp.conf。");
        _savedBundleName = await LocalConfig.GetConfigAsync(GameLocalSettingName.BunleName, token);
        var source = await ReadJsonAsync(ConfigurationUrls(), GameLauncherBunleSourceContext.Default.GameLauncherBunleSource, token);
        if (source.Bundles.Count == 0 || source.ResourcePacks.Count == 0)
            throw new InvalidDataException("分包地址返回的配置未包含 bundles/resourcePacks。");
        return source;
    }
    private string GetBundleName(GameLauncherBunleSource source, GameResourceParameter? parameter)
    {
        if (!string.IsNullOrWhiteSpace(parameter?.BundleName))
            return parameter.BundleName;
        if (!string.IsNullOrWhiteSpace(CurrentBundleName))
            return CurrentBundleName;
        if (!string.IsNullOrWhiteSpace(_savedBundleName))
            return _savedBundleName;
        if (!string.IsNullOrWhiteSpace(ApiConfig.BunleDefaultBundleName))
            return ApiConfig.BunleDefaultBundleName;
        if (source.Bundles.ContainsKey("default"))
            return "default";
        if (source.Bundles.Count == 1)
            return source.Bundles.Keys.Single();
        throw new InvalidOperationException(
            "请选择资源组合并传入 BundleName，或设置 CurrentBundleName。"
        );
    }

    private static GameLauncherBundle SelectBundle(
        Dictionary<string, GameLauncherBundle> bundles,
        Dictionary<string, BundleResourcePack> packs,
        string name
    )
    {
        if (
            !bundles.TryGetValue(name, out var bundle) || bundle.ResourcePacks is not { Count: > 0 }
        )
            throw new InvalidOperationException($"资源组合不存在或未包含资源包：{name}");
        foreach (var packName in bundle.ResourcePacks)
        {
            ValidateRelativePath(packName, false);
            if (packName.Contains('/') || packName.Contains('\\'))
                throw new InvalidDataException("资源包名称应为单一目录名。");
            if (
                !packs.TryGetValue(packName, out var pack)
                || string.IsNullOrWhiteSpace(pack.Version)
            )
                throw new InvalidDataException(
                    $"资源组合 {name} 引用了缺失或无版本的资源包：{packName}"
                );
            ValidateRelativePath(pack.Version, false);
            if (pack.Version.Contains('/') || pack.Version.Contains('\\'))
                throw new InvalidDataException($"资源包版本应为单一路径段：{packName}");
        }
        return bundle;
    }

    private async Task<Dictionary<string, string>> ReadVersionsAsync(
        IEnumerable<string> names,
        CancellationToken token
    )
    {
        var localVersion = await LocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion, token) ?? "";
        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        var keys = names.Distinct(StringComparer.Ordinal).ToArray();
        var hasPackRecords = false;
        foreach (var name in keys.Concat(["Common", "HD", "SD", "UHD"]).Distinct(StringComparer.Ordinal))
        {
            var version = await LocalConfig.GetConfigAsync(GameLocalSettingName.GetBunlePackVersionKey(name), token);
            hasPackRecords |= version is not null;
            versions[name] = version ?? "";
        }
        return keys.ToDictionary(name => name,
            name => hasPackRecords ? versions[name] : localVersion, StringComparer.Ordinal);
    }
    public async Task<bool> CheckUpdateAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    )
    {
        var summary = await GetResourceSummaryAsync(parameter, token);
        return summary.Update.Availability != GameResourceAvailability.AlreadyCurrent;
    }

    public async Task<GameResourceSummary> GetResourceSummaryAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    )
    {
        var source = await ReadConfigAsync(token);
        var name = GetBundleName(source, parameter);
        var bundle = SelectBundle(source.Bundles, source.ResourcePacks, name);
        var pre = GetPredownload(source, name);
        var names = bundle.ResourcePacks.Concat(pre?.Bundles[name].ResourcePacks ?? []);
        var versions = await ReadVersionsAsync(names, token);
        var apply = ResolveApplyMethod(source, bundle);
        var install = Describe(source.ResourcePacks, bundle, versions, false, false, apply);
        var update = Describe(source.ResourcePacks, bundle, versions, true, false, apply);
        var pred = pre is null
            ? null
            : Describe(
                pre.ResourcePacks,
                pre.Bundles[name],
                versions,
                true,
                false,
                ResolveApplyMethod(source, pre.Bundles[name])
            );
        return new GameResourceSummary
        {
            LocalVersion = versions[bundle.ResourcePacks[0]],
            BundleName = name,
            AvailableBundles = source.Bundles.Keys.ToList(),
            OfficialVersion = source.ResourcePacks[bundle.ResourcePacks[0]].Version,
            PredownloadVersion = pre is null
                ? null
                : pre.ResourcePacks[pre.Bundles[name].ResourcePacks[0]].Version,
            PredownloadEnabled = pre is not null,
            HistoricalVersions = (source.ResourcePacks[bundle.ResourcePacks[0]].PatchConfig ?? [])
                .Select(x => x.Version)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList(),
            Install = Summarize(install, apply),
            Update = Summarize(update, apply),
            Predownload = pred is null
                ? new GameResourceSize { Availability = GameResourceAvailability.NoPredownload }
                : Summarize(pred, ResolveApplyMethod(source, pre!.Bundles[name])),
        };
    }

    private static BundlePredownload? GetPredownload(GameLauncherBunleSource source, string name)
    {
        var pre = source.Predownload;
        var enabled =
            source.Bundles[name].Config?.PredownloadSwitch ?? source.Config.PredownloadSwitch;
        if (enabled == 0 || pre is null || !pre.Bundles.ContainsKey(name))
            return null;
        var bundle = SelectBundle(pre.Bundles, pre.ResourcePacks, name);
        return bundle.ResourcePacks.Any(n =>
            !source.ResourcePacks.TryGetValue(n, out var current)
            || CompareVersion(pre.ResourcePacks[n].Version, current.Version) > 0
        )
            ? pre
            : null;
    }

    public Task<GameVersionInfo> GetInstallGameResourceAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    ) => BuildAsync(parameter, false, false, null, token);

    public Task<GameVersionInfo> GetUpdateGameResourceAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    ) => BuildAsync(parameter, true, false, null, token);

    public Task<GameVersionInfo> GetGameProdownloadResourceAsync(
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    ) => BuildAsync(parameter, true, true, null, token);

    public Task<GameVersionInfo> GetVerificationResourceAsync(
        string targetVersion,
        GameResourceParameter? parameter = null,
        CancellationToken token = default
    ) => BuildAsync(parameter, false, false, targetVersion, token);

    private async Task<GameVersionInfo> BuildAsync(
        GameResourceParameter? parameter,
        bool update,
        bool predownload,
        string? verificationVersion,
        CancellationToken token
    )
    {
        var source = await ReadConfigAsync(token);
        var name = GetBundleName(source, parameter);
        var packs = source.ResourcePacks;
        var bundles = source.Bundles;
        var cdns = source.CdnList;
        var bundle = SelectBundle(bundles, packs, name);
        if (verificationVersion is not null && parameter?.TargetPackVersions is null
            && packs[bundle.ResourcePacks[0]].Version == verificationVersion
            && source.Predownload is { } candidate && candidate.Bundles.TryGetValue(name, out var preBundle)
            && preBundle.ResourcePacks.Count > 0
            && candidate.ResourcePacks.TryGetValue(preBundle.ResourcePacks[0], out var primary)
            && primary.Version == verificationVersion
            && !SameTargets(packs, bundle, candidate.ResourcePacks, preBundle))
            throw new InvalidOperationException("正式与预下载配置的主版本相同但分包目标不同，请传入清单的 TargetPackVersions。");
        if (
            predownload
            || verificationVersion is not null
                && (packs[bundle.ResourcePacks[0]].Version != verificationVersion
                    || parameter?.TargetPackVersions is { } targets && !MatchesTargets(packs, bundle, targets))
        )
        {
            var pre = verificationVersion is null
                ? GetPredownload(source, name)
                : source.Predownload;
            if (pre is null || !pre.Bundles.ContainsKey(name))
            {
                if (verificationVersion is not null)
                    throw new InvalidOperationException(
                        $"目标版本配置已失效：{verificationVersion}"
                    );
                return new GameVersionInfo
                {
                    ResourceVersion = "2",
            BundleName = name,
                    Availability = GameResourceAvailability.NoPredownload,
                };
            }
            packs = pre.ResourcePacks;
            bundles = pre.Bundles;
            bundle = SelectBundle(bundles, packs, name);
            if (pre.CdnList.Count > 0)
                cdns = pre.CdnList;
        }
        var target = packs[bundle.ResourcePacks[0]].Version;
        if (verificationVersion is not null && parameter?.TargetPackVersions is { } expected
            && !MatchesTargets(packs, bundle, expected))
            throw new InvalidOperationException("逐包目标版本配置已失效，保留原下载清单并重新查询配置。");
        if (verificationVersion is not null && verificationVersion != target)
            throw new InvalidOperationException($"目标版本配置已失效：{verificationVersion}");
        var versions = await ReadVersionsAsync(bundle.ResourcePacks, token);
        var apply = ResolveApplyMethod(source, bundle);
        var selected = Describe(
            packs,
            bundle,
            versions,
            update,
            verificationVersion is not null,
            apply
        );
        var size = Summarize(selected, apply);
        var result = new GameVersionInfo
        {
            ResourceVersion = "2",
            BundleName = name,
            OldGameVersion = versions[bundle.ResourcePacks[0]],
            NewGameVersion = target,
            Availability = size.Availability,
            DownloadSize = size.DownloadSize,
            TargetSize = size.TargetSize,
            RequiredSpace = size.RequiredSpace,
            ResourcePacks = selected.Select(x => x.Info).ToList(),
            CdnCandidates = OrderCdns(cdns)
                .Select(x => new GameResourceCdn
                {
                    Url = x.Url,
                    Priority = x.P,
                    SpeedWeight = x.K1,
                    PriorityWeight = x.K2,
                })
                .ToList(),
        };
        // 已是目标版本等非执行状态不请求索引；Legacy 的缺补丁规则不混入分包协议。
        if (result.Availability != GameResourceAvailability.Ready)
            return result;
        foreach (
            var selection in selected.Where(x =>
                x.Info.Availability == GameResourceAvailability.Ready
            )
        )
        {
            token.ThrowIfCancellationRequested();
            var indexPath = selection.Patch?.IndexFile ?? selection.Pack.IndexFile;
            var hash = selection.Patch?.IndexFileMd5 ?? selection.Pack.IndexFileMd5;
            selection.Info.IndexUrlCandidates = OrderCdns(cdns)
                .Select(c => JoinUrl(c.Url, indexPath))
                .Distinct()
                .ToList();
            selection.Info.IndexHash = hash;
            var index = await ReadIndexAsync(selection.Info.IndexUrlCandidates, hash, token);
            MapIndex(result, index, selection, cdns, apply, verificationVersion is not null);
        }
        result.DeleteFiles = result.DeleteFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return result;
    }

    private string ResolveApplyMethod(GameLauncherBunleSource source, GameLauncherBundle bundle)
    {
        if (!string.IsNullOrWhiteSpace(ApplyMethod))
            return ApplyMethod;
        var method = bundle.Config?.Experiment?.Apply?.ApplyMethodFeature;
        if (string.IsNullOrWhiteSpace(method))
            method = source.Config.Experiment?.Apply?.ApplyMethodFeature;
        return string.IsNullOrWhiteSpace(method) ? "default" : method;
    }

    private static bool MatchesTargets(Dictionary<string, BundleResourcePack> packs, GameLauncherBundle bundle,
        IReadOnlyDictionary<string, string> targets) =>
        bundle.ResourcePacks.Distinct(StringComparer.Ordinal).Count() == targets.Count
        && bundle.ResourcePacks.All(name => targets.TryGetValue(name, out var version)
            && packs.TryGetValue(name, out var pack) && pack.Version == version);

    private static bool SameTargets(Dictionary<string, BundleResourcePack> packs, GameLauncherBundle bundle,
        Dictionary<string, BundleResourcePack> otherPacks, GameLauncherBundle otherBundle) =>
        MatchesTargets(otherPacks, otherBundle, bundle.ResourcePacks.Distinct(StringComparer.Ordinal)
            .ToDictionary(name => name, name => packs[name].Version));

    private sealed record Selection(
        BundleResourcePack Pack,
        BundlePatchConfig? Patch,
        GameResourcePackInfo Info
    );

    private static List<Selection> Describe(
        Dictionary<string, BundleResourcePack> packs,
        GameLauncherBundle bundle,
        Dictionary<string, string> versions,
        bool update,
        bool verify,
        string apply
    )
    {
        var result = new List<Selection>();
        foreach (var name in bundle.ResourcePacks.Distinct(StringComparer.Ordinal))
        {
            var pack = packs[name];
            var local = versions[name];
            var availability =
                update && !string.IsNullOrEmpty(local) && CompareVersion(local, pack.Version) >= 0
                    ? GameResourceAvailability.AlreadyCurrent
                    : GameResourceAvailability.Ready;
            var patch = !verify ? pack.ZipConfig : null;
            var kind = patch is null ? GameResourcePackKind.Origin : GameResourcePackKind.Zip;
            if (
                update
                && availability == GameResourceAvailability.Ready
                && !string.IsNullOrEmpty(local)
            )
            {
                var matchedPatch = (pack.PatchConfig ?? []).FirstOrDefault(x =>
                    CompareVersion(x.Version, local) == 0
                );
                if (matchedPatch is not null)
                {
                    patch = matchedPatch;
                    kind = GameResourcePackKind.Patch;
                }
            }
            long download = patch?.Size ?? pack.Size;
            long workspace =
                patch?.Ext?.RequiredDiskSpace ?? patch?.UnCompressSize ?? pack.UnCompressSize;
            long delta = patch?.Ext?.DeltaSize ?? 0;
            long maxFile = patch?.Ext?.MaxFileSize ?? 0;
            if (kind == GameResourcePackKind.Patch)
            {
                var evaluation = patch?.Ext?.ApplyEvaluations?.FirstOrDefault(x => x.Name == apply);
                if (evaluation is not null)
                {
                    download = evaluation.Size;
                    workspace = evaluation.RequiredDiskSpace;
                    delta = evaluation.DeltaSize;
                    maxFile = evaluation.MaxFileSize;
                }
            }
            if (download < 0 || workspace < 0 || pack.UnCompressSize < 0)
                throw new InvalidDataException($"资源包大小无效：{name}");
            result.Add(
                new Selection(
                    pack,
                    patch,
                    new GameResourcePackInfo
                    {
                        Name = name,
                        LocalVersion = local,
                        TargetVersion = pack.Version,
                        Kind = kind,
                        Availability = availability,
                        DownloadSize =
                            availability == GameResourceAvailability.Ready ? download : 0,
                        TargetSize = pack.UnCompressSize,
                        RequiredSpace = workspace,
                        DeltaSize = delta,
                        MaxFileSize = maxFile,
                    }
                )
            );
        }
        return result;
    }

    private static GameResourceSize Summarize(List<Selection> selections, string apply)
    {
        var availability =
            selections.Any(x => x.Info.Availability == GameResourceAvailability.MissingPatch)
                ? GameResourceAvailability.MissingPatch
            : selections.All(x => x.Info.Availability == GameResourceAvailability.AlreadyCurrent)
                ? GameResourceAvailability.AlreadyCurrent
            : GameResourceAvailability.Ready;
        var active = selections
            .Where(x => x.Info.Availability == GameResourceAvailability.Ready)
            .ToList();
        long download =
            availability == GameResourceAvailability.Ready
                ? active.Sum(x => x.Info.DownloadSize)
                : 0;
        long extra = 0,
            cumulativeDelta = 0,
            groupPeak = 0;
        foreach (var selection in active)
        {
            if (apply == "group" && selection.Info.Kind == GameResourcePackKind.Patch)
            {
                groupPeak = Math.Max(
                    groupPeak,
                    checked(cumulativeDelta + selection.Info.RequiredSpace)
                );
                cumulativeDelta = checked(cumulativeDelta + selection.Info.DeltaSize);
            }
            else
                extra = checked(extra + selection.Info.RequiredSpace);
        }
        return new GameResourceSize
        {
            Availability = availability,
            DownloadSize = download,
            TargetSize = selections.Sum(x => x.Info.TargetSize),
            RequiredSpace =
                availability == GameResourceAvailability.Ready
                    ? checked(download + extra + groupPeak)
                    : 0,
        };
    }

    private async Task<PatchIndexGameResource> ReadIndexAsync(
        IList<string> urls,
        string expectedHash,
        CancellationToken token
    )
    {
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                using var response = await _getClient().GetAsync(url, token);
                response.EnsureSuccessStatusCode();
                var bytes = await ReadJsonBytesAsync(response.Content, token);
                if (
                    !string.IsNullOrWhiteSpace(expectedHash)
                    && !Convert
                        .ToHexString(MD5.HashData(bytes))
                        .Equals(expectedHash, StringComparison.OrdinalIgnoreCase)
                )
                    throw new InvalidDataException($"资源索引 Hash 不匹配：{url}");
                var index =
                    JsonSerializer.Deserialize(
                        bytes,
                        PathIndexGameResourceContext.Default.PatchIndexGameResource
                    ) ?? throw new JsonException($"资源索引为空：{url}");
                if (index.Resource is null && index.GroupResource is null)
                    throw new InvalidDataException($"资源索引缺少文件列表：{url}");
                return index;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                last = ex;
            }
            catch (Exception ex)
                when (ex
                        is HttpRequestException
                            or IOException
                            or InvalidDataException
                            or JsonException
                )
            {
                last = ex;
            }
        }
        throw new IOException("所有 CDN 的分包索引请求均失败。", last);
    }

    private static void MapIndex(
        GameVersionInfo result,
        PatchIndexGameResource index,
        Selection selection,
        List<BundleCdnSource> cdns,
        string apply,
        bool verification
    )
    {
        var useGroup =
            !verification
            && apply == "group"
            && index.ApplyTypes?.Contains("group") == true
            && index.GroupResource is not null;
        var files = (index.Resource ?? [])
            .Select(f => (File: f, Selected: !useGroup))
            .Concat((index.GroupResource ?? []).Select(f => (File: f, Selected: useGroup)))
            .GroupBy(x => x.File.Dest, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
                g.FirstOrDefault(x => x.Selected).File is not null
                    ? g.First(x => x.Selected)
                    : g.First()
            );
        foreach (var (file, selected) in files)
        {
            var zip = index.ZipFileInfos?.FirstOrDefault(x => x.Dest == file.Dest);
            var patch = index.PatchInfos?.FirstOrDefault(x => x.Dest == file.Dest);
            var group = index.GroupInfos?.FirstOrDefault(x => x.Dest == file.Dest);
            if (verification && !selected)
                continue;
            var mapped = MapFile(file, selection, cdns, false);
            mapped.IsSelected = selected;
            if (zip is null && patch is null && group is null)
            {
                if (
                    file.Dest.EndsWith(".krzip", StringComparison.OrdinalIgnoreCase)
                    || file.Dest.EndsWith(".krdiff", StringComparison.OrdinalIgnoreCase)
                    || file.Dest.EndsWith(".krpdiff", StringComparison.OrdinalIgnoreCase)
                )
                    throw new InvalidDataException(
                        $"下载载体缺少 Entries/SrcFiles/DstFiles 描述：{file.Dest}"
                    );
                result.DefaultResource.Add(mapped);
                continue;
            }
            if (verification)
            {
                foreach (var entry in group?.DstFiles ?? zip?.Entries ?? patch?.Entries ?? [])
                    result.DefaultResource.Add(MapFile(entry, selection, cdns, true));
                continue;
            }
            var task = new PatchGameFileInfo
            {
                ResourcePackName = mapped.ResourcePackName,
                CacheRelativePath = mapped.CacheRelativePath,
                Dest = mapped.Dest,
                Url = mapped.Url,
                UrlCandidates = mapped.UrlCandidates,
                FromFolder = mapped.FromFolder,
                Size = mapped.Size,
                Hash = mapped.Hash,
                Chunks = mapped.Chunks,
                Start = mapped.Start,
                End = mapped.End,
                IsSelected = selected,
                IsGroup = group is not null,
                Entries = (zip?.Entries ?? patch?.Entries ?? [])
                    .Select(x => MapFile(x, selection, cdns, true))
                    .ToList(),
                SrcFiles = (group?.SrcFiles ?? [])
                    .Select(x => MapFile(x, selection, cdns, true))
                    .ToList(),
                DstFiles = (group?.DstFiles ?? [])
                    .Select(x => MapFile(x, selection, cdns, true))
                    .ToList(),
            };
            if (zip is not null)
                result.ZipResources.Add(task);
            else
                result.PatchResources.Add(task);
        }
        if (!verification)
            foreach (var file in index.DeleteFiles ?? [])
            {
                ValidateRelativePath(file, false);
                result.DeleteFiles.Add(file);
            }
    }

    private static GameFileInfo MapFile(
        IndexResource file,
        Selection selection,
        List<BundleCdnSource> cdns,
        bool origin
    )
    {
        ValidateRelativePath(file.Dest, false);
        var folder = origin
            ? selection.Pack.Folder
            : selection.Patch?.Folder ?? selection.Pack.Folder;
        ValidateRelativePath(folder, true);
        var baseUrl = origin
            ? selection.Pack.BaseUrl
            : selection.Patch?.BaseUrl ?? selection.Pack.BaseUrl;
        var remote = file.FromFolder ?? JoinUrl(baseUrl, folder);
        var urls = OrderCdns(cdns)
            .Select(c => JoinUrl(JoinUrl(c.Url, remote), file.Dest))
            .Distinct()
            .ToList();
        if (urls.Count == 0)
            throw new InvalidDataException("分包配置没有可用 CDN。");
        return new GameFileInfo
        {
            ResourcePackName = selection.Info.Name,
            CacheRelativePath = JoinRelative(
                selection.Info.Name,
                selection.Info.TargetVersion,
                file.Dest
            ),
            Dest = file.Dest,
            FromFolder = file.FromFolder,
            Size = file.Size,
            Hash = file.Md5 ?? "",
            Url = urls[0],
            UrlCandidates = urls,
            Start = file.Start,
            End = file.End,
            Chunks =
                file.ChunkInfos?.Select(x => new GameFileChunkInfo
                    {
                        Start = x.Start,
                        End = x.End,
                        Hash = x.Md5,
                    })
                    .ToList()
                ?? [],
        };
    }

    private static IEnumerable<BundleCdnSource> OrderCdns(IEnumerable<BundleCdnSource> cdns) =>
        cdns.Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .OrderBy(x => x.P == 0 ? int.MaxValue : x.P);

    private static string JoinRelative(params string[] parts) =>
        string.Join(
            '/',
            parts.Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Replace('\\', '/').Trim('/'))
        );

    private static string JoinUrl(string root, string relative) =>
        Uri.TryCreate(relative, UriKind.Absolute, out var absolute)
        && absolute.Scheme is "https" or "http"
            ? absolute.AbsoluteUri
            : root.TrimEnd('/')
                + (string.IsNullOrEmpty(relative) ? "" : "/" + relative.TrimStart('/'));

    private static void ValidateRelativePath(string path, bool allowEmpty)
    {
        if (allowEmpty && string.IsNullOrEmpty(path))
            return;
        if (
            string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains(':')
            || path.Replace('\\', '/').Split('/').Any(x => x == ".." || x == ".")
        )
            throw new InvalidDataException($"资源相对路径无效：{path}");
    }

    private static int CompareVersion(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return string.IsNullOrWhiteSpace(right) ? 0 : -1;
        if (string.IsNullOrWhiteSpace(right))
            return 1;
        var a = left.Split('.');
        var b = right.Split('.');
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            if (
                !long.TryParse(i < a.Length ? a[i] : "0", out var av)
                || !long.TryParse(i < b.Length ? b[i] : "0", out var bv)
            )
                return string.Compare(left, right, StringComparison.Ordinal);
            var comparison = av.CompareTo(bv);
            if (comparison != 0)
                return comparison;
        }
        return 0;
    }
}

