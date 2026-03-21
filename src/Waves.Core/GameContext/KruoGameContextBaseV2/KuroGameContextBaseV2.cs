using Haiyu.Common;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;
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

    public IGameEventPublisher GameEventPublisher { get; internal set; }

    /// <summary>
    /// Http 请求服务，包含下载Client与配置Client
    /// </summary>
    public IHttpClientService? HttpClientService { get; internal set; }

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
    private DownloadState? _downloadState = null;

    /// <summary>
    /// CDN测速工具
    /// </summary>
    private CDNSpeedTester _cdnSpeedTester = new();
    #endregion

    #region 拆解下载

    private DownloadAndVerifyResource downloadMethod;

    #endregion



    /// <summary>
    /// 启动下载进程
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="isDelete"></param>
    /// <returns></returns>
    public async Task<bool> StartDownloadTaskAsync(string folder, bool isDelete = false,CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, folder);
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");
        var launcher = await this.GetGameLauncherSourceAsync(null, token);
        if (launcher == null)
        {
            return false;
        }
        return await StartDownloadAsync(folder,launcher,token);
    }

    private async Task<bool> StartDownloadAsync(string folder,GameLauncherSource? launcher, CancellationToken token)
    {
        if (launcher == null)
        {
            return false;
        }
        try
        {
            var resource = await GetGameResourceAsync(
                launcher.ResourceDefault,
                token
            );
            if (resource == null)
                return false;
            HttpClientService?.BuildClient();
            downloadMethod = new(this.Logger);
            downloadMethod.SetParam(new Dictionary<string, object>()
            {
                {"resource", resource.Resource },
                {"launcher", launcher },
                {"isDelete", false },
                {"folder", folder },
                {"httpClient", HttpClientService }
            },this.GameEventPublisher);
            await downloadMethod.RunAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> StopCannelTaskAsync()
    {
        try
        {
            if(downloadMethod != null)
                await downloadMethod.CancelAsync();
            return true;
        }
        catch (Exception)
        {

            throw;
        }
    }

}
