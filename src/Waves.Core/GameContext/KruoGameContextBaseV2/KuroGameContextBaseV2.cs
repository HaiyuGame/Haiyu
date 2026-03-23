using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;
using Haiyu.Common;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Common;
using Waves.Core.Contracts;
using Waves.Core.Contracts.Events;
using Waves.Core.GameContext.KruoGameContextBaseV2.Common;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.GameContext;

/// <summary>
/// 库洛游戏核心上下文基类V2，重构版本，增强结构性
/// </summary>
public abstract partial class KuroGameContextBaseV2
{
    private bool isLimtSpeed;

    /// <summary>
    /// 阻塞用户启动的下载发布器
    /// </summary>
    public IGameEventPublisher GameEventPublisher { get; internal set; }

    /// <summary>
    /// Http 请求服务，包含下载Client与配置Client
    /// </summary>
    public IHttpClientService HttpClientService { get; internal set; }

    /// <summary>
    /// CDN测试工具
    /// </summary>
    public CDNSpeedTester CDNSpeedTester { get; private set; }

    /// <summary>
    /// 日志组件
    /// </summary>
    public LoggerService Logger { get; set; }

    /// <summary>
    /// API配置项，包含游戏相关的API地址与参数等
    /// </summary>
    public KuroGameApiConfig Config { get; private set; }

    public string ContextName { get; }

    /// <summary>
    /// 核心配置文件读取
    /// </summary>
    public GameLocalConfig GameLocalConfig { get; private set; }
    public object SpeedValue { get; private set; }

    /// <summary>
    /// 是否限速
    /// </summary>
    public bool IsLimitSpeed
    {
        get => isLimtSpeed;
        set { this.isLimtSpeed = value; }
    }

    /// <summary>
    /// 游戏配置文件夹
    /// </summary>
    public string GamerConfigPath { get; set; }

    /// <summary>
    /// 核心进度状态跟踪器，聚合内部事件，供UI初始读取和长效绑定的最新状态
    /// </summary>
    public GameProgressTracker ProgressState { get; } = new();

    public KuroGameContextBaseV2(KuroGameApiConfig config, string contextName)
    {
        Logger = new LoggerService();
        Config = config;
        ContextName = contextName;
    }

    /// <summary>
    /// 初始化配置核心
    /// </summary>
    /// <returns></returns>
    public virtual async Task InitAsync()
    {
        this.HttpClientService.BuildClient();
        Directory.CreateDirectory(GamerConfigPath);
        this.GameLocalConfig = new GameLocalConfig(GamerConfigPath + "\\Settings.bat");
        var logPath = GamerConfigPath + "\\logs\\log.log";
        Logger.InitLogger(logPath, Serilog.RollingInterval.Day);
        CDNSpeedTester = new CDNSpeedTester();
        if (this.GameEventPublisher != null)
        {
            await ProgressState.StartTrackingAsync(this.GameEventPublisher);
        }

        await InitSettingAsync();
    }

    /// <summary>
    /// 初始化配置项目
    /// </summary>
    /// <returns></returns>
    private async Task InitSettingAsync()
    {
        var config = await this.ReadContextConfigAsync();
        if (config.LimitSpeed > 0)
        {
            this.SpeedValue = config.LimitSpeed;
            this.IsLimitSpeed = true;
        }
    }

