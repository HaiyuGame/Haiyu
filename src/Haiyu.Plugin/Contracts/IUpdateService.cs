using Haiyu.Plugin.Models;
using System;
using System.Threading.Tasks;

namespace Haiyu.Plugin.Contracts;

/// <summary>
/// Mirror 酱 更新服务接口
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 检查应用是否更新
    /// </summary>
    /// <returns></returns>
    public Task<bool> CheckProgramUpdateAsync(string currentVersion);

    /// <summary>
    /// 检查应用最后更新信息
    /// </summary>
    /// <returns></returns>
    public Task<DisplayVersionInfo?> GetLasterProgramInfoAsync();

    /// <summary>
    /// 下载应用更新信息
    /// </summary>
    /// <param name="progress"></param>
    /// <returns></returns>
    public Task DownloadProgramInfoAsync(IProgress<double> progress);

    /// <summary>
    /// 开始安装
    /// </summary>
    /// <returns></returns>
    public Task StartInstallProgramAsync();
}
