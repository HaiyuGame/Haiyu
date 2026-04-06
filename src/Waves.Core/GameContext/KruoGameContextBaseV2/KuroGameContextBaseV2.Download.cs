using Haiyu.Common;
using Waves.Api.Models;
using Waves.Core.Common;
using Waves.Core.GameContext.KruoGameContextBaseV2.Common;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

partial class KuroGameContextBaseV2
{
    private CancellationTokenSource _downloadCts = null;
    private CancellationTokenSource _prodDownloadCts = null;
    private CancellationTokenSource _installGameResourceCts = null;

    #region 下载方法

    /// <summary>
    /// 下载游戏接口
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="isDelete"></param>
    /// <returns></returns>
    public async Task<bool> StartDownloadTaskAsync(
        string folder,
        bool isDelete = false,
        CancellationToken token = default
    )
    {
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, folder);
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");
        var launcher = await this.GetGameLauncherSourceAsync(null, token);
        if (launcher == null)
        {
            this.GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    TipMessage = "未请求到游戏文件信息",
                    Type = GameContextActionType.TipMessage,
                }
            );
            return false;
        }
        Task.Run(async () => await StartDownloadAsync(folder, launcher));
        return true;
    }

    /// <summary>
    /// 开始下载
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="launcher"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> StartDownloadAsync(
        string folder,
        GameLauncherSource launcher,
        bool isRepir = false
    )
    {
        try
        {
            if (_currentRunningAction != null)
            {
                await _currentRunningAction.DisposeAsync();
            }
            this.Setups = new List<string>();
            Setups.Add("下载校验");
            Setups.Add("保存数据");
            var downloadMethod = new DownloadAndVerifyResource(this.Logger);
            var resource = await GetGameResourceAsync(launcher.ResourceDefault);
            if (resource == null)
                return false;
            HttpClientService?.BuildClient();
            _downloadCts = new CancellationTokenSource();
            var state = GetInitDownloadState(false);
            state.CancelToken = _downloadCts;
            state.IsActive = true;
            downloadMethod = new(this.Logger);
            downloadMethod.ProgressName = "下载校验";
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = GameContextActionType.CdnSelect,
                    TipMessage = "正在选择最优CDN",
                    Prod = false,
                }
            );
            var cdnResult = await TestCdnAsync(
                launcher.ResourceDefault.CdnList,
                launcher.ResourceDefault.Config.BaseUrl,
                resource.Resource
            );
            if (cdnResult == null)
            {
                this.GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = GameContextActionType.TipMessage,
                        TipMessage = "未找到可用的CDN地址，无法进行下载",
                    }
                );
                return false;
            }
            var baseUrl = cdnResult.Value.url + launcher.ResourceDefault.Config.BaseUrl;
            downloadMethod.SetParam(
                new Dictionary<string, object>()
                {
                    { "resource", resource.Resource },
                    { "launcher", launcher },
                    { "isDelete", false },
                    { "folder", folder },
                    { "httpClient", HttpClientService! },
                    { "downloadState", DownloadState! },
                    { "baseUrl", baseUrl },
                    { "isProd", false },
                },
                this.GameEventPublisher
            );
            _currentRunningAction = downloadMethod;
            await GameEventPublisher.PublisAsync(GameContextActionType.CdnSelect, "CDN选择完毕");
            this.CurrentSetups = 0;
            await this.GameEventPublisher.PublishStepAsync("下载校验", CurrentSetups, Setups);
            await downloadMethod.ExecuteAsync(true);
            var writeConfig = new WriteGameResourceConfig(
                this.GameLocalConfig,
                launcher,
                this.Config,
                Logger
            );
            _currentRunningAction = writeConfig;
            this.CurrentSetups = 1;
            if (state.CancelToken.IsCancellationRequested)
            {
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.GameLauncherBassFolder,
                    ""
                );
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

                GameEventPublisher.Publish(
                    new GameContextOutputArgs() { Type = GameContextActionType.None }
                );

                return true;
            }
            await this.GameEventPublisher.PublishStepAsync("写入配置", CurrentSetups, Setups);
            await writeConfig.WriteDownloadComplateAsync(this.GameEventPublisher, true);
            //通知UI刷新
            state.IsActive = false;
            SetCurrentStateNull(false);
            GameEventPublisher.Publish(
                new GameContextOutputArgs() { Type = GameContextActionType.None }
            );
            return true;
        }
        catch (OperationCanceledException)
        {
            SetCurrentStateNull(false);
            return false;
        }
        finally
        {
            SetCurrentStateNull(false);
        }
    }

    /// <summary>
    /// 修复游戏
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> RepairGameAsync()
    {
        var folder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );

        if (string.IsNullOrWhiteSpace(folder))
            return false;
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, folder);
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");
        var launcher = await this.GetGameLauncherSourceAsync(null);
        if (launcher == null)
        {
            this.GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    TipMessage = "未请求到游戏文件信息",
                    Type = GameContextActionType.TipMessage,
                }
            );
            return false;
        }
        Task.Run(async () => await StartDownloadAsync(folder, launcher, true));
        return true;
    }

    /// <summary>
    /// 测试CDN
    /// </summary>
    /// <param name="cdnList"></param>
    /// <param name="baseUrl"></param>
    /// <param name="resource"></param>
    /// <returns></returns>
    public async Task<CdnTestResult?> TestCdnAsync(
        List<CdnList> cdnList,
        string baseUrl,
        List<IndexResource> resource
    )
    {
        if (resource == null || !resource.Any())
            return null;

        const long targetTestSize = 50L * 1024 * 1024;

        var item = resource.MinBy(x => Math.Abs((long)x.Size - targetTestSize));
        item ??= resource.MinBy(x => x.Size);
        if (item == null || string.IsNullOrWhiteSpace(item.Dest))
        {
            return null;
        }

        // 修复找不到文件错误：安全地拼接 URL
        var testUrl = baseUrl.TrimEnd('/') + "/" + item.Dest.TrimStart('/');
        var best = await CDNSpeedTester.TestAllAsync(cdnList, testUrl, TimeSpan.FromSeconds(40));
        return best;
    }

    #endregion
}
