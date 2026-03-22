using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Services;

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
    public async Task WriteDownloadComplateAsync(IGameEventPublisher gameEventPublisher,bool isSync = false)
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
                source.ResourceDefault.Version
            );
        }
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LocalGameVersion,
            source.ResourceDefault.Version
        );
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LocalGameUpdateing,
            "False"
        );

        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.GameLauncherBassProgram,
            $"{installFolder}\\{this.kuroGameApiConfig.GameExeName}"
        );
    }


    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }
}
