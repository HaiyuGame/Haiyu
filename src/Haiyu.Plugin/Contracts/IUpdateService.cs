using System;
using System.Threading;
using System.Threading.Tasks;
using Haiyu.Plugin.Models;

namespace Haiyu.Plugin.Contracts;

/// <summary>
/// 应用更新源API
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 检查应用是否更新
    /// </summary>
    /// <returns></returns>
    public Task<bool> CheckProgramUpdateAsync(
        string currentVersion,
        CancellationToken token = default
    );

    /// <summary>
    /// 检查应用最后更新信息
    /// </summary>
    /// <returns></returns>
    public Task<DisplayVersionInfo?> GetLasterProgramInfoAsync(CancellationToken token = default);

    /// <summary>
    /// 下载应用更新信息
    /// </summary>
    /// <param name="progress"></param>
    /// <returns></returns>
    public Task<string?> DownloadProgramInfoAsync(
        IProgress<double> progress,
        CancellationToken token = default
    );

    /// <summary>
    /// 开始安装
    /// </summary>
    /// <returns></returns>
    public Task StartInstallProgramAsync();
}
