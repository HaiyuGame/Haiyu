using System;
using System.Threading.Tasks;

namespace Haiyu.Plugin.Contracts;

/// <summary>
/// Mirror 酱 更新服务接口
/// </summary>
public interface IMirrorUpdateService
{
    /// <summary>
    /// 检查应用是否更新
    /// </summary>
    /// <returns></returns>
    public Task CheckProgramUpdateAsync();

    /// <summary>
    /// 检查应用最后更新信息
    /// </summary>
    /// <returns></returns>
    public Task GetLasterProgramInfoAsync();

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