    /// <summary>
    /// 读取核心配置项目，包含一些持久化参数
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<GameContextConfig> ReadContextConfigAsync(CancellationToken token = default)
    {
        GameContextConfig config = new();
        var speed = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.LimitSpeed);
        var dx11 = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.IsDx11);
        if (int.TryParse(speed, out var rate))
        {
            config.LimitSpeed = rate;
        }
        else
            config.LimitSpeed = 0;
        if (string.IsNullOrWhiteSpace(dx11))
            config.IsDx11 = false;
        if (bool.TryParse(dx11, out var isDx11))
        {
            config.IsDx11 = isDx11;
        }
        else
            config.IsDx11 = false;
        return config;
    }

    #region 下载字段
    /// <summary>
    /// 阻塞用户启动的下载状态管理
    /// </summary>
    private DownloadState? _downloadState = null;

    /// <summary>
    /// CDN测速工具
    /// </summary>
    private CDNSpeedTester _cdnSpeedTester = new();
    #endregion

    #region 拆解下载


    #endregion

    private IAsyncDisposable? _currentRunningAction;
    private readonly SemaphoreSlim _actionLock = new(1, 1); // 结合锁，防并发冲突
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
        Task.Run(async () => await StartDownloadAsync(folder, launcher, token));
        return true;
    }

    private async Task<bool> StartDownloadAsync(
        string folder,
        GameLauncherSource launcher,
        CancellationToken token
    )
    {
        await _actionLock.WaitAsync();
        try
        {
            if (_currentRunningAction != null)
            {
                await _currentRunningAction.DisposeAsync();
            }
            this.Setups = new List<string>();
            Setups.Add("下载与校验数据");
            Setups.Add("保存数据信息");
            var downloadMethod = new DownloadAndVerifyResource(this.Logger);
            var resource = await GetGameResourceAsync(launcher.ResourceDefault, token);
            if (resource == null)
                return false;
            HttpClientService?.BuildClient();
            _downloadState = new DownloadState();
            downloadMethod = new(this.Logger);
            downloadMethod.ProgressName = "下载与校验数据";
            await GameEventPublisher.PublisAsync(
                GameContextActionType.CdnSelect,
                "正在选择最优CDN"
            );
            var cdnResult = await TestCdnAsync(launcher.ResourceDefault.CdnList, launcher.ResourceDefault.Config.BaseUrl, resource.Resource);
            if(cdnResult == null)
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
                    { "downloadState", _downloadState! },
                    { "baseUrl", baseUrl },
                    { "isProd", false },
                },
                this.GameEventPublisher
            );
            _currentRunningAction = downloadMethod;
            await GameEventPublisher.PublisAsync(GameContextActionType.CdnSelect, "CDN选择完毕");
            this.CurrentSetups = 0;
            await this.GameEventPublisher.PublishStepAsync("下载与校验数据", CurrentSetups, Setups);
            await downloadMethod.RunAsync(true);
            var writeConfig = new WriteGameResourceConfig(
                this.GameLocalConfig,
                launcher,
                this.Config,
                Logger
            );
            _currentRunningAction = writeConfig;
            this.CurrentSetups = 1;
            await this.GameEventPublisher.PublishStepAsync("写入配置信息", CurrentSetups, Setups);
            await writeConfig.WriteDownloadComplateAsync(this.GameEventPublisher, true);
            //通知UI刷新
            GameEventPublisher.Publish(
                new GameContextOutputArgs() { Type = GameContextActionType.None }
            );
            return true;
        }
        finally
        {
            _actionLock.Release();
        }
    }

    public async Task<bool> RepairGameAsync(CancellationToken token = default)
    {
        var folder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        if (folder == null)
        {
            return false;
        }
        await StartDownloadTaskAsync(folder, true, token);
        return true;
    }

    public async Task<CdnTestResult?> TestCdnAsync(
        List<CdnList> cdnList,
        string baseUrl,
        List<IndexResource> resource
    )
    {
        if (resource == null || !resource.Any()) return null;

        const long targetTestSize = 50L * 1024 * 1024;

        var item = resource.MinBy(x => Math.Abs((long)x.Size - targetTestSize));
        item ??= resource.MinBy(x => x.Size);
        if (item == null || string.IsNullOrWhiteSpace(item.Dest))
        {
            return null;
        }

        // 修复找不到文件错误：安全地拼接 URL
        var testUrl = baseUrl.TrimEnd('/') + "/" + item.Dest.TrimStart('/');
        var best = await CDNSpeedTester.TestAllAsync(
            cdnList,
            testUrl,
            TimeSpan.FromSeconds(40)
        );
        return best;
    }

    #endregion
    #region 更新方法
    public async Task<bool> UpdateGameResourceAsync()
    {
        var _launcher = await this.GetGameLauncherSourceAsync();
        var currentVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        Task.Run(async () => await StartDownloadUpdateGameResourceAsync(_launcher, currentVersion));
        return true;
    }

    private async Task<bool> StartDownloadUpdateGameResourceAsync(
        GameLauncherSource _launcher,
        string currentVersion,
        bool isProd = false
    )
    {
        await _actionLock.WaitAsync();
        try
        {
            #region 获取配置
            if (_launcher == null || string.IsNullOrWhiteSpace(currentVersion))
            {
                GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = Models.Enums.GameContextActionType.TipMessage,
                        TipMessage = "未找到更新配置文件，无法进行下载",
                    }
                );
                return false;
            }
            var baseFolder = await this.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder
            );
            var previous = _launcher
                .ResourceDefault.Config.PatchConfig.Where(x => x.Version == currentVersion)
                .FirstOrDefault();
            if (previous == null)
            {
                GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = Models.Enums.GameContextActionType.TipMessage,
                        TipMessage = "未找到更新配置文件，无法进行下载",
                    }
                );
                return false;
            }
            var cdnUrl =
                _launcher
                    .ResourceDefault.CdnList.Where(x => x.P != 0)
                    .OrderBy(x => x.P)
                    .FirstOrDefault() ?? null;
            if (cdnUrl == null)
            {
                GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = Models.Enums.GameContextActionType.TipMessage,
                        TipMessage = "未找到更新配置文件，无法进行下载",
                    }
                );
                return false;
            }
            var _patch = await GetPatchGameResourceAsync(cdnUrl.Url + previous.IndexFile);
            if (_patch == null)
            {
                GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = Models.Enums.GameContextActionType.TipMessage,
                        TipMessage = "未找到更新配置文件，无法进行下载",
                    }
                );
                return false;
            }
            #endregion

            this._downloadState = new DownloadState();

            var downloadResource = new List<IndexResource>();
            var patchResource = new List<IndexResource>();
            var groupResource = new List<IndexResource>();
            var zipResource = new List<IndexResource>();

            foreach (var x in _patch.Resource)
            {
                if (x.Dest.Contains("krdiff")) patchResource.Add(x);
                else if (x.Dest.Contains("krpdiff")) groupResource.Add(x);
                else if (x.Dest.Contains("krzip")) zipResource.Add(x);
                else downloadResource.Add(x);
            }

            var downloadBaseFolder = Path.Combine(baseFolder, "downloads");
            DownloadUpdateFolderConfig folderConfig = new();
            await GameEventPublisher.PublisAsync(
                GameContextActionType.CdnSelect,
                "正在选择最优CDN"
            );
            // 避免 patchResource 为空导致无文件测速报错，这里改用全资源测速
            var cdnResult = await TestCdnAsync(_launcher.ResourceDefault.CdnList, previous.BaseUrl, _patch.Resource);
            if(cdnResult == null)
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
            var baseUrl = cdnResult.Value.Url + previous.BaseUrl;
            #region 初始化步骤显示
            var downloadTasks = new List<(IEnumerable<IndexResource> Items, string Name, string Folder)>();
            if (patchResource.Any())
            {
                this.Setups.Add("下载补丁文件");
                folderConfig.PatchFolder = Path.Combine(downloadBaseFolder, "patchs");
                downloadTasks.Add((patchResource, "下载补丁文件", folderConfig.PatchFolder));
            }
            if (groupResource.Any())
            {
                this.Setups.Add("下载补丁组文件");
                folderConfig.PatchGroupFolder = Path.Combine(downloadBaseFolder, "patchGroup");
                downloadTasks.Add((groupResource, "下载补丁组文件", folderConfig.PatchGroupFolder));
            }
            if (zipResource.Any())
            {
                this.Setups.Add("下载压缩包更新文件");
                folderConfig.ZipFolder = Path.Combine(downloadBaseFolder, "zips");
                downloadTasks.Add((zipResource, "下载压缩包更新文件", folderConfig.ZipFolder));
            }
            if (downloadResource.Any())
            {
                this.Setups.Add("下载更新文件");
                folderConfig.DownloadFolder = Path.Combine(downloadBaseFolder, "downloads");
                downloadTasks.Add((downloadResource, "下载更新文件", folderConfig.DownloadFolder));
            }
            #endregion
            for (int i = 0; i < downloadTasks.Count; i++)
            {
                var downloadMethod = new DownloadAndVerifyResource(this.Logger)
                {
                    ProgressName = downloadTasks[i].Name
                };

                downloadMethod.SetParam(
                    new Dictionary<string, object>()
                    {
                        { "resource", downloadTasks[i].Items },
                        { "launcher", _launcher },
                        { "isDelete", false },
                        { "folder", downloadTasks[i].Folder },
                        { "httpClient", HttpClientService! },
                        { "downloadState", _downloadState! },
                        { "baseUrl", baseUrl },
                        { "isProd", isProd },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = downloadMethod;
                CurrentSetups = i;
                await this.GameEventPublisher.PublishStepAsync(downloadTasks[i].Name, CurrentSetups, Setups);
                await downloadMethod.RunAsync(true);
            }
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _actionLock.Release();
        }
    }
    #endregion


    public async Task<bool> StopCannelTaskAsync()
    {
        try
        {
            if (_currentRunningAction != null)
                await _currentRunningAction.DisposeAsync();
            this.GameEventPublisher.Publish(
                new GameContextOutputArgs() { Type = GameContextActionType.None }
            );
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteError($"取消任务失败:{ex.Message}");
            return false;
        }
    }

    public async Task PauseDownloadAsync()
    {
        if (_downloadState == null)
            return;
        if (_downloadState.IsActive)
        {
            await _downloadState.PauseAsync();
        }
    }

    public async Task ResumeDownloadAsync()
    {
        if (_downloadState == null)
            return;
        if (_downloadState.IsPaused)
        {
            await _downloadState.ResumeAsync();
        }
    }

    public async Task SetDownloadSpeedAsync(long mbValue)
    {
        if (_downloadState == null)
            return;
        await _downloadState.SetSpeedLimitAsync(mbValue * 1024 * 1024);
    }
}
