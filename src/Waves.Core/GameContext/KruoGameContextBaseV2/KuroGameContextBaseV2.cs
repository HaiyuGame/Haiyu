using Haiyu.Common;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
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
public abstract partial class KuroGameContextBaseV2:IGameContextV2
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

    public abstract string GameContextNameKey { get; }


    public abstract GameType GameType { get; }

    public abstract Type ContextType { get; }
    IHttpClientService IGameContextV2.HttpClientService { get => HttpClientService; set => HttpClientService = value; }

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
    public DownloadState? DownloadState { get; private set; }

    /// <summary>
    /// CDN测速工具
    /// </summary>
    private CDNSpeedTester _cdnSpeedTester = new();
    #endregion

    private IAsyncDisposable? _currentRunningAction;
    private bool _isStarting;


    public async Task<bool> StopCannelTaskAsync()
    {
        try
        {
            if (_currentRunningAction != null)
            {
                if (_currentRunningAction is IProgressSetup cancelTask)
                {
                    if (cancelTask.CanStop)
                    {
                        await _currentRunningAction.DisposeAsync();
                    }
                    else
                    {
                        this.GameEventPublisher.Publish(
                            new GameContextOutputArgs()
                            {
                                Type = GameContextActionType.TipMessage,
                                TipMessage = "当前任务不支持取消",
                            }
                        );
                        return true;
                    }
                }
            }
            if(DownloadState != null)
            {
                DownloadState.IsStop = true;
                DownloadState.IsActive = false;
            }
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

    public async Task<bool> PauseDownloadAsync()
    {
        if (DownloadState == null)
            return true;
        if (DownloadState.IsActive && _currentRunningAction != null)
        {
            if (_currentRunningAction is IProgressSetup cancelTask)
            {
                if (cancelTask.CanPause)
                {
                    await DownloadState.PauseAsync();
                }
                else
                {
                    this.GameEventPublisher.Publish(
                        new GameContextOutputArgs()
                        {
                            Type = GameContextActionType.TipMessage,
                            TipMessage = "当前任务不支持",
                        }
                    );
                }
            }
        }
        else
        {
            //处于其他任务，直接暂停
            await DownloadState.PauseAsync();
        }
        return true;
    }

    public async Task<bool> ResumeDownloadAsync()
    {
        if (DownloadState == null)
            return true;
        if (DownloadState.IsPaused)
        {
            await DownloadState.ResumeAsync();
        }
        return true;
    }

    public async Task SetDownloadSpeedAsync(long mbValue)
    {
        if (DownloadState == null)
            return;
        await DownloadState.SetSpeedLimitAsync(mbValue * 1024 * 1024);
    }

    public async Task<FileVersion> GetLocalFileVersionAsync(string fileName, string displayName)
    {
        var gameFolder = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder);
        var file = Directory
            .GetFiles(gameFolder, fileName, SearchOption.AllDirectories)
            .FirstOrDefault();
        if (file == null)
        {
            return new FileVersion() { DisplayName = displayName, Version = "未找到文件" };
        }
        FileVersionInfo fileinfo = FileVersionInfo.GetVersionInfo(file);
        return new FileVersion()
        {
            DisplayName = displayName,
            Subtitle = fileinfo.InternalName,
            FilePath = file,
            Version =
                $"{fileinfo.FileMajorPart}.{fileinfo.FileMinorPart}.{fileinfo.FileBuildPart}.{fileinfo.FilePrivatePart}",
        };
    }

    public async Task<FileVersion> GetLocalDLSSAsync()
    {
        return await GetLocalFileVersionAsync("nvngx_dlss.dll", "Xess");
    }
    public async Task<FileVersion> GetLocalDLSSGenerateAsync()
    {
        return await GetLocalFileVersionAsync("nvngx_dlssg.dll", "Dlss 帧生成");
    }

    public async Task<FileVersion> GetLocalXeSSGenerateAsync()
    {
        return await GetLocalFileVersionAsync("libxess.dll", "Xess");
    }

    public TimeSpan GetGameTime()
    {
        return TimeSpan.FromDays(2);
    }

    public async Task<GameContextStatus> GetGameContextStatusAsync(CancellationToken token = default)
    {
        GameContextStatus status = new GameContextStatus();
        var localVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        var gameBaseFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        var gameProgramFile = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassProgram
        );
        var updateing = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameUpdateing
        );
        if (Directory.Exists(gameBaseFolder))
        {
            status.IsGameExists = true;
        }
        if (File.Exists(gameProgramFile))
        {
            status.IsGameInstalled = true;
        }
        if (!string.IsNullOrWhiteSpace(localVersion))
        {
            status.IsLauncher = true;
        }
        var ping = (await NetworkCheck.PingAsync(KuroGameApiConfig.BaseAddress[0]));
        if (ping != null && ping.Status == IPStatus.Success)
        {
            var indexSource = await this.GetGameLauncherSourceAsync();
            if (indexSource != null)
            {
                if (localVersion != indexSource.ResourceDefault.Version)
                {
                    status.IsUpdate = true;
                    status.DisplayVersion = indexSource.ResourceDefault.Version;
                }
                else
                {
                    status.DisplayVersion = localVersion;
                }
                if (
                    !string.IsNullOrWhiteSpace(updateing)
                    && bool.TryParse(updateing, out var updateResult)
                )
                {
                    status.IsUpdateing = updateResult;
                }
                if (
                    indexSource.Predownload != null
                    && status.IsGameExists == true
                    && status.IsGameInstalled == true
                )
                {
                    status.IsProdownPause =
                        status == null ? false : status.IsPause;
                    status.IsPredownloaded = true;
                    var donwResult = await GameLocalConfig.GetConfigAsync(
                        GameLocalSettingName.ProdDownloadFolderDone
                    );
                    var prodDownVersion = await GameLocalConfig.GetConfigAsync(
                        GameLocalSettingName.ProdDownloadVersion
                    );
                    if (bool.TryParse(donwResult, out var predDown))
                    {
                        status.PredownloadedDone = predDown;
                    }
                    else
                    {
                        status.PredownloadedDone = false;
                    }
                    status.PredownloaAcion = status != null ? status.IsAction : false;
                }
            }
        }
        if (DownloadState != null)
        {
            status.IsPause = this.DownloadState.IsPaused;
            status.IsAction = this.DownloadState.IsActive;
        }
        status.Gameing = this._isStarting;
        return status;
    }


    public async Task ReEmitLastOutputAsync(bool isPred = false)
    {
        this.GameEventPublisher.Publish(this.ProgressState.LastArgs);
    }

    public Task<List<KRSDKLauncherCache>?> GetLocalGameOAuthAsync(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StartGameAsync()
    {
        throw new NotImplementedException();
    }

    public async Task DeleteResourceAsync()
    {
        var folder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        await Task.Run(() =>
        {
            Directory.Delete(folder, true);
        });
        await this.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, "");
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.GameLauncherBassProgram,
            ""
        );
        await this.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameVersion, "");
    }

    public Task<QueryPlayerInfo?> QueryPlayerInfoAsync(string oAutoCode, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task<QueryRoleInfo?> QueryRoleInfoAsync(string oautoCode, string playerId, string region, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}
