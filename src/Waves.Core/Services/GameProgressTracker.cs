using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Waves.Core.Contracts.Events;

namespace Waves.Core.Services;

public sealed class GameProgressTracker : TrackerBase<GameProgressTracker, GameContextOutputArgs>
{
    public GameContextActionType CurrentAction { get; private set; }

    public string CurrentStepTip { get; private set; } = string.Empty;

    public int CurrentStepIndex { get; private set; }

    public int TotalSteps { get; private set; }

    public int SetupIndex { get; internal set; } = -1;

    public List<string> AllSteps { get; private set; } = new();

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

    public bool Prod { get; private set; }
    public bool IsActive { get; private set; }

    public string FilePath { get; private set; }

    public long FileCurrentSize { get; private set; }

    public long FileTotalSize { get; private set; }

    public ConcurrentDictionary<string, (long Current, long Total)> ActiveFiles { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    private long _activeFilesVersion;
    private long _cachedActiveFilesVersion = -1;
    private ObservableCollection<DownloadActiveFileItem>? _cachedActiveFilesItem;

    public long ActiveFilesVersion => Interlocked.Read(ref _activeFilesVersion);

    public ObservableCollection<DownloadActiveFileItem> ActiveFilesItem
    {
        get
        {
            var currentVersion = Interlocked.Read(ref _activeFilesVersion);
            if (_cachedActiveFilesItem != null && _cachedActiveFilesVersion == currentVersion)
            {
                return _cachedActiveFilesItem;
            }
            _cachedActiveFilesItem = new(
                ActiveFiles.Select(x => new DownloadActiveFileItem()
                {
                    CurrentSize = x.Value.Current,
                    TotalSize = x.Value.Total,
                    FileName = x.Key,
                })
            );
            _cachedActiveFilesVersion = currentVersion;
            return _cachedActiveFilesItem;
        }
    }

    public double Percentage =>
        TotalBytes > 0 ? Math.Round((CurrentBytes * 100.0) / TotalBytes, 2) : 0;

    public GameContextOutputArgs LastArgs => _lastArgs;

    public string StepName { get; private set; }
    public bool IsCancel { get; private set; }
    public double DiffSpeed { get; set; }

    private GameContextOutputArgs _lastArgs;
    private DateTime? lastTime;
    private bool _isTerminated;
    private long _terminationGeneration;

    public override ValueTask HandleEventAsync(GameContextOutputArgs args)
    {
        if (args == null)
            return default;

        if (args.Type == GameContextActionType.None)
        {
            CurrentAction = GameContextActionType.None;
            CurrentBytes = 0;
            TotalBytes = 0;
            CurrentFileIndex = 0;
            TotalFiles = 0;
            DownloadSpeed = 0;
            VerifySpeed = 0;
            ZipSpeed = 0;
            DiffSpeed = 0;
            IsCancel = false;
            IsActive = false;
            IsPaused = false;
            FilePath = string.Empty;
            FileCurrentSize = 0;
            FileTotalSize = 0;
            CurrentStepTip = string.Empty;
            ActiveFiles.Clear();
            Interlocked.Increment(ref _activeFilesVersion);
            if (args.Generation > _terminationGeneration)
            {
                _terminationGeneration = args.Generation;
                _isTerminated = true;
            }
            this.lastTime = args.CreateTime;
            this._lastArgs = args;
            _isDirty = true;
            return default;
        }

        if (_isTerminated)
        {
            if (args.Generation > 0 && args.Generation <= _terminationGeneration)
                return default;
            _isTerminated = false;
        }

        if (this.lastTime == null || this.lastTime == DateTime.MinValue)
        {
            this.lastTime = args.CreateTime;
        }
        if (args.CreateTime < this.lastTime)
        {
            return default;
        }
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
            || args.Type == GameContextActionType.Decompress
            || args.Type == GameContextActionType.ZipDecompress
        )
        {
            CurrentBytes = args.CurrentSize;
            TotalBytes = args.TotalSize;
            CurrentFileIndex = args.CurrentFile;
            TotalFiles = args.FileTotal;
            DownloadSpeed = args.DownloadSpeed;
            VerifySpeed = args.VerifySpeed;
            ZipSpeed = args.ZipSpeed;
            this.DiffSpeed = args.DiffSpeed;
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
                Interlocked.Increment(ref _activeFilesVersion);
            }
            else
            {
                ActiveFiles[fileName] = (args.FileCurrentSize, args.FileTotalSize);
                Interlocked.Increment(ref _activeFilesVersion);
            }
        }
        if (!string.IsNullOrWhiteSpace(args.TipMessage))
        {
            CurrentStepTip = args.TipMessage;
        }
        this._lastArgs = args;
        _isDirty = true;

        return default;
    }

    public override void Invoke()
    {
        onTrackerHandle?.Invoke(this);
    }

    public override async Task OnVirualDispose()
    {
        ActiveFiles.Clear();
        _cachedActiveFilesItem = null;
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
