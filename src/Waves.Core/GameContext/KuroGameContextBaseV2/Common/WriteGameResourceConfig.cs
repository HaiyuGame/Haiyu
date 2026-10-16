using Waves.Core.Models.Options;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 执行某些任务结束后写入的数据信息
/// </summary>
public class WriteGameResourceConfig : IAsyncDisposable
{
    private readonly GameLocalConfig GameLocalConfig;
    private readonly string targetVersion;
    private readonly KuroGameApiConfig kuroGameApiConfig;
    private readonly LoggerService logger;

    public WriteGameResourceConfig(GameLocalConfig config, string targetVersion,KuroGameApiConfig kuroGameApiConfig, Services.LoggerService logger)
    {
        this.GameLocalConfig = config;
        this.targetVersion = !string.IsNullOrWhiteSpace(targetVersion) ? targetVersion : throw new ArgumentException("目标版本为空。", nameof(targetVersion));
        this.kuroGameApiConfig = kuroGameApiConfig;
        this.logger = logger;
    }

    /// <summary>
    /// DownloadAndVerifyResource类写入后执行方法
    /// </summary>
    /// <returns></returns>
    public Task WriteDownloadComplateAsync(IGameEventPublisher<GameContextOutputArgs> gameEventPublisher,
        bool isSync = false, CancellationToken token = default, GameVersionInfo? plan = null) =>
        WriteDownloadAndUpDateResultAsync(InstallOption.CreateDefault(), token, plan);

    public async Task WriteDownloadCancelAsync(IGameEventPublisher<GameContextOutputArgs> gameEventPublisher, bool isSync = false)
    {
        var currentVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        var installFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            await this.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.LocalGameVersion,
            ""
            );
        }
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LocalGameVersion,
            ""
        );
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LocalGameUpdateing,
            "False"
        );

        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.GameLauncherBassProgram,
            ""
        );
    }
    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task WriteDownloadAndUpDateResultAsync(InstallOption option, CancellationToken token = default, GameVersionInfo? plan = null)
    {
        token.ThrowIfCancellationRequested();
        var installFolder = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder, token);
        if (string.IsNullOrWhiteSpace(installFolder))
            throw new InvalidOperationException("安装目录为空，未提交版本配置。");
        if (plan is not null && plan.NewGameVersion != targetVersion)
            throw new InvalidOperationException("待提交版本与已校验清单的目标版本不一致。");
        var values = new Dictionary<string, string>
        {
            [GameLocalSettingName.LocalGameVersion] = targetVersion,
            [GameLocalSettingName.LocalGameUpdateing] = "False",
            [GameLocalSettingName.ProdIsAdvance] = option.IsAdvance ? "True" : "False",
            [GameLocalSettingName.GameLauncherBassProgram] = Path.Combine(installFolder!, kuroGameApiConfig.GameExeName)
        };
        if (plan?.ResourceVersion == "2")
        {
            if (string.IsNullOrWhiteSpace(plan.BundleName) || plan.ResourcePacks.Count == 0)
                throw new InvalidOperationException("分包安装清单缺少资源组合或分包版本，未提交版本配置。");
            values[GameLocalSettingName.BunleName] = plan.BundleName;
            foreach (var pack in plan.ResourcePacks)
            {
                if (string.IsNullOrWhiteSpace(pack.Name) || string.IsNullOrWhiteSpace(pack.TargetVersion))
                    throw new InvalidOperationException("分包目标版本为空，未提交版本配置。");
                values[GameLocalSettingName.GetBunlePackVersionKey(pack.Name)] = pack.TargetVersion;
            }
        }
        if (option.IsProd || option.IsAdvance)
        {
            values[GameLocalSettingName.ProdDownloadFolderDone] = "False";
            values[GameLocalSettingName.ProdDownloadPath] = "";
            values[GameLocalSettingName.ProdDownloadVersion] = "";
        }
        // 主版本、逐包版本及任务标记在同一次原子配置替换中提交。
        // SaveConfigsAsync 会以 false 返回磁盘写入失败，必须将失败传播给任务。
        if (!await GameLocalConfig.SaveConfigsAsync(values, token))
            throw new IOException($"版本配置保存失败：{GameLocalConfig.SettingPath}。目标版本：{targetVersion}；保留下载缓存。");
        logger.WriteInfo($"安装配置已提交，主版本：{targetVersion}；分包：{string.Join(", ", plan?.ResourcePacks.Select(x => $"{x.Name}={x.TargetVersion}") ?? [])}");
    }
}
