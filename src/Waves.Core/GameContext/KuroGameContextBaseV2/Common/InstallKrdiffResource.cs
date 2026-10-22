namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 安装库洛补丁包资源类
/// </summary>
public class InstallKrdiffResource : IProgressSetup, IAsyncDisposable
{
    private long _generation;
    private List<PatchGameFileInfo> krdiffs;
    private string diffFolderPath;
    private string gameBaseFolder;
    private bool isPreview;

    public InstallKrdiffResource(LoggerService loggerService)
    {
        this.Logger = loggerService;
    }

    public Dictionary<string, object> Param { get; private set; }
    public LoggerService Logger { get; }
    public IGameEventPublisher<GameContextOutputArgs> GameEventPublisher { get; private set; }
    public string ProgressName { get; set; }
    public double ProgressValue { get; set; }

    public bool CanPause => false;

    public bool CanStop => false;

    public void SetParam(Dictionary<string, object> param)
    {
        _generation = GameContextOutputArgs.CurrentGeneration.Value;
        this.Param = param;
    }

    public async Task<bool> CheckAsync()
    {
        //补丁
        if (!Param.CheckParam<List<PatchGameFileInfo>>("krdiffs", out var krdiffs))
        {
            return false;
        }
        //资源基础路径
        if (!Param.CheckParam<string>("diffFolderPath", out var diffFolderPath))
        {
            return false;
        }
        if (!Param.CheckParam<string>("gameBaseFolder", out var gameBaseFolder))
            return false;
        Param.CheckParam<bool>("isPreview", out var isPreview);
        this.krdiffs = krdiffs!;
        this.diffFolderPath = diffFolderPath!;
        this.gameBaseFolder = gameBaseFolder!;
        this.isPreview = isPreview;
        return true;
    }

    public async Task<bool> RunAsync()
    {
        if (!await CheckAsync())
            return false;
        for (int i = 0; i < krdiffs.Count; i++)
        {
            var diskSize = await BuildFileHelper.GetDiskAvailableSize(gameBaseFolder);
            if (diskSize < krdiffs[i].Size)
            {
                GameEventPublisher.Publish(
                    new GameContextOutputArgs
                    {
                        Generation = _generation,
                        Type = GameContextActionType.TipMessage,
                        ErrorString = $"磁盘空间不足，剩余可用空间{diskSize}字节,严重不足",
                    }
                );
                return false;
            }
            var krdiffPath = BuildFileHelper.BuildFilePath(diffFolderPath, krdiffs[i]);
            if (!File.Exists(krdiffPath))
                return false;
            IProgress<(GameContextActionType, string, KrDiffDecompressResult)> progress =
                new Progress<(GameContextActionType, string, KrDiffDecompressResult)>(
                    (s) =>
                    {
                        GameEventPublisher.Publish(
                            new GameContextOutputArgs
                            {
                                Generation = _generation,
                                Type = GameContextActionType.Decompress,
                                CurrentSize = (long)s.Item3.PatchedCurrentBytes,
                                TotalSize = (long)s.Item3.PatchTotalBytes,
                                DownloadSpeed = 0,
                                VerifySpeed = 0,
                                IsAction = true,
                                IsPause = false,
                                CurrentDecompressCount = i,
                                MaxDecompressValue = krdiffs.Count,
                            }
                        );
                        ProgressValue = s.Item3.TotalBytesProgress;
                    }
                );
            int diffValue = -1;
            if (this.isPreview)
            {
                diffValue = await DiffDecompressTaskV2.DecompressKrdiffFile(
                    gameBaseFolder,
                    krdiffPath,
                    i,
                    krdiffs.Count,
                    progress: progress
                );
            }
            else
            {
                diffValue = await DiffDecompressTask.DecompressKrdiffFile(
                    gameBaseFolder,
                    krdiffPath,
                    i,
                    krdiffs.Count,
                    progress: progress
                );
            }

            if (diffValue != 0)
            {
                Logger.WriteError($"补丁解压失败，退出码: {diffValue}，跳过: {krdiffPath}");
                GameEventPublisher.Publish(
                    new GameContextOutputArgs
                    {
                        Generation = _generation,
                        Type = GameContextActionType.Error,
                        TipMessage =
                            $"补丁解压失败，退出码: {diffValue}，跳过: {System.IO.Path.GetFileName(krdiffPath)}",
                    }
                );
                return false;
            }
        }
        return true;
    }

    public void SetParam(
        Dictionary<string, object> param,
        IGameEventPublisher<GameContextOutputArgs> gameEventPublisher
    )
    {
        this.Param = param;
        this.GameEventPublisher = gameEventPublisher;
    }

    /// <summary>
    /// 当前解压任务不可取消，UI界面需要按钮禁用
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task<object?> ExecuteAsync(bool isSync = false)
    {
        if (isSync)
        {
            return await RunAsync();
        }
        else
        {
            Task.Run(async () => await RunAsync());
            return true;
        }
    }
}
