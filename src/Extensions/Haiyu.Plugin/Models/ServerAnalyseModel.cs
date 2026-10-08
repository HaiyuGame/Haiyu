using System.Collections.Generic;
using Waves.Core.Models;

namespace Haiyu.Plugin.Models;

/// <summary>
/// 服务器分析结果
/// </summary>
public class ServerAnalyseModel
{
    public List<GameFileInfo> RewriterFiles { get; }
    public List<GameFileInfo> DeleteFiles { get; }
    public List<GameFileInfo> UnchangedFiles { get; }
    public List<GameFileInfo> AddFiles { get; }

    public double ScoreValue { get; }
    public bool IsSwitch { get; }

    public ServerAnalyseModel(
        List<GameFileInfo> addFiles,
        List<GameFileInfo> rewriterFiles,
        List<GameFileInfo> deleteFiles,
        List<GameFileInfo> unchangedFiles,
       bool isSwitch
,
       double scoreValue)
    {
        AddFiles = addFiles;
        RewriterFiles = rewriterFiles;
        DeleteFiles = deleteFiles;
        UnchangedFiles = unchangedFiles;
        IsSwitch = isSwitch;
        ScoreValue = scoreValue;
    }
}
