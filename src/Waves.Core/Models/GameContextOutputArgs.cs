using System.Collections.Generic;
using Waves.Core.Models.Enums;

namespace Waves.Core.Models;

public class GameContextOutputArgs
{

    public GameContextActionType Type { get; set; }

    public string ErrorString { get; set; }

    #region 整体大步骤进度
    /// <summary>
    /// 是否为步骤更新消息
    /// </summary>
    public bool IsStepUpdate { get; set; }
    public string StepName { get; set; }
    public int TotalSteps { get; set; }
    /// <summary>
    /// 当更新大步骤时，提供整个步骤的名称列表，以便UI初始化生成侧边栏/步骤条
    /// </summary>
    public List<string> AllSteps { get; set; } = new();
    #endregion

    #region 文件进度
    public int FileTotal { get; set; }

    public int CurrentFile { get; set; }

    public string DeleteString { get; set; }
    #endregion

    #region 字节进度
    public long CurrentSize { get; set; }
    public long TotalSize { get; set; }


    public long CurrentDecompressCount { get; set; }

    public long MaxDecompressValue { get; set; }

    public double DownloadSpeed { get; set; }

    public double VerifySpeed { get; set; }

    [Obsolete("Use RemainingTime instead.")]
    public TimeSpan RemainingTime { get; set; }
    #endregion

    public bool IsAction { get; set; }

    public bool IsPause { get; set; }

    #region 单文件进度
    public string FilePath { get; set; }

    public long FileCurrentSize { get; set; }

    public long FileTotalSize { get; set; }
    #endregion
    public string TipMessage { get; set; }

    public double ProgressPercentage =>
        TotalSize > 0 ? Math.Round((CurrentSize * 100.0) / TotalSize, 2) : 0;

    public int CurrentStepIndex { get; internal set; }
    /// <summary>
    /// 是否为预下载
    /// </summary>
    public bool Prod { get; internal set; }
    public double ZipSpeed { get; internal set; }
}
