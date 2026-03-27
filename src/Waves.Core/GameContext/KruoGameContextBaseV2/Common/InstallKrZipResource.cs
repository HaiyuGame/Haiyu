using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using Serilog.Sinks.File;
using Waves.Core.Common;
using Waves.Core.Common.Downloads;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 安装库洛解压报资源类
/// </summary>
public class InstallKrZipResource : IProgressSetup,IAsyncDisposable
{
    private List<IndexResource> zipInfos;

    private string baseGamePath;


    private string zipDownFolder;

    private DownloadState downloadState;

    private CancellationTokenSource cts;
    private long _totalDownloadedBytes;
    private long _totalProgressSize;
    private long _lastSpeedBytes;
    private DateTime _lastSpeedUpdateTime;
    private double _zipSpeed;
    private long _currentZipMaxSize;

    public string ProgressName { get; set; }

    public double ProgressValue { get; private set; }

    public bool CanPause => true;

    public bool CanStop => true;

    public IGameEventPublisher GameEventPublisher { get; private set; }
    public Dictionary<string, object> Param { get; private set; }
    public LoggerService Logger { get; }

    public InstallKrZipResource(LoggerService logger)
    {
        Logger = logger;
    }

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher)
    {
        this.GameEventPublisher = gameEventPublisher;
        this.Param = param;
    }

    public bool Check()
    {
        if (!Param.CheckParam<List<IndexResource>>("zipInfos", out var zipInfos))
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
                // 在此进行调试
                return false;
            }
            var fileSize = await UnZipTask.GetZipEntriesSizeAsync(item.Key);
            _currentZipMaxSize = fileSize;
            Interlocked.Exchange(ref _totalProgressSize, 0);
            Interlocked.Exchange(ref _totalDownloadedBytes, 0);
            IProgress<(GameContextActionType, bool, long, string, long, long)> progress =
                new Progress<(GameContextActionType, bool, long, string, long, long)>(tuple =>
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
        }
        return true;
    }

    private GameContextOutputArgs UpdateFileProgress(
        GameContextActionType type,
        long fileSize,
        bool isAdd = true,
        string tip = "",
        string filePath = null,
        long currentFileSize = 0,
        long fileMaxSize = 0
    )
    {
        if (type == GameContextActionType.ZipDecompress || type == GameContextActionType.Decompress)
        {
            Interlocked.Add(ref _totalDownloadedBytes, fileSize);
            if (isAdd)
                Interlocked.Add(ref _totalProgressSize, fileSize);
        }
        var elapsed = (DateTime.Now - _lastSpeedUpdateTime).TotalSeconds;
        if (elapsed >= 1)
        {
            _zipSpeed = _totalDownloadedBytes / elapsed;
            Interlocked.Exchange(ref _totalDownloadedBytes, 0);
            var currentBytes = Interlocked.Read(ref _totalDownloadedBytes);
            _lastSpeedBytes = currentBytes;
            _lastSpeedUpdateTime = DateTime.Now;
        }
        var args = new GameContextOutputArgs
        {
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
            return null;
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
