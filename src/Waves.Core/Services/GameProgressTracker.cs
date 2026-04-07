using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.Enums;

namespace Waves.Core.Services;

/// <summary>
/// 游戏下载/校验进度跟踪器
/// 负责订阅并在内部维护更新当前所处状态。
/// </summary>
public sealed class GameProgressTracker : IAsyncDisposable
{
    private IGameEventSubscription? _subscription;

    public GameContextActionType CurrentAction { get; private set; }

    /// <summary>
    ///
    /// </summary>
    public string CurrentStepTip { get; private set; } = string.Empty;

    // 大步骤状态
    public int CurrentStepIndex { get; private set; }

    public int TotalSteps { get; private set; }

    public int SetupIndex { get; internal set; } = -1;

    public System.Collections.Generic.List<string> AllSteps { get; private set; } = new();

    public List<DownloadSetupItem> GetCurrentSteps()
    {
        var setups = AllSteps
            .Select(
                (name, index) =>
                    new DownloadSetupItem()
                    {
                        Name = name,
                        IsActive = index == CurrentStepIndex,
                        IsOK = index < CurrentStepIndex,
                    }
            )
            .ToList();
        if (CurrentStepIndex >= setups.Count && setups.Count > 0)
        {
            for (int i = 0; i < setups.Count; i++)
            {
                setups[i].IsActive = false;
                setups[i].IsOK = true;
            }
        }
        return setups;
    }

    public long CurrentBytes { get; private set; }

    public long TotalBytes { get; private set; }

    public int CurrentFileIndex { get; private set; }

    public int TotalFiles { get; private set; }

    public double DownloadSpeed { get; private set; }
    public double VerifySpeed { get; private set; }

    public double ZipSpeed { get; private set; }

    public bool IsPaused { get; private set; }
    /// <summary>
    /// 是否预下载
    /// </summary>
    public bool Prod { get; private set; }
    public bool IsActive { get; private set; }

    public string FilePath { get; private set; }

    public long FileCurrentSize { get; private set; }

    public long FileTotalSize { get; private set; }


    

    /// <summary>
    /// 正在进行操作的活跃文件列表（如并发下载/校验的文件）
    /// Key: 文件名, Value: (当前进度, 总大小)
    /// </summary>
    public ConcurrentDictionary<string, (long Current, long Total)> ActiveFiles { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<DownloadActiveFileItem> ActiveFilesItem =>
        new(
            ActiveFiles.Select(x => new DownloadActiveFileItem()
            {
                CurrentSize = x.Value.Current,
                TotalSize = x.Value.Total,
                FileName = x.Key,
            })
        );

    public event Action<GameProgressTracker>? OnProgressChanged;

    public double Percentage =>
        TotalBytes > 0 ? Math.Round((CurrentBytes * 100.0) / TotalBytes, 2) : 0;

    public GameContextOutputArgs LastArgs => _lastArgs;

    public string StepName { get; private set; }
    public bool IsCancel { get; private set; }

    private SynchronizationContext? _syncContext;
    private PeriodicTimer? _timer;
    private Task? _timerTask;
    private GameContextOutputArgs _lastArgs;
    private volatile bool _isDirty;

    /// <summary>
    /// UI线程启动收集数据
    /// </summary>
    /// <param name="publisher"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task StartTrackingAsync(IGameEventPublisher publisher)
    {
        if (publisher == null)
            throw new ArgumentNullException(nameof(publisher));

        _syncContext = SynchronizationContext.Current;

        _subscription = await publisher.SubscribeAsync(HandleEventAsync).ConfigureAwait(false);
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        _timerTask = NotifyLoopAsync();
    }

    /// <summary>
    /// 轮询推送数据
    /// </summary>
    /// <returns></returns>
    private async Task NotifyLoopAsync()
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync())
            {
                if (_isDirty)
                {
                    _isDirty = false;

                    if (_syncContext != null)
                    {
                        _syncContext.Post(_ => OnProgressChanged?.Invoke(this), null);
                    }
                    else
                    {
                        OnProgressChanged?.Invoke(this);
                    }
                }
            }
        }
        catch (Exception) { }
    }

    private async ValueTask HandleEventAsync(GameContextOutputArgs args)
    {
        if (args == null)
            return;
        if (args.Type != GameContextActionType.None)
        {
            CurrentAction = args.Type;
        }
        if (args.IsStepUpdate)
        {
            CurrentStepIndex = args.CurrentStepIndex;
            TotalSteps = args.TotalSteps;
            if (!string.IsNullOrEmpty(args.StepName))
                StepName = args.StepName;

            if (args.AllSteps != null && args.AllSteps.Count > 0)
                AllSteps = args.AllSteps;
        }
        if (
            args.TotalSize > 0
            || args.Type == GameContextActionType.Download
            || args.Type == GameContextActionType.Verify
        )
        {
            CurrentBytes = args.CurrentSize;
            TotalBytes = args.TotalSize;
            CurrentFileIndex = args.CurrentFile;
            TotalFiles = args.FileTotal;
            DownloadSpeed = args.DownloadSpeed;
            VerifySpeed = args.VerifySpeed;
            ZipSpeed = args.ZipSpeed;
            this.IsCancel = args.IsCancel;
        }
        IsActive = args.IsAction;
        IsPaused = args.IsPause;
        this.Prod = args.Prod;
        if (!string.IsNullOrWhiteSpace(args.FilePath))
        {
            FilePath = args.FilePath;
            FileCurrentSize = args.FileCurrentSize;
            FileTotalSize = args.FileTotalSize;

            var fileName = System.IO.Path.GetFileName(args.FilePath);
            if (args.FileCurrentSize >= args.FileTotalSize && args.FileTotalSize > 0)
            {
                ActiveFiles.TryRemove(fileName, out _);
            }
            else
            {
                ActiveFiles[fileName] = (args.FileCurrentSize, args.FileTotalSize);
            }
        }
        if (!string.IsNullOrWhiteSpace(args.TipMessage))
        {
            CurrentStepTip = args.TipMessage;
        }
        this._lastArgs = args;
        _isDirty = true;
        await ValueTask.CompletedTask;
    }

    public string GetSpeedText()
    {
        return CurrentAction switch
        {
            GameContextActionType.Download => $"{FormatBytes(DownloadSpeed)}/s",
            GameContextActionType.Verify => $"{FormatBytes(VerifySpeed)}/s",
            GameContextActionType.Decompress => $"{FormatBytes(ZipSpeed)}/s",
            _ => "",
        };
    }

    public static string FormatBytes(double bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblSByte = bytes;
        while (dblSByte >= 1024 && i < suffix.Length - 1)
        {
            dblSByte /= 1024;
            i++;
        }
        return $"{dblSByte:0.##} {suffix[i]}";
    }

    public static double FormatDoubleBytes(double bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblSByte = bytes;
        while (dblSByte >= 1024 && i < suffix.Length - 1)
        {
            dblSByte /= 1024;
            i++;
        }
        return dblSByte;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _timer?.Dispose();
            _subscription?.Dispose();
            _subscription = null;
            OnProgressChanged = null;

            if (_timerTask != null)
            {
                await _timerTask;
            }
        }
        catch { }
    }

    public double? GetSpeedValue()
    {
        return CurrentAction switch
        {
            GameContextActionType.Download => FormatDoubleBytes(DownloadSpeed),
            GameContextActionType.Verify => FormatDoubleBytes(VerifySpeed),
            GameContextActionType.Decompress => FormatDoubleBytes(ZipSpeed),
            _ => null,
        };
    }
}
