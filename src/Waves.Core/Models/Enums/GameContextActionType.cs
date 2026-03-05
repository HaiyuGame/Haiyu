namespace Waves.Core.Models.Enums;

public enum GameContextActionType:uint
{
    /// <summary>
    /// 无状态
    /// </summary>
    None,

    /// <summary>
    /// 校验中
    /// </summary>
    Verify,

    /// <summary>
    /// 下载中
    /// </summary>
    Download,

    /// <summary>
    /// 删除文件
    /// </summary>
    DeleteFile,
    Error,

    Decompress,
    GameExit,
    TipMessage,
    CdnSelect
}
