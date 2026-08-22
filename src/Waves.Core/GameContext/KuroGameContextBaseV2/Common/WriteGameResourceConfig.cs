using Waves.Core.Models.Options;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 执行某些任务结束后写入的数据信息
/// </summary>
public class WriteGameResourceConfig : IAsyncDisposable
{
    private readonly GameLocalConfig GameLocalConfig;
    private readonly GameLauncherSource source;
    private readonly KuroGameApiConfig kuroGameApiConfig;
    private readonly LoggerService logger;

    public WriteGameResourceConfig(GameLocalConfig config, GameLauncherSource launcherSource,KuroGameApiConfig kuroGameApiConfig, Services.LoggerService logger)
    {
        this.GameLocalConfig = config;
        this.source = launcherSource;
        this.kuroGameApiConfig = kuroGameApiConfig;
        this.logger = logger;
    }

    /// <summary>
    /// DownloadAndVerifyResource类写入后执行方法
    /// </summary>
    /// <returns></returns>
    public async Task WriteDownloadComplateAsync(IGameEventPublisher<GameContextOutputArgs> gameEventPublisher,bool isSync = false)
    {
        var installFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );

        await this.GameLocalConfig.SaveConfigsAsync(
            new Dictionary<string, string>
            {
                [GameLocalSettingName.LocalGameVersion] = source.ResourceDefault.Version,
                [GameLocalSettingName.LocalGameUpdateing] = "False",
                [GameLocalSettingName.GameLauncherBassProgram] =
                    $"{installFolder}\\{this.kuroGameApiConfig.GameExeName}",
            }
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

    public async Task WriteDownloadAndUpDateResultAsync(GameLauncherSource source, InstallOption option)
    {
        var installFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );

        if (option.IsAdvance && source.Predownload != null)
        {
            await this.GameLocalConfig.SaveConfigsAsync(
                new Dictionary<string, string>
                {
                    [GameLocalSettingName.LocalGameVersion] = source.Predownload.Version,
                    [GameLocalSettingName.ProdIsAdvance] = "True",
                    [GameLocalSettingName.ProdDownloadFolderDone] = "False",
                    [GameLocalSettingName.ProdDownloadPath] = "",
                    [GameLocalSettingName.ProdDownloadVersion] = "",
                    [GameLocalSettingName.LocalGameUpdateing] = "False",
                    [GameLocalSettingName.GameLauncherBassProgram] =
                        $"{installFolder}\\{kuroGameApiConfig.GameExeName}",
                }
            );
        }
        else
        {
            await this.GameLocalConfig.SaveConfigsAsync(
                new Dictionary<string, string>
                {
                    [GameLocalSettingName.LocalGameVersion] = source.ResourceDefault.Version,
                    [GameLocalSettingName.LocalGameUpdateing] = "False",
                    [GameLocalSettingName.GameLauncherBassProgram] =
                        $"{installFolder}\\{kuroGameApiConfig.GameExeName}",
                }
            );
        }
    }
}
