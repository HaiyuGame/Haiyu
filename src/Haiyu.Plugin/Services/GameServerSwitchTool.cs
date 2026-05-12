using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Models;
using Haiyu.Plugin.Models.Enums;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;

namespace Haiyu.Plugin.Services;

/// <summary>
/// 服务器转换工具
/// 1. 服务器备份转换，两个输入服务器与输出服务器之间必须有共存文件夹
/// 2. 开始转换不能停止，否则则清楚上下文的本地配置缓存
/// 3. 工具需要提前输出共存文件夹位置。
/// 第一步：计算差异，标记删除，标记新增
/// 第二部：补全差异
/// 第三步：合并
/// 第四步：修改配置
/// </summary>
public class GameServerSwitchTool : ITool
{
    public string ToolName => "GameServerSwitchTitle";

    public string ToolWaringString => "GameServerSwitchWaringString";

    public ToolType Status { get; private set; }

    /// <summary>
    /// 执行合并
    /// </summary>
    /// <param name="progress"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task InvokeAsync(ServerAnalyseModel analyResult, IProgress<ToolOutputArgs> progress)
    {

    }

    /// <summary>
    /// 分析差异
    /// </summary>
    /// <param name="inputGameContext"></param>
    /// <param name="outputGameContext"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<ServerAnalyseModel> AnalyseAsync(
        IGameContextV2 inputGameContext,
        IGameContextV2 outputGameContext,
        CancellationToken token = default
    )
    {
        var inputResource = await inputGameContext.GetGameResourceAsync(
            (await inputGameContext.GetGameLauncherSourceAsync(null, token))!.ResourceDefault
        );
        var outputResource = await outputGameContext.GetGameResourceAsync(
            (await outputGameContext.GetGameLauncherSourceAsync(null, token))!.ResourceDefault
        );
        var folder =
            await inputGameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder
            ) ?? "";

        var inputPathToMd5 = inputResource
            .Resource.Where(r => !string.IsNullOrEmpty(r.Dest))
            .ToDictionary(
                keySelector: r => BuildFileHelper.BuildFilePath(folder, r.Dest),
                elementSelector: r => r.Md5,
                comparer: StringComparer.OrdinalIgnoreCase
            );
        var outputPaths = outputResource
            .Resource.Where(r => !string.IsNullOrEmpty(r.Dest))
            .Select(r => BuildFileHelper.BuildFilePath(folder, r.Dest))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newFiles = outputResource
            .Resource.Where(r =>
                !string.IsNullOrEmpty(r.Dest)
                && !inputPathToMd5.ContainsKey(BuildFileHelper.BuildFilePath(folder, r.Dest))
            )
            .ToList();
        var rewriteFiles = outputResource
            .Resource.Where(r =>
                !string.IsNullOrEmpty(r.Dest)
                && inputPathToMd5.ContainsKey(BuildFileHelper.BuildFilePath(folder, r.Dest))
                && !string.Equals(
                    r.Md5,
                    inputPathToMd5[BuildFileHelper.BuildFilePath(folder, r.Dest)],
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();
        var deleteFiles = inputResource
            .Resource.Where(r =>
                !string.IsNullOrEmpty(r.Dest)
                && !outputPaths.Contains(BuildFileHelper.BuildFilePath(folder, r.Dest))
            )
            .ToList();
        var unchangedFiles = outputResource
            .Resource.Where(r =>
                !string.IsNullOrEmpty(r.Dest)
                && inputPathToMd5.ContainsKey(BuildFileHelper.BuildFilePath(folder, r.Dest))
                && string.Equals(
                    r.Md5,
                    inputPathToMd5[BuildFileHelper.BuildFilePath(folder, r.Dest)],
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();
        var config = new
        {
            NewFileRatioWeight = 40,    // 新增文件占比权重（满分40分）
            RewriteFileRatioWeight = 30,// 重写文件占比权重（满分30分）
            DeleteFileRatioWeight = 25, // 删除文件占比权重（满分25分）
            ExtremeCountPenalty = 5,    // 极端数量惩罚分（比如单类文件超80%额外加5分）
            TotalScoreThreshold = 35,   // 总分阈值：超过则不建议转换
            ExtremeRatioThreshold = 0.8 // 极端占比阈值（80%）
        };
        double newFileRatio = outputResource.Resource.Count == 0 ? 0 : (double)newFiles.Count / outputResource.Resource.Count;
        double rewriteFileRatio = outputResource.Resource.Count == 0 ? 0 : (double)rewriteFiles.Count / outputResource.Resource.Count;
        double deleteFileRatio = inputResource.Resource.Count == 0 ? 0 : (double)deleteFiles.Count / inputResource.Resource.Count;
        double newFileScore = newFileRatio * config.NewFileRatioWeight;
        double rewriteFileScore = rewriteFileRatio * config.RewriteFileRatioWeight;
        double deleteFileScore = deleteFileRatio * config.DeleteFileRatioWeight;
        double penaltyScore = 0;
        if (newFileRatio > config.ExtremeRatioThreshold || rewriteFileRatio > config.ExtremeRatioThreshold || deleteFileRatio > config.ExtremeRatioThreshold)
        {
            penaltyScore = config.ExtremeCountPenalty;
        }
        double totalScore = newFileScore + rewriteFileScore + deleteFileScore + penaltyScore;
        totalScore = Math.Min(totalScore, 100);
        bool suggestConvert = totalScore <= config.TotalScoreThreshold;
        return new ServerAnalyseModel(newFiles, rewriteFiles, deleteFiles, unchangedFiles, suggestConvert,totalScore);
    }
}
