using System.Text.Json.Serialization.Metadata;
using Waves.Core.Contracts;
using Waves.Core.Models;
using Waves.Core.Models.Enums;
using Waves.Core.Models.OfficialLauncher;
using Waves.Core.Models.Options;
using Waves.Core.Services.GameResourceProvider;

namespace Waves.Core.Services;

/// <summary>
/// 官方启动器安装记录工具：向游戏目录写入官方启动器（KRLauncher）格式的安装记录，
/// 使官方启动器"定位游戏目录"时识别为已安装，避免触发全量重下。
/// 独立工具，不与 Haiyu 下载/更新流程耦合；目录定位复用日志解密工具的上下文选择。
/// </summary>
public class OfficialLauncherRecordService : IOfficialLauncherRecordService
{
    public const string InstalledConfigFileName = "launcherDownloadConfig.json";
    public const string PackVersionDirName = "launcherDownloadConfig";
    public const string LocalIndexFileName = "LocalGameResources.json";

    public LoggerService Logger { get; }
    public AppSettings AppSettings { get; }
    public IServiceProvider ServiceProvider { get; }

    public OfficialLauncherRecordService(
        [FromKeyedServices("AppLog")] LoggerService loggerService,
        AppSettings appSettings,
        IServiceProvider serviceProvider
    )
    {
        Logger = loggerService;
        AppSettings = appSettings;
        ServiceProvider = serviceProvider;
    }

