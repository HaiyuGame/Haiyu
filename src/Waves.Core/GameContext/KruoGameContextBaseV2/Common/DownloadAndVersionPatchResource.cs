using Haiyu.Common;
using System.Text.RegularExpressions;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Common;
using Waves.Core.Common.Downloads;
using Waves.Core.Contracts;
using Waves.Core.Contracts.Events;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 下载更新资源类，包含下载过程中需要用到的资源，如下载器、校验器等，此方法可通用与预下载和下载更新资源
/// </summary>
public sealed class DownloadAndVersionPatchResource : IProgressSetup, IAsyncDisposable
{
    private IGameEventPublisher gameEventPublisher;

    public Dictionary<string, object> Param { get; private set; }

    #region 传入参数
    private GameLauncherSource _launcher;
    private string _localVersion;
    private DownloadState _downloadState;
    private bool _isProd;
    private string _folder;
    private object _totalfileSize;
    private int _totalFileTotal;
    private long _totalProgressSize;
    private long _totalProgressTotal;
    private int _totalVerifiedBytes;
    private int _totalDownloadedBytes;
    private PatchIndexGameResource _patch;
    private IHttpClientService _httpClientService;
    #endregion
    public DownloadAndVersionPatchResource(Services.LoggerService logger)
    {
        this.Logger = logger;
        this.ProgressName = "下载更新数据";
    }

    public LoggerService Logger { get; }
    public string ProgressName { get; set; }
    public double ProgressValue { get; set; }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher)
    {
        this.gameEventPublisher = gameEventPublisher;
        this.Param = param;
    }

    public async Task<bool> CheckAsync(Dictionary<string, object> param)
    {
        if (!param.CheckParam<GameLauncherSource>("launcher", out var launcher))
        {
            return false;
        }
        if (!param.CheckParam<string>("localVersion", out var localVersion))
        {
            return false;
        }
        if (!param.CheckParam<string>("folder", out var folder))
        {
            return false;
        }
        if (!param.CheckParam<DownloadState>("downloadState", out var downloadState))
        {
            return false;
        }
        if (!param.CheckParam<PatchIndexGameResource>("patch", out var patch))
        {
            return false;
        }
        if (!param.CheckParam<bool>("isProd", out var isProd))
        {
            return false;
        }
        if(!param.CheckParam<IHttpClientService>("httpClient",out var httpClient))
        {
            return false;
        }
        this._launcher = launcher!;
        this._localVersion = localVersion!;
        this._folder = folder!;
        this._downloadState = downloadState!;
        this._isProd = isProd;
        this._patch = patch!;
        this._httpClientService = httpClient!;
        return true;
    }

    public async Task RunAsync(bool isSync = false)
    {
        // 下载结构
        var downloadFolder = Path.Combine(_folder, "downloads");
        Directory.CreateDirectory(downloadFolder);
        // 取资源部分
        var downloadResource = _patch.Resource.Where(x =>
            !x.Dest.Contains("krpdiff") || !x.Dest.Contains("krdiff") || !x.Dest.Contains("krzip")
        );
        if (_patch.PatchInfos != null && _patch.PatchInfos.Count > 0) 
        {
        
        }
    }



    public void InitProgress()
    {
        _totalfileSize = 0L;
        _totalFileTotal = 0;
        _totalProgressSize = 0L;
        _totalProgressTotal = 0L;
        _totalVerifiedBytes = 0;
        _totalDownloadedBytes = 0;
    }

    /// <summary>
    /// 取消接口
    /// </summary>
    /// <returns></returns>
    public async Task CancelAsync() { }
}
