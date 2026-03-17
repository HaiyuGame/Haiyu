using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Common;
using Waves.Core.Contracts;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.GameContext;

public abstract partial class KuroGameContextBase : IGameContext
{
    #region _filed

    private bool isLimtSpeed;
    private CancellationTokenSource _downloadCTS;
    private bool _isDownload;
    #endregion

    #region Property
    public IHttpClientService HttpClientService { get; set; }

    public LoggerService Logger { get; set; }
    public KuroGameApiConfig Config { get; private set; }
    public string ContextName { get; }
    public string GamerConfigPath { get; set; }

    public GameLocalConfig GameLocalConfig { get; private set; }

    public bool IsDx11Launche { get; private set; }

    public bool IsLimitSpeed
    {
        get => isLimtSpeed;
        set { this.isLimtSpeed = value; }
    }

    public int SpeedValue { get; private set; }

    public Process NowProcess { get; private set; }

    public virtual Type ContextType { get; }

    public virtual GameType GameType { get; }

    public virtual string GameContextNameKey { get; }

    #endregion


    internal KuroGameContextBase(KuroGameApiConfig config, string contextName)
    {
        Logger = new LoggerService();
        Config = config;
        ContextName = contextName;
    }

    public virtual async Task InitAsync()
    {
        this.HttpClientService.BuildClient();
        Directory.CreateDirectory(GamerConfigPath);
        this.GameLocalConfig = new GameLocalConfig(GamerConfigPath + "\\Settings.bat");
        var logPath = GamerConfigPath + "\\logs\\log.log";
        Logger.InitLogger(logPath, Serilog.RollingInterval.Day);
        await InitSettingAsync();
    }

    private async Task InitSettingAsync()
    {
        var config = await this.ReadContextConfigAsync();
        if (config.LimitSpeed > 0)
        {
            this.SpeedValue = config.LimitSpeed;
            this.IsLimitSpeed = true;
        }
    }

    public async Task<GameContextStatus> GetGameContextStatusAsync(
        CancellationToken token = default
    )
    {
        GameContextStatus status = new GameContextStatus();
        status.LasterArgs = this._lastOutputArgs;
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
        if (Directory.Exists(gameBaseFolder) )
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
                //预下载是否完成，确保本地安装完成
                if (
                    indexSource.Predownload != null
                    && status.IsGameExists == true
                    && status.IsGameInstalled == true
                )
                {
                    status.IsProdownPause =
                        _prodDownloadState == null ? false : _prodDownloadState.IsPaused;
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
                    status.PredownloaAcion = _prodDownloadState != null?_prodDownloadState.IsActive:false;
                }
            }
        }
        //下载游戏和预下载只存在一个状态，不需要分离状态
        if (_downloadState != null)
        {
            status.IsPause = this._downloadState.IsPaused;
            status.IsAction = this._downloadState.IsActive;
        }
        status.Gameing = this._isStarting;
        return status;
    }

    public virtual async Task DeleteResourceAsync()
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
        await SetNoneStatusAsync().ConfigureAwait(false);
    }

    public void UpdateLogCore(CoreLogOption coreLogOption)
    {
        this.Logger.Option = coreLogOption;
    }
}