    public async Task<OfficialRecordLocateResult> LocateGameFolderAsync(CancellationToken token = default)
    {
        var context = await ResolveContextAsync(token);
        if (context is null)
        {
            return new OfficialRecordLocateResult { State = OfficialRecordLocateState.GameFolderMissing };
        }
        // 本工具仅适配鸣潮官方启动器的目录格式；其他游戏（战双等）的官方记录语义未经验证。
        if (context.GameType != GameType.Waves)
        {
            return new OfficialRecordLocateResult { State = OfficialRecordLocateState.NotWavesGame };
        }
        var gameFolder = await context.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder,
            token
        );
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            return new OfficialRecordLocateResult { State = OfficialRecordLocateState.GameFolderMissing };
        }
        var isEmpty = false;
        try
        {
            isEmpty = !Directory.EnumerateFileSystemEntries(gameFolder).Any();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // 目录可存在但不可枚举（ACL 受限等）：按非空继续，后续 exe 检查仍会兜底。
            Logger.WriteWarning($"游戏目录枚举失败，按非空处理：{gameFolder}；{ex.Message}");
        }
        if (isEmpty)
        {
            return new OfficialRecordLocateResult { State = OfficialRecordLocateState.GameFolderEmpty };
        }
        // 游戏主程序必须存在：官方判定依赖 exe 存在性，缺失时写入记录无意义。
        if (string.IsNullOrWhiteSpace(context.Config.GameExeName)
            || !File.Exists(Path.Combine(gameFolder, context.Config.GameExeName)))
        {
            return new OfficialRecordLocateResult { State = OfficialRecordLocateState.GameExeMissing };
        }
        var configPath = Path.Combine(gameFolder, InstalledConfigFileName);
        var existingConfig = await DeserializeAsync(
            configPath,
            OfficialLauncherJsonContext.Default.OfficialInstalledConfig,
            token
        );
        return new OfficialRecordLocateResult
        {
            State = OfficialRecordLocateState.Ready,
            GameFolder = gameFolder,
            ContextName = context.ContextName,
            HasExistingRecord = existingConfig is not null,
            ExistingBundles = existingConfig?.Bundles.Keys.ToList() ?? [],
        };
    }

    public async Task<OfficialInstallRecord?> BuildInstallRecordAsync(
        CancellationToken token = default
    )
    {
        var context = await ResolveContextAsync(token);
        if (context is null)
            return null;
        var provider = context.GameResourceProvider;
        if (provider is not BundleGameResourceProvider bundleProvider)
        {
            // 旧协议：官方格式为顶层 version/state，固定 default 包。
            try
            {
                var summary = await provider.GetResourceSummaryAsync(null, token);
                return new OfficialInstallRecord(
                    context.Config.AppId,
                    "default",
                    [new OfficialPackVersion("default", summary.OfficialVersion)]
                )
                {
                    IsLegacy = true,
                };
            }
            catch (Exception ex)
            {
                Logger.WriteError($"旧协议配置拉取失败：{ex.Message}");
                return null;
            }
        }
        return await BuildBundleRecordAsync(context, bundleProvider, token);
    }

    public async Task<OfficialRecordWriteResult> WriteInstallRecordAsync(
        string gameFolder,
        OfficialInstallRecord record,
        CancellationToken token = default
    )
    {
        // 两阶段提交：全部内容先生成为 <目标>.<guid>.tmp 临时文件，全部成功后统一 Move 替换生效；
        // 任一失败则删除全部临时文件并保持旧状态（不出现"半套记录"），失败文件逐项汇总提示。
        var staged = new List<(string TempPath, string TargetPath, string RelativePath)>();
        var failed = new List<string>();
        try
        {
            await StageInstalledConfigAsync(gameFolder, record, staged, failed, token);
            await StagePackVersionsAsync(gameFolder, record, staged, failed, token);
            foreach (var additional in record.AdditionalBundles)
            {
                await StagePackVersionsAsync(gameFolder, ToInstallRecord(record, additional), staged, failed, token);
            }
        }
        catch (OperationCanceledException)
        {
            CleanupStaged(staged);
            throw;
        }
        catch (Exception ex)
        {
            Logger.WriteError($"官方安装记录写入异常：{ex.Message}");
            failed.Add(InstalledConfigFileName);
            CleanupStaged(staged);
            return new OfficialRecordWriteResult { FailedFiles = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList() };
        }
        if (failed.Count > 0)
        {
            // 部分失败 → 全部不生效：清空临时文件，目标文件保持旧状态。
            CleanupStaged(staged);
            Logger.WriteWarning($"官方安装记录部分生成失败，本次写入全部不生效：{string.Join(", ", failed.Distinct())}");
            return new OfficialRecordWriteResult { FailedFiles = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList() };
        }

        // 全部生成成功 → 统一替换生效。
        // 幂等项（TempPath 为空）目标已是期望内容，直接计入成功。
        // 替换前备份每个目标旧内容：替换阶段任一失败时恢复全部已替换目标（两阶段提交语义覆盖到最后一步）。
        var written = new List<string>();
        var backups = new List<(string TargetPath, string? OldContent, bool OldExists)>();
        try
        {
            foreach (var (tempPath, targetPath, relativePath) in staged)
            {
                if (string.IsNullOrEmpty(tempPath))
                {
                    written.Add(relativePath);
                    continue;
                }
                backups.Add((targetPath, File.Exists(targetPath) ? await File.ReadAllTextAsync(targetPath, token) : null, File.Exists(targetPath)));
                try
                {
                    File.Move(tempPath, targetPath, true);
                    written.Add(relativePath);
                }
                catch (Exception ex)
                {
                    Logger.WriteWarning($"官方记录替换失败：{targetPath}；{ex.Message}");
                    failed.Add(relativePath);
                    throw; // 进入回滚
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 恢复所有已替换目标到替换前内容；未替换的临时文件随后清理。
            foreach (var (targetPath, oldContent, oldExists) in backups)
            {
                try
                {
                    if (oldExists)
                    {
                        await File.WriteAllTextAsync(targetPath, oldContent ?? string.Empty, token);
                    }
                    else if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteError($"官方记录回滚失败：{targetPath}；{ex.Message}");
                }
            }
            CleanupStaged(staged.Where(s => !written.Contains(s.RelativePath, StringComparer.OrdinalIgnoreCase)).ToList());
            Logger.WriteWarning($"官方记录替换阶段失败，已回滚全部已生效文件，游戏目录保持旧状态");
            return new OfficialRecordWriteResult { FailedFiles = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList() };
        }
        // 多画质并存时同一分包（如 common）会出现在多个 bundle 的写入清单里；
        // 按文件去重，保证"写入 N 个文件"与实际落盘文件数、移除计数一致。
        return new OfficialRecordWriteResult
        {
            WrittenFiles = written.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            FailedFiles = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    /// <summary>删除尚未生效的临时文件（尽力清理，保留最新错误便于排查）。</summary>
    private void CleanupStaged(List<(string TempPath, string TargetPath, string RelativePath)> staged)
    {
        foreach (var (tempPath, _, _) in staged)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Logger.WriteWarning($"官方记录临时文件清理失败：{tempPath}；{ex.Message}");
            }
        }
    }

    private static OfficialInstallRecord ToInstallRecord(
        OfficialInstallRecord source,
        OfficialBundleRecord bundle
    ) =>
        new(source.AppId, bundle.BundleName, bundle.Packs);

    public async Task<OfficialRecordWriteResult> WriteResourceIndexAsync(
        string gameFolder,
        OfficialInstallRecord record,
        CancellationToken token = default
    )
    {
        var written = new List<string>();
        var failed = new List<string>();
        try
        {
            var files = await FetchTargetManifestAsync(record, token);
            if (files.Count == 0)
            {
                Logger.WriteWarning("官方资源清单拉取为空，跳过 LocalGameResources.json 写入。");
                return new OfficialRecordWriteResult
                {
                    WrittenFiles = written,
                    FailedFiles = [LocalIndexFileName],
                };
            }
            var indexRecord = new OfficialGameResourceRecord
            {
                Resource = files
                    .Where(x => !string.IsNullOrWhiteSpace(x.Dest))
                    .GroupBy(x => x.Dest, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .Select(x => new OfficialResourceFileInfo
                    {
                        Dest = x.Dest.Replace('\\', '/'),
                        Size = x.Size,
                        Md5 = x.Hash ?? string.Empty,
                        FromFolder = x.FromFolder,
                        ChunkInfos = x.Chunks.Count == 0
                            ? null
                            : x.Chunks
                                .Select(c => new OfficialResourceChunkInfo
                                {
                                    Start = c.Start,
                                    End = c.End,
                                    Md5 = c.Hash ?? string.Empty,
                                })
                                .ToList(),
                    })
                    .ToList(),
            };
            var path = Path.Combine(gameFolder, LocalIndexFileName);
            if (
                await TryWriteAtomicAsync(
                    path,
                    JsonSerializer.Serialize(
                        indexRecord,
                        OfficialLauncherJsonContext.Default.OfficialGameResourceRecord
                    ),
                    token
                )
            )
                written.Add(LocalIndexFileName);
            else
                failed.Add(LocalIndexFileName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.WriteError($"官方资源清单写入失败：{ex.Message}");
            failed.Add(LocalIndexFileName);
        }
        return new OfficialRecordWriteResult { WrittenFiles = written, FailedFiles = failed };
    }

    public async Task<OfficialRecordWriteResult> RemoveRecordsAsync(
        string gameFolder,
        CancellationToken token = default
    )
    {
        var removed = new List<string>();
        var failed = new List<string>();
        TryDelete(Path.Combine(gameFolder, InstalledConfigFileName), removed, failed);
        TryDeleteDirectory(Path.Combine(gameFolder, PackVersionDirName), removed, failed);
        TryDelete(Path.Combine(gameFolder, LocalIndexFileName), removed, failed);
        return new OfficialRecordWriteResult { RemovedEntries = removed, FailedFiles = failed };
    }

    /// <summary>
    /// 按需移除分包版本目录中的记录文件：只删除官方记录格式的 .json 文件（逐项计数，与写入口径一致）；
    /// 目录本身与任何非 .json 内容一律保留，避免误删其他工具写入的内容。
    /// </summary>
    private void TryDeleteDirectory(string path, List<string> removed, List<string> failed)
    {
        try
        {
            if (!Directory.Exists(path))
                return;
            foreach (var file in Directory.GetFiles(path, "*.json"))
            {
                var relative = $"{PackVersionDirName}/{Path.GetFileName(file)}";
                try
                {
                    File.Delete(file);
                    removed.Add(relative);
                }
                catch (Exception ex)
                {
                    Logger.WriteWarning($"官方记录删除失败：{file}；{ex.Message}");
                    failed.Add(relative);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.WriteWarning($"官方记录目录访问失败：{path}；{ex.Message}");
            failed.Add($"{PackVersionDirName}/");
        }
    }

    /// <summary>
    /// 分包协议：从服务器分包配置组装安装记录（版本取服务器当前值）。
    /// 多画质检测：按 Haiyu 既有的 BunlePackVersion_* 分包版本记录（GetBunlesAsync 同款判定）
    /// 扫描全部画质组合，已安装的组合全部登记为 bundle 节点（官方 bundles 字典支持多节点并存）。
    /// </summary>
    private async Task<OfficialInstallRecord?> BuildBundleRecordAsync(
        IGameContextV2 context,
        BundleGameResourceProvider bundleProvider,
        CancellationToken token
    )
    {
        GameLauncherBunleSource source;
        try
        {
            source = await bundleProvider.ReadConfigAsync(token);
        }
        catch (Exception ex)
        {
            Logger.WriteError($"分包配置拉取失败：{ex.Message}");
            return null;
        }
        var savedName = await context.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.BunleName,
            token
        );
        var name =
            savedName
            ?? context.Config.BunleDefaultBundleName
            ?? (source.Bundles.Count == 1 ? source.Bundles.Keys.Single() : null);
        if (
            string.IsNullOrWhiteSpace(name)
            || !source.Bundles.TryGetValue(name, out var bundle)
            || bundle.ResourcePacks.Count == 0
        )
        {
            Logger.WriteWarning($"无法确定分包组合：{name ?? "(null)"}");
            return null;
        }
        var additional = new List<OfficialBundleRecord>();
        // 逐分包本地版本（BunlePackVersion_*，含"无分包记录时回退 LocalGameVersion"的既有语义）：
        // 附加画质可能停留在旧版本（装完未更），必须按本地实际版本写入——
        // 写服务器当前版本会给陈旧画质制造假"最新"记录，用户切过去文件缺失。
        var allPackNames = source.Bundles.Values
            .SelectMany(b => b.ResourcePacks)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var localVersions = await bundleProvider.ReadVersionsAsync(allPackNames, token);
        foreach (var (bundleName, bundleConfig) in source.Bundles)
        {
            if (bundleName == name || bundleConfig.ResourcePacks.Count == 0)
                continue;
            // 与 GetBunlesAsync 相同的已安装判定：该画质分包版本记录非空即视为已安装。
            var installedVersion = await context.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.GetBunlePackVersionKey(bundleName),
                token
            );
            if (string.IsNullOrWhiteSpace(installedVersion))
                continue;
            additional.Add(
                new OfficialBundleRecord(
                    bundleName,
                    BuildPackVersions(localVersions, bundleConfig)
                )
            );
        }
        return new OfficialInstallRecord(
            context.Config.AppId,
            name,
            BuildPackVersions(localVersions, bundle)
        )
        {
            AdditionalBundles = additional,
        };
    }

    /// <summary>从本地记录的逐分包版本取某画质组合的版本（本地记录缺失的置空，写入时跳过——诚实留白交给官方修复流程）。</summary>
    private static IReadOnlyList<OfficialPackVersion> BuildPackVersions(
        Dictionary<string, string> localVersions,
        GameLauncherBundle bundle
    ) =>
        bundle.ResourcePacks
            .Select(packName => new OfficialPackVersion(
                packName,
                localVersions.TryGetValue(packName, out var version) ? version : string.Empty
            ))
            .ToList();

    /// <summary>目录中是否存在任意官方记录（安装配置或资源清单）。</summary>
    public async Task<bool> HasAnyRecordAsync(string gameFolder, CancellationToken token = default)
    {
        var configPath = Path.Combine(gameFolder, InstalledConfigFileName);
        var indexPath = Path.Combine(gameFolder, LocalIndexFileName);
        return await Task.Run(() => File.Exists(configPath) || File.Exists(indexPath), token);
    }

    /// <summary>
    /// 拉取目标版本全量文件清单，语义与官方多画质并存对齐：
    /// 主 bundle + 全部附加画质组合分别拉取 verification（已展开 zip/patch 载体），
    /// 归并 group 载体的 DstFiles，按 dest 去重合并（common 为各画质共有分包，
    /// 索引内容天然一致自动合一；各画质独有分包互相补充），得到目录实际承载的全部文件台账。
    /// </summary>
    private async Task<List<GameFileInfo>> FetchTargetManifestAsync(
        OfficialInstallRecord record,
        CancellationToken token
    )
    {
        var context = await ResolveContextAsync(token);
        if (context is null)
            throw new InvalidOperationException("游戏上下文不可用。");
        var version = record.Packs[0].Version;
        var bundles = new[] { record.BundleName }
            .Concat(record.AdditionalBundles.Select(x => x.BundleName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var files = new List<GameFileInfo>();
        foreach (var bundleName in bundles)
        {
            token.ThrowIfCancellationRequested();
            var parameter = new GameResourceParameter { BundleName = bundleName };
            var verification = await context.GetVerificationResourceAsync(version, parameter, token);
            files.AddRange(verification.DefaultResource);
            foreach (var group in verification.PatchResources.Where(x => x.IsGroup))
                files.AddRange(group.DstFiles);
        }
        return files;
    }

    /// <summary>生成安装配置的临时文件（不替换目标；合并语义：保留其他 bundle 节点与 appId，覆盖当前 bundle/旧协议顶层版本）。</summary>
    private async Task StageInstalledConfigAsync(
        string gameFolder,
        OfficialInstallRecord record,
        List<(string TempPath, string TargetPath, string RelativePath)> staged,
        List<string> failed,
        CancellationToken token
    )
    {
        var path = Path.Combine(gameFolder, InstalledConfigFileName);
        OfficialInstalledConfig config = new();
        if (File.Exists(path))
        {
            config =
                await DeserializeAsync(
                    path,
                    OfficialLauncherJsonContext.Default.OfficialInstalledConfig,
                    token
                ) ?? new OfficialInstalledConfig();
        }
        config.AppId = record.AppId;
        if (record.IsLegacy)
        {
            // 旧协议：顶层 version/state 即官方旧格式的全部安装状态。
            config.Version = record.Packs[0].Version;
            config.State = string.Empty;
        }
        else
        {
            config.Bundles[record.BundleName] = new OfficialBundleInstalledInfo
            {
                Version = record.Packs[0].Version,
                State = string.Empty,
                ResourcePacks = record.Packs.Select(x => x.Name).ToList(),
            };
            // 多画质并存：同目录已安装的其他画质组合一并登记 bundle 节点
            //（与官方 kr_game_temp.bin 按 (resId, bundleName) 多条目录绑定的语义一致）。
            foreach (var additional in record.AdditionalBundles)
            {
                config.Bundles[additional.BundleName] = new OfficialBundleInstalledInfo
                {
                    Version = additional.Packs[0].Version,
                    State = string.Empty,
                    ResourcePacks = additional.Packs.Select(x => x.Name).ToList(),
                };
            }
        }
        await StageFileAsync(
            path,
            JsonSerializer.Serialize(config, OfficialLauncherJsonContext.Default.OfficialInstalledConfig),
            InstalledConfigFileName,
            staged,
            failed,
            token
        );
    }

    /// <summary>生成分包版本文件的临时文件（不替换目标；内容与目标一致的幂等文件直接计入成功、不生成临时文件）。</summary>
    private async Task StagePackVersionsAsync(
        string gameFolder,
        OfficialInstallRecord record,
        List<(string TempPath, string TargetPath, string RelativePath)> staged,
        List<string> failed,
        CancellationToken token
    )
    {
        if (record.IsLegacy)
            return;
        var dir = Path.Combine(gameFolder, PackVersionDirName);
        Directory.CreateDirectory(dir);
        foreach (var pack in record.Packs)
        {
            if (string.IsNullOrWhiteSpace(pack.Name) || string.IsNullOrWhiteSpace(pack.Version))
                continue;
            var path = Path.Combine(dir, pack.Name + ".json");
            var content = JsonSerializer.Serialize(
                new OfficialPackVersionConfig { PackName = pack.Name, Version = pack.Version },
                OfficialLauncherJsonContext.Default.OfficialPackVersionConfig
            );
            if (
                File.Exists(path)
                && (await File.ReadAllTextAsync(path, token)).Trim() == content.Trim()
            )
            {
                // 幂等：目标已是期望内容，无需替换，直接计入成功。
                staged.Add((string.Empty, path, $"{PackVersionDirName}/{pack.Name}.json"));
                continue;
            }
            await StageFileAsync(
                path,
                content,
                $"{PackVersionDirName}/{pack.Name}.json",
                staged,
                failed,
                token
            );
        }
    }

    /// <summary>把内容写入 &lt;目标&gt;.&lt;guid&gt;.tmp 临时文件（不替换目标）；失败计入 failed。</summary>
    private async Task StageFileAsync(
        string path,
        string content,
        string relativePath,
        List<(string TempPath, string TargetPath, string RelativePath)> staged,
        List<string> failed,
        CancellationToken token
    )
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(tempPath, content, token);
            staged.Add((tempPath, path, relativePath));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.WriteWarning($"官方记录生成失败：{path}；{ex.Message}");
            failed.Add(relativePath);
        }
    }

    private void TryDelete(string path, List<string> removed, List<string> failed)
    {
        try
        {
            if (!File.Exists(path))
                return;
            File.Delete(path);
            removed.Add(Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Logger.WriteWarning($"官方记录删除失败：{path}；{ex.Message}");
            failed.Add(Path.GetFileName(path));
        }
    }

    private static async Task<bool> TryWriteAtomicAsync(
        string path,
        string content,
        CancellationToken token
    )
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, content, token);
                File.Move(tempPath, path, true);
                return true;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // 尽力清理临时文件。
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<T?> DeserializeAsync<T>(
        string path,
        JsonTypeInfo<T> typeInfo,
        CancellationToken token
    )
    {
        try
        {
            var content = await File.ReadAllTextAsync(path, token);
            return JsonSerializer.Deserialize(content, typeInfo);
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return default;
        }
    }

    /// <summary>与 GameLogService 相同的上下文解析：读取最近一次在游戏页选择的鸣潮上下文。</summary>
    private async Task<IGameContextV2?> ResolveContextAsync(CancellationToken token)
    {
        var contextName = await AppSettings.GetWavesAutoOpenContextAsync(token);
        if (string.IsNullOrWhiteSpace(contextName))
        {
            contextName = nameof(WavesMainGameContextV2);
        }
        return ServiceProvider.GetKeyedService<IGameContextV2>(contextName);
    }
}
