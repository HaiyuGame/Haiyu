using Waves.Core.Models;
using Waves.Core.Models.OfficialLauncher;

namespace Waves.Core.Contracts;

/// <summary>
/// 官方启动器安装记录工具：向游戏目录写入官方启动器（KRLauncher）格式的
/// launcherDownloadConfig.json、launcherDownloadConfig/&lt;pack&gt;.json 与 LocalGameResources.json，
/// 使官方启动器"定位游戏目录"时识别为已安装，避免触发全量重下。
/// 独立工具，不与 Haiyu 下载/更新流程耦合。
/// </summary>
public interface IOfficialLauncherRecordService
{
    /// <summary>
    /// 定位最近一次在游戏页选择的游戏目录，并读取目录当前状态（官方记录是否存在）。
    /// </summary>
    Task<OfficialRecordLocateResult> LocateGameFolderAsync(CancellationToken token = default);

    /// <summary>
    /// 生成安装记录（appId、bundle 名、逐包版本），逐包版本取服务器当前配置；
    /// 旧协议固定 default 包。多画质并存时已安装的其他画质组合一并返回（AdditionalBundles）。
    /// </summary>
    Task<OfficialInstallRecord?> BuildInstallRecordAsync(CancellationToken token = default);

    /// <summary>
    /// 写入安装记录（launcherDownloadConfig.json + 分包版本文件）。
    /// 合并写：保留目录中已有的其他 bundle 节点；分包版本文件内容相同则跳过。
    /// 单文件失败不中断，结果逐项汇总。
    /// </summary>
    Task<OfficialRecordWriteResult> WriteInstallRecordAsync(string gameFolder, OfficialInstallRecord record, CancellationToken token = default);

    /// <summary>
    /// 从官方 CDN 拉取目标版本全量文件清单并写入 LocalGameResources.json（整文件原子重写）。
    /// 清单体积较大（数十万条目），建议仅在需要时执行；失败不影响已写入的安装记录。
    /// </summary>
    Task<OfficialRecordWriteResult> WriteResourceIndexAsync(string gameFolder, OfficialInstallRecord record, CancellationToken token = default);

    /// <summary>
    /// 删除官方记录文件：安装配置与资源清单整文件删除；
    /// 分包版本目录只删除 .json 记录文件，目录本身与其他内容保留。返回删除明细。
    /// </summary>
    Task<OfficialRecordWriteResult> RemoveRecordsAsync(string gameFolder, CancellationToken token = default);

    /// <summary>
    /// 目录中是否存在任意官方记录（安装配置或资源清单）。
    /// </summary>
    Task<bool> HasAnyRecordAsync(string gameFolder, CancellationToken token = default);
}
