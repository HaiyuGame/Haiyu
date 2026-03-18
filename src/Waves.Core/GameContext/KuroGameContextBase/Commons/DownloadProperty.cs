using Haiyu.Common;
using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Common;
using Waves.Core.Models;

namespace Waves.Core.GameContext;

partial class KuroGameContextBase
{
    /// <summary>
    /// 下载校验最大并发数
    /// </summary>
    const int MAX_Concurrency_Count = 5;

    #region 常量
    const int MaxBufferSize = 65536;
    const long UpdateThreshold = 1048576;
    #endregion

    #region 字段和属性
    private string _downloadBaseUrl;
    private long _totalfileSize = 0L;
    private long _totalProgressSize = 0L;
    private long _totalFileTotal = 0L;
    private long _totalProgressTotal = 0L;
    string baseUrl = "";
    #endregion

    #region DownloadStatus
    private long _totalVerifiedBytes;
    private long _totalDownloadedBytes;
    private DateTime _lastSpeedUpdateTime;
    private double _downloadSpeed;
    private double _verifySpeed;

    private GameContextOutputArgs? _lastOutputArgs;
    private GameContextOutputArgs? _lastProdOutputArgs;

    private DateTime _lastSpeedTime = DateTime.Now;
    private long _lastSpeedBytes; // 速度计算基准值
    #endregion

    #region 速度属性
    public double DownloadSpeed => _downloadSpeed;
    public double VerifySpeed => _verifySpeed;
    #endregion

    #region DownloadStatus
    private DownloadState _downloadState;
    private DownloadState _prodDownloadState;
    private CancellationTokenSource _prodDownloadCTS;
    #endregion


    public TimeSpan RemainingTime
    {
        get
        {
            try
            {
                if (DownloadSpeed <= 0 || _totalDownloadedBytes >= _totalfileSize)
                    return TimeSpan.Zero;
                var remainingBytes = _totalfileSize - _totalProgressSize;
                return TimeSpan.FromSeconds(remainingBytes / DownloadSpeed);
            }
            catch (Exception)
            {
                return TimeSpan.Zero;
            }
        }
    }

    public CDNSpeedTester CDNSpeedTester { get; private set; }

}
