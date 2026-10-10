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
    public async Task WriteDownloadComplateAsync(IGameEventPublisher<GameContextOutputArgs> gameEventPublisher,bool isSync = false, CancellationToken token = default)
    {
        var installFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder, token
        );

        await this.GameLocalConfig.SaveConfigsAsync(
            new Dictionary<string, string>
            {
                [GameLocalSettingName.LocalGameVersion] = targetVersion,
                [GameLocalSettingName.LocalGameUpdateing] = "False",
                [GameLocalSettingName.GameLauncherBassProgram] =
                    $"{installFolder}\\{this.kuroGameApiConfig.GameExeName}",
            }, token
        );
    }

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

    public async Task WriteDownloadAndUpDateResultAsync(InstallOption option, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var installFolder = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder, token);
        var values = new Dictionary<string, string>
        {
            [GameLocalSettingName.LocalGameVersion] = targetVersion,
            [GameLocalSettingName.LocalGameUpdateing] = "False",
            [GameLocalSettingName.ProdIsAdvance] = option.IsAdvance ? "True" : "False",
            [GameLocalSettingName.GameLauncherBassProgram] = Path.Combine(installFolder!, kuroGameApiConfig.GameExeName)
        };
        if (option.IsProd || option.IsAdvance)
        {
            values[GameLocalSettingName.ProdDownloadFolderDone] = "False";
            values[GameLocalSettingName.ProdDownloadPath] = "";
            values[GameLocalSettingName.ProdDownloadVersion] = "";
        }
        await GameLocalConfig.SaveConfigsAsync(values, token);
    }
}
