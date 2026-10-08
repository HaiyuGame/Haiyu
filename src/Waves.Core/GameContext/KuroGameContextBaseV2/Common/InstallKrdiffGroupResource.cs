namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 安装库洛补丁包组资源类
/// </summary>
public sealed partial class InstallKrdiffGroupResource : IProgressSetup, IAsyncDisposable
{
    private List<PatchGameFileInfo> krdiffs;
    private string diffFolderPath;
    private List<PatchGameFileInfo> groupFileInfos;
    private string baseFolderPath;
    private string decompressTempFolder;

    public InstallKrdiffGroupResource(LoggerService loggerService)
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

    public void SetParam(Dictionary<string, object> param, GameEventPublisher gameEventPublisher)
    {
        this.Param = param;
        this.GameEventPublisher = gameEventPublisher;
    }

    public async Task<bool> CheckAsync()
    {
        //补丁列表
        if (!Param.CheckParam<List<PatchGameFileInfo>>("krpdiffs", out var krdiffs))
        {
            return false;
        }
        //补丁路径
        if (!Param.CheckParam<string>("diffFolderPath", out var diffFolderPath))
        {
            return false;
        }
        //游戏本体路径
        if (!Param.CheckParam<string>("baseFolderPath", out var baseFolderPath))
        {
            return false;
        }
        //分组文件信息列表
        if (!Param.CheckParam<List<PatchGameFileInfo>>("groupFileInfos", out var groupFileInfos))
        {
            return false;
        }
        if (!Param.CheckParam<string>("decompressTempFolder", out var decompressTempFolder))
        {
            return false;
        }
        this.krdiffs = krdiffs!;
        this.diffFolderPath = diffFolderPath!;
        this.groupFileInfos = groupFileInfos!;
        this.baseFolderPath = baseFolderPath!;
        this.decompressTempFolder = decompressTempFolder!;
        return true;
    }

    /// <summary>
    /// 执行，获得已经解压成功得文件列表
    /// </summary>
    /// <param name="isSync"></param>
    /// <returns></returns>
    public async Task<object?> ExecuteAsync(bool isSync = false)
    {
        try
        {
            if (!await CheckAsync())
            {
                GameEventPublisher.Publish(
                    new GameContextOutputArgs
                    {
                        Type = GameContextActionType.Error,
                        TipMessage = "初始化失败",
                    }
                );
                return false;
            }
            Dictionary<string, string> newFiles = new();
            var tempFolder = decompressTempFolder;
            Directory.CreateDirectory(tempFolder);
            for (int i = 0; i < groupFileInfos.Count; i++)
            {
                var size = groupFileInfos[i].DstFiles.Sum(x => x.Size);
                var diskSize = await BuildFileHelper.GetDiskAvailableSize(baseFolderPath);
                if (diskSize < size)
                {
                    GameEventPublisher.Publish(
                        new GameContextOutputArgs
                        {
                            Type = GameContextActionType.TipMessage,
                            TipMessage =
                                $"磁盘空间不足，剩余空间{GameProgressTracker.FormatBytes(diskSize)},需要空间{GameProgressTracker.FormatBytes(size)}，解压损坏！请修复游戏",
                        }
                    );
                    await Task.Delay(200);
                    return false;
                }
                var krdiffPath = BuildFileHelper.BuildFilePath(diffFolderPath, groupFileInfos[i]);
                if (!File.Exists(krdiffPath))
                {
                    Logger.WriteError($"补丁文件不存在，跳过: {krdiffPath}");
                    GameEventPublisher.Publish(
                        new GameContextOutputArgs
                        {
                            Type = GameContextActionType.Error,
                            TipMessage = $"补丁文件不存在，跳过: {System.IO.Path.GetFileName(krdiffPath)}",
                        }
                    );
                    return false;
                }
                IProgress<(GameContextActionType, string, KrDiffDecompressResult)> progress =
                    new Progress<(GameContextActionType, string, KrDiffDecompressResult)>(
                        (s) =>
                        {
                            GameEventPublisher.Publish(
                                new GameContextOutputArgs
                                {
                                    Type = GameContextActionType.Decompress,
                                    CurrentSize = (long)s.Item3.PatchedCurrentBytes,
                                    TotalSize = (long)s.Item3.PatchTotalBytes,
                                    DownloadSpeed = 0,
                                    VerifySpeed = 0,
                                    IsAction = true,
                                    IsPause = false,
                                    TipMessage = "正在解压合并资源",
                                    CurrentDecompressCount = i,
                                    MaxDecompressValue = groupFileInfos.Count,
                                    FilePath = s.Item2,
                                    FileCurrentSize = (long)s.Item3.PatchedCurrentBytes,
                                    FileTotalSize = (long)s.Item3.PatchTotalBytes,
                                    DiffSpeed=s.Item3.SpeedValue
                                }
                            );
                            ProgressValue = s.Item3.TotalBytesProgress;
                        }
                    );
                var decompressResult = await DiffDecompressTask.DecompressKrdiffFile(
                    baseFolderPath,
                    krdiffPath,
                    i,
                    groupFileInfos.Count,
                    tempFolder,
                    progress: progress
                );
                if (decompressResult != 0)
                {
                    Logger.WriteError($"补丁解压失败，退出码: {decompressResult}，跳过: {krdiffPath}");
                    GameEventPublisher.Publish(
                        new GameContextOutputArgs
                        {
                            Type = GameContextActionType.Error,
                            TipMessage = $"补丁解压失败，退出码: {decompressResult}，跳过: {System.IO.Path.GetFileName(krdiffPath)}",
                        }
                    );
                    return false;
                }
                for (int j = 0; j < groupFileInfos[i].SrcFiles.Count; j++)
                {
                    var deleteFilePath = BuildFileHelper.BuildFilePath(
                        baseFolderPath,
                        groupFileInfos[i].SrcFiles[j]
                    );
                    Logger.WriteError($"删除源文件{deleteFilePath}");
                    if (File.Exists(deleteFilePath))
                        File.Delete(deleteFilePath);
                }
                for (int j = 0; j < groupFileInfos[i].DstFiles.Count; j++)
                {
                    newFiles.Add(
                        BuildFileHelper.BuildFilePath(tempFolder, groupFileInfos[i].DstFiles[j]),
                        BuildFileHelper.BuildFilePath(baseFolderPath, groupFileInfos[i].DstFiles[j])
                    );
                }
                Logger.WriteInfo("删除差异文件");
                if (File.Exists(krdiffPath))
                    File.Delete(krdiffPath);
            }
            var keys = newFiles.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                string value = newFiles[key].Replace("/", "\\");
                var dirName = System.IO.Path.GetDirectoryName(value)!;
                Directory.CreateDirectory(dirName);
                try
                {
                    if (File.Exists(value))
                        File.Delete(value);
                    File.Move(key.Replace("/", "\\"), value, true);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteError($"安装补丁组文件异常: {ex.Message}");
            GameEventPublisher.Publish(new GameContextOutputArgs
            {
                Type = GameContextActionType.Error,
                TipMessage = $"安装补丁组文件异常: {ex.Message}"
            });
            return false;
        }
    }

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher<GameContextOutputArgs> gameEventPublisher)
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
}
