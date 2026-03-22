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
    public async Task StartDownloadTaskAsync(
        string folder,
        bool isDelete = false,
        CancellationToken token = default
    )
    {
        if (string.IsNullOrWhiteSpace(folder))
            return;
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, folder);
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");
        var launcher = await this.GetGameLauncherSourceAsync(null, token);
        if (launcher == null)
        {
            this.GameEventPublisher.Publish(new GameContextOutputArgs()
            {
                TipMessage = "未请求到游戏文件信息",
                Type = GameContextActionType.TipMessage
            });
            return;
        }
        Task.Run(async() => await StartDownloadAsync(folder, launcher, token));
    }

    public async Task<bool> RepairGameAsync(CancellationToken token = default)
    {
        var folder =  await GameLocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder);
        if(folder == null)
        {
            return false;
        }
        await StartDownloadTaskAsync(folder,true,token);
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
            downloadMethod.SetParam(
                new Dictionary<string, object>()
                {
                    { "resource", resource.Resource },
                    { "launcher", launcher },
                    { "isDelete", false },
                    { "folder", folder },
                    { "httpClient", HttpClientService! },
                    {"downloadState",_downloadState! }
                },
                this.GameEventPublisher
            );
            _currentRunningAction = downloadMethod;
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
            await writeConfig.WriteDownloadComplateAsync(this.GameEventPublisher,true);
            //通知UI刷新
            GameEventPublisher.Publish(new GameContextOutputArgs()
            {
                Type = GameContextActionType.None
            });
            return true;
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
            this.GameEventPublisher.Publish(new GameContextOutputArgs()
            {
                Type = GameContextActionType.None
            });
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
        if(_downloadState.IsActive)
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
