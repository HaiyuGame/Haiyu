using Waves.Core.Models.Options;

namespace Waves.Core.Services.GameResourceProvider;

public sealed partial class BundleGameResourceProvider
{
    public bool SupportsPackageType { get; set; }

    public async Task LoadPackageSizesAsync(IEnumerable<BunleResourceSize> bundles, CancellationToken token = default)
    {
        if (!SupportsPackageType)
            return;
        var items = bundles.ToList();
        var source = await ReadConfigAsync(token);
        var sizes = new Dictionary<string, long>();
        foreach (var name in items.SelectMany(x => source.Bundles[x.BunleName].ResourcePacks).Distinct())
        {
            var pack = source.ResourcePacks[name];
            var urls = OrderCdns(source.CdnList).Select(c => JoinUrl(c.Url, pack.IndexFile)).ToList();
            var index = await ReadIndexAsync(urls, pack.IndexFileMd5, token);
            var selection = new Selection(pack, null, new GameResourcePackInfo
            {
                Name = name,
                TargetVersion = pack.Version
            });
            var plan = new GameVersionInfo();
            MapIndex(plan, BundleIndexFilter.Apply(index, GamePackageType.Minimal), selection,
                source.CdnList, "default", true);
            sizes[name] = plan.DefaultResource.Sum(x => x.Size);
        }
        foreach (var item in items)
            item.MinimalSize = source.Bundles[item.BunleName].ResourcePacks.Sum(name => sizes[name]);
    }

    public async Task<GamePackageType> ReadPackageTypeAsync(string bundleName, CancellationToken token = default) =>
        SupportsPackageType
            ? GamePackageStateService.Parse(await LocalConfig.GetConfigAsync(
                GameLocalSettingName.GetPackageTypeKey(bundleName), token)) ?? GamePackageType.Full
            : GamePackageType.Full;

    public async Task SavePackageTypeAsync(string bundleName, GamePackageType packageType, CancellationToken token = default)
    {
        if (SupportsPackageType && !await LocalConfig.SaveConfigAsync(
            GameLocalSettingName.GetPackageTypeKey(bundleName), GamePackageStateService.ToValue(packageType), token))
            throw new IOException("资源量偏好保存失败。");
    }

    private async Task<bool> HasPackageStateChangedAsync(string bundleName, CancellationToken token)
    {
        var preference = GamePackageStateService.Parse(await LocalConfig.GetConfigAsync(
            GameLocalSettingName.GetPackageTypeKey(bundleName), token));
        var folder = await LocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder, token);
        if (preference is null || string.IsNullOrWhiteSpace(folder))
            return false;
        var gameType = await GamePackageStateService.ReadAsync(folder, ApiConfig.PackageStatePath!, bundleName, token);
        return gameType is not null && gameType != preference;
    }

    public async Task<bool> MatchesPredownloadSelectionAsync(string? bundleName, CancellationToken token = default)
    {
        var recordedBundle = await LocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadBundleName, token);
        if (!string.IsNullOrWhiteSpace(recordedBundle) && recordedBundle != bundleName)
            return false;
        if (!SupportsPackageType || string.IsNullOrWhiteSpace(bundleName))
            return true;
        var recordedType = GamePackageStateService.Parse(await LocalConfig.GetConfigAsync(
            GameLocalSettingName.ProdDownloadPackageType, token)) ?? GamePackageType.Full;
        return recordedType == await ReadPackageTypeAsync(bundleName, token);
    }

    public async Task<GameResourceParameter> ReadPredownloadParameterAsync(
        GameResourceParameter? parameter, CancellationToken token = default)
    {
        var bundle = await LocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadBundleName, token);
        var type = await LocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadPackageType, token);
        return new GameResourceParameter
        {
            BundleName = string.IsNullOrWhiteSpace(bundle) ? parameter?.BundleName : bundle,
            PackageType = SupportsPackageType ? GamePackageStateService.Parse(type) ?? GamePackageType.Full : null,
            DownloadFolder = await LocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadPath, token)
        };
    }

    private async Task ApplyGroupFallbackAsync(GameVersionInfo plan, GameResourceParameter? parameter,
        bool predownload, CancellationToken token)
    {
        var gameFolder = await LocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder, token);
        if (string.IsNullOrWhiteSpace(gameFolder))
            return;
        var cache = parameter?.DownloadFolder;
        if (string.IsNullOrWhiteSpace(cache) && predownload)
            cache = await LocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadPath, token);
        cache = new InstallOption { IsProd = predownload, DownloadFolder = cache }.ResolveDownloadFolder(gameFolder);
        bool Exists(string folder, GameFileInfo file) =>
            File.Exists(BuildFileHelper.ResolveFilePath(folder, file.Dest))
            && new FileInfo(BuildFileHelper.ResolveFilePath(folder, file.Dest)).Length == file.Size;
        foreach (var group in plan.PatchResources.Where(x => x.IsSelected && x.IsGroup).ToList())
        {
            token.ThrowIfCancellationRequested();
            if (group.DstFiles.Count == 0)
                continue;
            var cachedTargets = group.DstFiles.All(x => Exists(Path.Combine(cache, "resources"), x));
            if (!cachedTargets && (Exists(Path.Combine(cache, "patchGroup"), group)
                || group.SrcFiles.Count > 0 && group.SrcFiles.All(x => Exists(gameFolder, x))))
                continue;
            plan.PatchResources.Remove(group);
            foreach (var file in group.DstFiles)
            {
                if (!plan.DefaultResource.Any(x => x.IsSelected
                    && x.ResourcePackName == file.ResourcePackName
                    && string.Equals(x.Dest, file.Dest, StringComparison.OrdinalIgnoreCase)))
                    plan.DefaultResource.Add(file);
            }
        }
    }

    private static IEnumerable<GameFileInfo> SelectedFiles(GameVersionInfo plan) =>
        plan.DefaultResource.Concat(plan.ZipResources).Concat(plan.PatchResources).Where(x => x.IsSelected);

    private static void RecalculatePackageSizes(GameVersionInfo plan, bool predownload, double spaceRatio, string? gameFolder)
    {
        var files = SelectedFiles(plan).ToList();
        plan.DownloadSize = files.Sum(x => x.Size);
        foreach (var pack in plan.ResourcePacks)
            pack.DownloadSize = files.Where(x => x.ResourcePackName == pack.Name).Sum(x => x.Size);
        var zipSize = plan.ZipResources.Where(x => x.IsSelected).Sum(x => x.Entries.Sum(f => f.Size));
        long peak = 0, delta = 0;
        foreach (var group in plan.PatchResources.Where(x => x.IsSelected && x.IsGroup))
        {
            var target = group.DstFiles.Sum(x => x.Size);
            peak = Math.Max(peak, delta + target);
            delta += target - group.SrcFiles.Where(x => !string.IsNullOrWhiteSpace(gameFolder)
                && File.Exists(BuildFileHelper.ResolveFilePath(gameFolder, x.Dest))).Sum(x => x.Size);
        }
        var patchSize = plan.PatchResources.Where(x => x.IsSelected && !x.IsGroup).Sum(x => x.Entries.Sum(f => f.Size));
        var extra = Math.Max(peak, Math.Max(zipSize, patchSize));
        plan.RequiredSpace = extra > 0
            ? checked(plan.DownloadSize + (predownload ? files.Select(x => x.Size).DefaultIfEmpty().Max() : extra))
            : (long)(plan.DownloadSize * spaceRatio);
    }
}
