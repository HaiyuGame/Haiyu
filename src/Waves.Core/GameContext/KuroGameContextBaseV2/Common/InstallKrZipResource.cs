using System.Diagnostics;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 安装库洛解压报资源类
/// </summary>
public class InstallKrZipResource : IProgressSetup,IAsyncDisposable
{
    private long _generation;
    private List<PatchGameFileInfo> zipInfos;

    private string baseGamePath;


    private string zipDownFolder;

    private DownloadState downloadState;

    private CancellationTokenSource cts;
    private long _totalDownloadedBytes;
    private long _totalProgressSize;
    private readonly object _progressGate = new();
    private long _speedSampleTimestamp;
    private double _smoothedZipSpeed;
    private double _zipSpeed;
    private long _currentZipMaxSize;

    public string ProgressName { get; set; }

    public double ProgressValue { get; private set; }

    public bool CanPause => true;

    public bool CanStop => true;

    public IGameEventPublisher<GameContextOutputArgs> GameEventPublisher { get; private set; }
    public Dictionary<string, object> Param { get; private set; }
    public LoggerService Logger { get; }

    public InstallKrZipResource(LoggerService logger)
    {
        Logger = logger;
    }

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher<GameContextOutputArgs> gameEventPublisher)
    {
        _generation = GameContextOutputArgs.CurrentGeneration.Value;
        this.GameEventPublisher = gameEventPublisher;
        this.Param = param;
    }

    public bool Check()
    {
        if (!Param.CheckParam<List<PatchGameFileInfo>>("zipInfos", out var zipInfos))
        {
            return false;
        }
        if (!Param.CheckParam<string>("baseGamePath", out var baseGamePath))
        {
            return false;
        }
        if (!Param.CheckParam<string>("zipDownFolder", out var zipDownFolder))
        {
            return false;
        }
        if (!Param.CheckParam<DownloadState>("downloadState", out var downloadState))
        {
            return false;
        }
        this.zipInfos = zipInfos!;
        this.baseGamePath = baseGamePath!;
        this.zipDownFolder = zipDownFolder!;
        this.downloadState = downloadState!;
        return true;
    }

    public async Task<bool> RunAsync()
    {
        if (!Check())
        {
            this.GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
            Generation = _generation,
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "参数不正确，无法解压",
                }
            );
            return false;
        }
        var resultZipFiles = new Dictionary<string, long>();
        foreach (var zipInfo in this.zipInfos)
        {
            resultZipFiles.Add(Path.Combine(zipDownFolder, zipInfo.Dest), await UnZipTask.GetZipEntriesSizeAsync(
                Path.Combine(zipDownFolder, zipInfo.Dest)
            ));
        }
        foreach (var item in resultZipFiles)
        {
            if (!File.Exists(item.Key))
            {
                GameEventPublisher.Publish(new GameContextOutputArgs()
                {
            Generation = _generation,
                    Type = GameContextActionType.TipMessage,
                    TipMessage = "解压文件不存在，无法解压，请直接修复游戏",
                });
                return false;
            }
            var fileSize = item.Value;
            InitZipProgress(fileSize);
            IProgress<(GameContextActionType, bool, long, string, long, long)> progress =
                new ZipProgress(tuple =>
                {
                    var args = UpdateFileProgress(
                        tuple.Item1,
                        tuple.Item3,
                        tuple.Item2,
                        filePath: tuple.Item4,
                        currentFileSize: tuple.Item5,
                        fileMaxSize: tuple.Item6
                    );
                    GameEventPublisher.Publish(args);
                });
            var unzipResult = await UnZipTask.UnZipFileAsync(
                item.Key,
                baseGamePath,
                fileSize,
                downloadState,
                progress,
                Logger
            );
            if (!unzipResult || downloadState.CancelToken.IsCancellationRequested) return false;
            File.Delete(item.Key);
        }
        return true;
    }

    private sealed class ZipProgress(Action<(GameContextActionType, bool, long, string, long, long)> report)
        : IProgress<(GameContextActionType, bool, long, string, long, long)>
    {
        public void Report((GameContextActionType, bool, long, string, long, long) value) => report(value);
    }

    internal void InitZipProgress(long size)
    {
        lock (_progressGate)
        {
            _currentZipMaxSize = size;
            _totalProgressSize = _totalDownloadedBytes = 0;
            _zipSpeed = _smoothedZipSpeed = 0;
            _speedSampleTimestamp = Stopwatch.GetTimestamp();
        }
    }

    internal GameContextOutputArgs UpdateFileProgress(
        GameContextActionType type,
        long fileSize,
        bool isAdd = true,
        string tip = "",
        string filePath = null,
        long currentFileSize = 0,
        long fileMaxSize = 0
    )
    {
        lock (_progressGate)
        {
            if (type == GameContextActionType.ZipDecompress || type == GameContextActionType.Decompress)
            {
                Interlocked.Add(ref _totalDownloadedBytes, fileSize);
                if (isAdd)
                    Interlocked.Add(ref _totalProgressSize, fileSize);
            }
            var elapsed = Stopwatch.GetElapsedTime(_speedSampleTimestamp).TotalSeconds;
            if (elapsed >= 1)
            {
                _zipSpeed = Interlocked.Exchange(ref _totalDownloadedBytes, 0) / elapsed;
                _smoothedZipSpeed = _zipSpeed > 0
                    ? (_smoothedZipSpeed > 0 ? 0.3 * _zipSpeed + 0.7 * _smoothedZipSpeed : _zipSpeed) : 0;
                _speedSampleTimestamp = Stopwatch.GetTimestamp();
            }
            var total = _currentZipMaxSize > 0 ? _currentZipMaxSize : fileMaxSize;
            var remaining = Math.Max(0, total - _totalProgressSize);
            TimeSpan? remainingTime = null;
            if (downloadState is not null && !downloadState.IsPaused
                && !downloadState.CancelToken.IsCancellationRequested && total > 0)
            {
                if (remaining == 0) remainingTime = TimeSpan.Zero;
                else if (_smoothedZipSpeed > 0)
                {
                    var seconds = remaining / _smoothedZipSpeed;
                    if (double.IsFinite(seconds) && seconds < TimeSpan.MaxValue.TotalSeconds)
                        remainingTime = TimeSpan.FromSeconds(Math.Ceiling(seconds));
                }
            }
            var args = new GameContextOutputArgs
            {
                Generation = _generation,
                RemainingTime = remainingTime,
                Type = type,
                CurrentSize = _totalProgressSize,
                TotalSize = _currentZipMaxSize > 0 ? _currentZipMaxSize : fileMaxSize,
                FileTotal = this.zipInfos?.Count ?? 0,
                ZipSpeed = _zipSpeed,
                FilePath = filePath,
                FileCurrentSize = currentFileSize,
                FileTotalSize = fileMaxSize,
                CurrentDecompressCount = currentFileSize,
                MaxDecompressValue = fileMaxSize,
                Prod = false,
                IsAction = this.downloadState?.IsActive ?? false,
                IsPause = downloadState?.IsPaused ?? false,
                TipMessage = tip,
            };
            return args;
        }
    }

    public async Task<object?> ExecuteAsync(bool isSync = false)
    {
        if (isSync)
        {
            return await RunAsync();
        }
        else
        {
            Task.Run(async()=>
            {
                await RunAsync();
            });
            return true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                await cts.CancelAsync();
            }
        }
        catch
        {
        }

        zipInfos?.Clear();
        Param?.Clear();
    }
}
