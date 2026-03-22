using Waves.Api.Models;
using Waves.Core.Common;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 下载更新资源类，包含下载过程中需要用到的资源，如下载器、校验器等，此方法可通用与预下载和下载更新资源
/// </summary>
public sealed class DownloadUpdateResource : IProgressSetup,IAsyncDisposable
{
    private IGameEventPublisher gameEventPublisher;

    public Dictionary<string, object> Param { get; private set; }

    #region 传入参数
    private GameLauncherSource _launcher;
    private string _localVersion;
    private DownloadState _downloadState;
    private bool _isProd;
    private string _folder;
    #endregion
    public DownloadUpdateResource()
    {
        this.ProgressName = "下载更新数据";
    }

    public string ProgressName { get; set;  }
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

    public async Task<bool> CheckAsync(Dictionary<string,object> param)
    {
        if (!param.CheckParam<GameLauncherSource>("launcher", out var launcher))
        {
            return false;
        }
        if(!param.CheckParam<string>("localVersion", out var localVersion))
        {
            return false;
        }
        if(!param.CheckParam<string>("folder",out var folder))
        {
            return false;
        }
        if(!param.CheckParam<DownloadState>("downloadState", out var downloadState))
        {
            return false;
        }
        if(!param.CheckParam<bool>("isProd",out var isProd))
        {
            return false;
        }
        this._launcher = launcher!;
        this._localVersion = localVersion!;
        this._folder = folder!;
        this._downloadState = downloadState!;
        this._isProd = isProd;
        return true;
    }



    /// <summary>
    /// 取消接口
    /// </summary>
    /// <returns></returns>
    public async Task CancelAsync()
    {

    }
}
