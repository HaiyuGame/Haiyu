using Waves.Core.Models.Enums;

namespace Waves.Core.Models;

public class GameContextOutputArgs
{
    public GameContextActionType Type { get; set; }

    public string ErrorString { get; set; }
    #region 文件进度
    public int FileTotal { get; set; }

    public int CurrentFile { get; set; }

    public string DeleteString { get; set; }
    #endregion

    #region 字节进度
    public long CurrentSize { get; set; }
    public long TotalSize { get; set; }

    public int CurrentDecompressCount { get; set; }

    public int MaxDecompressValue { get; set; }

    public double DownloadSpeed { get; set; }

    public double VerifySpeed { get; set; }

    [Obsolete("Use RemainingTime instead.")]
    public TimeSpan RemainingTime { get; set; }
    #endregion

    public bool IsAction { get; set; }

    public bool IsPause { get; set; }

    // 进度百分比

    public string TipMessage { get; set; }

    public double ProgressPercentage =>
        TotalSize > 0 ? Math.Round((CurrentSize * 100.0) / TotalSize, 2) : 0;
}
