using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Common;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

public partial class KuroGameContextBase
{
    /// <summary>
    /// 下载校验最大并发数
    /// </summary>
    const int MAX_Concurrency_Count = 4;

    #region 常量
    const int MaxBufferSize = 65536; // 64KB缓冲区
    const long UpdateThreshold = 1048576; // 1MB进度更新阈值
    #endregion

    #region 字段和属性
    private string _downloadBaseUrl;
    private long _totalfileSize = 0L;
    private long _totalProgressSize = 0L;
    private long _totalFileTotal = 0L;
    private long _totalProgressTotal = 0L;
    #endregion

    #region DownloadStatus
    private long _totalVerifiedBytes;
    private long _totalDownloadedBytes;
    private DateTime _lastSpeedUpdateTime;
    private double _downloadSpeed;
    private double _verifySpeed;

    private DateTime _lastSpeedTime = DateTime.Now;
    private long _lastSpeedBytes; // 速度计算基准值
    #endregion

    #region 速度属性
    public double DownloadSpeed => _downloadSpeed;
    public double VerifySpeed => _verifySpeed;
    #endregion
    #region DownloadStatus
    private DownloadState _downloadState;
    #endregion
    #region 公开方法
    public async Task StartDownloadTaskAsync(
        string folder,
        GameLauncherSource? source,
        bool isDelete = false
    )
    {
        if (source == null || string.IsNullOrWhiteSpace(folder))
            return;
        _downloadCTS = new CancellationTokenSource();
        _isDownload = true;
        _totalProgressSize = 0;
        _totalProgressTotal = 0;
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, folder);
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");
        await GetGameResourceAsync(folder, source, isDelete);
    }
    #endregion


    public async Task UpdataGameAsync(string diffSavePath = null,UpdateGameType type = UpdateGameType.UpdateGame)
    {
        _downloadCTS = new CancellationTokenSource();
        var folder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        var launcher = await this.GetGameLauncherSourceAsync(null, _downloadCTS.Token);
        if (string.IsNullOrWhiteSpace(folder) || launcher == null)
            return;
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");
        await UpdataGameResourceAsync(folder, launcher, diffSavePath);
        if(type == UpdateGameType.ProDownload)
        {
            //如果是预下载安装，则直接删除预下载配置
            await this.GameLocalConfig.SaveConfigAsync(
               GameLocalSettingName.ProdDownloadPath,
               "");
            await this.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.ProdDownloadFolderDone,
                "False"
            ); await this.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.ProdDownloadVersion,
                ""
            );
        }

    }

    #region 核心下载逻辑
    private async Task<bool> GetGameResourceAsync(
        string folder,
        GameLauncherSource source,
        bool isDelete
    )
    {
        try
        {
            var resource = await GetGameResourceAsync(source.ResourceDefault);
            if (resource == null)
                return false;
            // 构建下载基础URL
            _downloadBaseUrl =
                source.ResourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).First().Url
                + source.ResourceDefault.Config.BaseUrl;
            HttpClientService.BuildClient();
            await InitializeProgress(resource.Resource);
            await Task.Run(() => StartDownloadAsync(folder, resource, isDelete));
            if (!_isDownload)
            {
                await DownloadComplate(source);
            }
            await SetNoneStatusAsync().ConfigureAwait(false);
            return true;
        }
        catch (IOException ex)
        {
            Logger.WriteError(ex.Message);
            await this.SetNoneStatusAsync();
            return true;
        }
    }

    async Task DownloadComplate(GameLauncherSource source)
    {
        if (_downloadState!.IsStop)
            return;
        var currentVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        var installFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            await this.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.LocalGameVersion,
                source.ResourceDefault.Version
            );
        }
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LocalGameVersion,
            source.ResourceDefault.Version
        );
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LocalGameUpdateing,
            "False"
        ); 

       
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.GameLauncherBassProgram,
            $"{installFolder}\\{this.Config.GameExeName}"
        );
        if (gameContextOutputDelegate == null)
        {
            return;
        }
        await this
            .gameContextOutputDelegate.Invoke(
                this,
                new GameContextOutputArgs() { Type = GameContextActionType.None }
            )
            .ConfigureAwait(false);
    }

    private async Task StartDownloadAsync(string folder, IndexGameResource resource, bool isDelete)
    {
        _downloadState.IsActive = true;
        if (isDelete)
        {
            Logger.WriteInfo("修复游戏，开始删除本地多余文件");
            var localFile = new DirectoryInfo(folder).GetFiles("*", SearchOption.AllDirectories);
            var serverFileSet = new HashSet<string>(
                resource.Resource.Select(x => BuildFileHelper.BuildFilePath(folder, x).ToLower())
            );

            var filesToDelete = localFile
                .Where(f =>
                {
                    return !serverFileSet.Contains(f.FullName.ToLower());
                })
                .ToList();

            if (filesToDelete.Any())
            {
                foreach (var file in filesToDelete)
                {
                    File.Delete(file.FullName);
                }
                var fileNames = filesToDelete.Select(f => Path.GetFileName(f.FullName));
                Logger.WriteInfo($"删除：删除版本旧文件{string.Join(',', fileNames)}");
            }
        }
        await UpdateFileProgress(GameContextActionType.Verify, 0);
        #region 下载逻辑
        try
        {
            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = MAX_Concurrency_Count,
                CancellationToken = _downloadCTS.Token,
            };
            if (!(await ParallelDownloadAsync(resource.Resource, options, folder)))
            {
                throw new IOException("下载文件出错！");
            }
        }
        catch (IOException ex)
        {
            _downloadState.IsActive = false;
            _downloadCTS?.Dispose();
            _downloadCTS = null;
            _isDownload = false;
            _downloadState.IsStop = true;
            await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "False");
            await SetNoneStatusAsync().ConfigureAwait(false);
            Logger.WriteError($"退出下载，错误{ex.Message}");
            return;
        }
        catch (OperationCanceledException operEx)
        {
            _downloadState.IsActive = false;
            _downloadCTS?.Dispose();
            _downloadState.IsStop = true;
            _downloadCTS = null;
            _isDownload = false;
            await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "False");
            await SetNoneStatusAsync().ConfigureAwait(false);
            Logger.WriteError($"退出下载，错误{operEx.Message}");
            return;
        }
        #endregion
        _downloadCTS?.Dispose();
        _downloadCTS = null;
        _isDownload = false;
        _downloadState.IsActive = false;
    }

    public int GetVerifyLimit()
    {
        if (
            this.ContextName == nameof(PunishMainGameContext)
            || this.ContextName == nameof(PunishGlobalGameContext)
            || this.ContextName == nameof(PunishTwGameContext)
        )
        {
            return 200;
        }
        else
        {
            return 2000;
        }
    }

    public async Task<bool> ParallelDownloadAsync(
        List<IndexResource> resource,
        ParallelOptions options,
        string folder,
        bool ispred = false
    )
    {
        try
        {
            await Parallel.ForEachAsync(
                resource,
                options,
                async (item, token) =>
                {
                    Logger.WriteInfo(
                        $"[{item.Dest}],当前进度大小[{Math.Round((double)_totalProgressSize, 2)}/{Math.Round((double)_totalfileSize, 2)}]"
                    );
                    if (IsDownloadCanceled())
                    {
                        this._downloadState.IsActive = false;
                        await SetNoneStatusAsync().ConfigureAwait(false);
                        return;
                    }
                    var filePath = BuildFileHelper.BuildFilePath(folder, item);
                    if (File.Exists(filePath))
                    {
                        if (item.ChunkInfos == null)
                        {
                            var checkResult = await VaildateFullFile(item.Md5, filePath, ispred);
                            if (checkResult)
                            {
                                Logger.WriteInfo("需要全量下载……");
                                await DownloadFileByFull(
                                    item.Dest,
                                    item.Size,
                                    filePath,
                                    new()
                                    {
                                        Start = 0,
                                        End = item.Size - 1,
                                        Md5 = item.Md5,
                                    },
                                    ispred
                                );
                            }
                            else
                            {
                                await UpdateFileProgress(
                                        GameContextActionType.Verify,
                                        item.Size,
                                        true,
                                        ispred
                                    )
                                    .ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            var fileName = System.IO.Path.GetFileName(filePath);
                            for (int i = 0; i < item.ChunkInfos.Count; i++)
                            {
                                var needDownload = await ValidateFileChunks(
                                    item.ChunkInfos[i],
                                    filePath,
                                    ispred
                                );
                                if (needDownload)
                                {
                                    Logger.WriteInfo($"分片[{i}]需要全量下载……");
                                    if (i == item.ChunkInfos.Count - 1)
                                    {
                                        await DownloadFileByChunks(
                                            item.Dest,
                                            filePath,
                                            item.ChunkInfos[i].Start,
                                            item.ChunkInfos[i].End,
                                            true,
                                            item.Size,
                                            ispred
                                        );
                                    }
                                    else
                                    {
                                        await DownloadFileByChunks(
                                            item.Dest,
                                            filePath,
                                            item.ChunkInfos[i].Start,
                                            item.ChunkInfos[i].End,
                                            false,
                                            isPred: ispred
                                        );
                                    }
                                }
                                else
                                {
                                    await UpdateFileProgress(
                                            GameContextActionType.Verify,
                                            item.ChunkInfos[i].End - item.ChunkInfos[i].Start,
                                            true,
                                            ispred
                                        )
                                        .ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    else
                    {
                        Logger.WriteInfo($"文件不存在，全量下载");
                        await DownloadFileByFull(
                            item.Dest,
                            item.Size,
                            filePath,
                            new IndexChunkInfo()
                            {
                                Start = 0,
                                End = item.Size - 1,
                                Md5 = item.Md5,
                            },
                            ispred
                        );
                        //await FinalValidation(file, filePath);
                    }
                }
            );
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteError("校验失败！");
            return false;
        }
    }

    public async Task<bool> PauseDownloadAsync()
    {
        if (this._downloadState.IsActive)
        {
            Logger.WriteInfo($"暂停下载");
            return await this._downloadState.PauseAsync();
        }

        return false;
    }

    public async Task<bool> ResumeDownloadAsync()
    {
        if (_downloadState.IsPaused)
        {
            Logger.WriteInfo($"恢复下载");
            _lastSpeedTime = DateTime.Now;
            return await _downloadState.ResumeAsync();
        }
        return false;
    }

    public async Task<bool> StopDownloadAsync()
    {
        try
        {
            if (_downloadCTS != null && !_downloadCTS.IsCancellationRequested)
            {
                this._downloadState.IsStop = true;
                await _downloadCTS.CancelAsync().ConfigureAwait(false);
            }
            Interlocked.Exchange(ref _totalProgressSize, 0L);
            Interlocked.Exchange(ref _totalfileSize, 0L);
            Interlocked.Exchange(ref _totalVerifiedBytes, 0L);
            Interlocked.Exchange(ref _totalDownloadedBytes, 0L);
            Logger.WriteInfo($"取消下载");
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteInfo($"取消下载异常: {ex.Message}");
            return false;
        }
        finally
        {
            this._isDownload = false;
            _downloadCTS?.Dispose();
            _downloadCTS = null;
        }
    }
    #endregion

    async Task UpdataGameResourceAsync(
        string folder,
        GameLauncherSource launcher,
        string diffSavePath
    )
    {
        var currentVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        #region 测试预下载
        var previous = launcher
            .ResourceDefault.Config.PatchConfig.Where(x => x.Version == currentVersion)
            .FirstOrDefault();
        
        #endregion
        PatchIndexGameResource? patch = null;
        this._downloadState = new DownloadState();
        await _downloadState.SetSpeedLimitAsync(this.SpeedValue);
        _downloadState.IsActive = true;
        _totalProgressTotal = 0;
        _totalVerifiedBytes = 0;
        _totalDownloadedBytes = 0;
        if (previous != null)
        {
            var cdnUrl =
                launcher
                    .ResourceDefault.CdnList.Where(x => x.P != 0)
                    .OrderBy(x => x.P)
                    .FirstOrDefault() ?? null;
            if (cdnUrl == null)
            {
                await CancelDownloadAsync();
                return;
            }
            patch = await GetPatchGameResourceAsync(cdnUrl.Url + previous.IndexFile);
        }
        else
        {
            Logger.WriteInfo("本地资源与网络版本不匹配，请直接尝试修复游戏！");
            await CancelDownloadAsync();
            return;
        }
        if (patch == null)
        {
            Logger.WriteInfo("获得Patch文件失败！");
            await CancelDownloadAsync();
            return;
        }
        _downloadCTS = new CancellationTokenSource();
        bool result = false;
        _downloadBaseUrl =
            launcher.ResourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).First().Url
            + previous.BaseUrl;
        _totalProgressTotal = 0;
        _totalProgressSize = 0;
        if (
            patch.ApplyTypes != null
            && patch.ApplyTypes.Contains("patch")
            && patch.PatchInfos != null
            && patch.PatchInfos.Count > 0
        )
        {
            var count = patch.Resource.Where(x => x.Dest.EndsWith(".krdiff"));
            var size = count.Sum(x => x.Size);
            _totalfileSize = size;
            _totalFileTotal = count.Count() - 1;
            _totalProgressTotal = 0;
            result = await Task.Run(() => DownloadPatcheToResource(diffSavePath, patch));
            if (result == false)
            {
                Logger.WriteInfo($"下载差异文件失败，请检查网络之后，重启启动器再次更新");
                await SetNoneStatusAsync().ConfigureAwait(false);
                await UpdateFileProgress(
                        GameContextActionType.TipMessage,
                        0,
                        false,
                        false,
                        "下载差异组文件失败，请尝试修复游戏！"
                    )
                    .ConfigureAwait(false);
                return;
            }
        }
        else if (
            patch.ApplyTypes != null
            && patch.ApplyTypes.Contains("group")
            && patch.GroupInfos != null
            && patch.GroupInfos.Count > 0
        )
        {
            //2.8.0_3.0.0_group_27_1766046101203.krpdiff
            var count = patch.Resource.Where(x => x.Dest.EndsWith(".krpdiff"));
            var size = count.Sum(x => x.Size);
            _totalfileSize = size;
            _totalFileTotal = count.Count() - 1;
            _totalProgressTotal = 0;
            this._downloadState = new DownloadState();
            this._downloadState.IsActive = true;
            await _downloadState.SetSpeedLimitAsync(this.SpeedValue);
            result = await Task.Run(() =>
                DownloadGroupPatcheToResource(diffSavePath, patch.Resource)
            );
            if (result == false)
            {
                Logger.WriteInfo($"下载差异组文件失败，请检查网络之后，重启启动器再次更新");
                await SetNoneStatusAsync().ConfigureAwait(false);
                await UpdateFileProgress(
                        GameContextActionType.TipMessage,
                        0,
                        false,
                        false,
                        "下载差异组文件失败，请尝试修复游戏！"
                    )
                    .ConfigureAwait(false);
                return;
            }
            string tempFolder = folder + "\\decompressFolder";
            Dictionary<string, string> newFiles = new();
            for (int i = 0; i < patch.GroupInfos.Count; i++)
            {
                var filePath = BuildFileHelper.BuildFilePath(diffSavePath, patch.GroupInfos[i]);
                await DecompressKrdiffFile(
                    folder,
                    filePath,
                    i + 1,
                    patch.GroupInfos.Count,
                    tempFolder
                );
                Logger.WriteInfo($"文件{filePath}解压完毕，已经删除");
                for (int j = 0; j < patch.GroupInfos[i].SrcFiles.Count; j++)
                {
                    var deleteFilePath = BuildFileHelper.BuildFilePath(
                        folder,
                        patch.GroupInfos[i].SrcFiles[j]
                    );
                    Logger.WriteError($"删除源文件{deleteFilePath}");
                    File.Delete(deleteFilePath);
                }
                foreach (var file in patch.GroupInfos[i].DstFiles)
                {
                    newFiles.Add(
                        BuildFileHelper.BuildFilePath(tempFolder, file),
                        BuildFileHelper.BuildFilePath(folder, file)
                    );
                }
                File.Delete(filePath);
                Logger.WriteInfo($"删除差异文件：{filePath}");
            }
            var resource = await GetGameResourceAsync(launcher.ResourceDefault);
            //var resource2 = await GetGameResourceAsync(launcher.ResourceDefault,launcher.Predownload);
            if (resource == null)
            {
                this._isDownload = false;
                Logger.WriteInfo($"下载差异组文件失败，请尝试修复游戏！");
                await UpdateFileProgress(
                        GameContextActionType.TipMessage,
                        0,
                        false,
                        false,
                        "下载差异组文件失败，请重启应用进行重新更新"
                    )
                    .ConfigureAwait(false);
                await SetNoneStatusAsync().ConfigureAwait(false);
                return;
            }
            if (!await CheckApplyFilesMd5(resource.Resource, folder, tempFolder, newFiles))
            {
                this._isDownload = false;
                Logger.WriteInfo($"下载差异组文件失败，请尝试修复游戏！");
                await UpdateFileProgress(
                        GameContextActionType.TipMessage,
                        0,
                        false,
                        false,
                        "更新失败，请直接进行修复文件"
                    )
                    .ConfigureAwait(false);
                await SetNoneStatusAsync().ConfigureAwait(false);
                return;
            }
        }
        else
        {
            var resourceinfo = patch.Resource;
            _downloadBaseUrl =
                launcher.ResourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).First().Url
                + launcher.ResourceDefault.ResourcesBasePath;
            _totalfileSize = resourceinfo.Sum(x => x.Size);
            _totalFileTotal = resourceinfo.Count() - 1;
            _totalProgressTotal = resourceinfo.Sum(x => x.Size);
            _totalProgressSize = 0;
            _downloadCTS = new CancellationTokenSource();
            result = await Task.Run(() => UpdateGameToResources(folder, resourceinfo.ToList()));
        }
        if (!result)
        {
            Logger.WriteInfo($"下载差异组文件失败，请使用游戏修复进行更新游戏");
            await SetNoneStatusAsync().ConfigureAwait(false);
            await UpdateFileProgress(
                GameContextActionType.TipMessage,
                0,
                false,
                false,
                "下载差异文件失败，请直接进行修复游戏"
            );
            this._isDownload = false;
            return;
        }
        else
        {
            Logger.WriteInfo("删除缓存文件夹");
        }
        #region Update Resource
        for (int i = 0; i < patch.DeleteFiles.Count; i++)
        {
            var localFile = $"{folder}\\{patch.DeleteFiles[i]}".Replace('/', '\\');
            if (File.Exists(localFile))
            {
                File.Delete(localFile);
            }
            Logger.WriteInfo($"删除旧文件{System.IO.Path.GetFileName(localFile)}");
            this.gameContextOutputDelegate?.Invoke(
                    this,
                    new GameContextOutputArgs()
                    {
                        Type = GameContextActionType.DeleteFile,
                        FileTotal = patch.DeleteFiles.Count,
                        CurrentFile = i,
                        DeleteString =
                            i != patch.DeleteFiles.Count
                                ? $"正在删除旧文件{System.IO.Path.GetFileName(localFile)}"
                                : "稍微等一下，马上就好",
                    }
                )
                .ConfigureAwait(false);
        }
        await DownloadComplate(launcher);
        #endregion
        await SetNoneStatusAsync().ConfigureAwait(false);
    }


    private async Task<IndexGameResource> GetGameResourceAsync(ResourceDefault resourceDefault,Predownload predownload,CancellationToken token = default)
    {
        var resourceIndexUrl =
            resourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).First().Url
            + predownload.Config.IndexFile;
        var result = await HttpClientService.HttpClient.GetAsync(resourceIndexUrl, token);
        var jsonStr = await result.Content.ReadAsStringAsync();
        var launcherIndex = JsonSerializer.Deserialize<IndexGameResource>(
            jsonStr,
            IndexGameResourceContext.Default.IndexGameResource
        );
        return launcherIndex;
    }

    private async Task<bool> CheckApplyFilesMd5(
        List<IndexResource> list,
        string folder,
        string tempFolder,
        Dictionary<string, string> newFiles
    )
    {
        try
        {
            var keys = newFiles.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                string value = newFiles[key];
                if (File.Exists(value))
                    File.Delete(value);
                File.Move(key, value);
                this.gameContextOutputDelegate?.Invoke(
                        this,
                        new GameContextOutputArgs()
                        {
                            Type = GameContextActionType.DeleteFile,
                            FileTotal = keys.Count,
                            CurrentFile = i,
                            DeleteString = $"正在移动校验文件{System.IO.Path.GetFileName(value)}",
                        }
                    )
                    .ConfigureAwait(false);
            }
            await InitializeProgress(list);
            var resource = await this.GetGameLauncherSourceAsync();
            var resourceOne = await this.GetGameResourceAsync(resource.ResourceDefault);
            if (!await GetGameResourceAsync(folder, resource, false))
            {
                await UpdateFileProgress(
                        GameContextActionType.TipMessage,
                        0,
                        false,
                        false,
                        "更新校验出错，请直接尝试修复游戏，下载缓存需手动删除"
                    )
                    .ConfigureAwait(false);
                await SetNoneStatusAsync();
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            await SetNoneStatusAsync().ConfigureAwait(false);
            await UpdateFileProgress(GameContextActionType.TipMessage, 0, false, false, ex.Message);
            this._isDownload = false;
            Logger.WriteError(ex.Message);
            return false;
        }
    }

    private async Task<bool> DownloadGroupPatcheToResource(
        string folder,
        List<IndexResource> patch,
        bool ispred = false
    )
    {
        var patchInfos = patch.Where(x => x.Dest.EndsWith("krpdiff")).ToList();
        ParallelOptions options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = MAX_Concurrency_Count,
            CancellationToken = _downloadCTS.Token,
        };
        if (!(await ParallelDownloadAsync(patchInfos, options, folder, ispred)))
        {
            Logger.WriteError("下载差异文件取消或出现异常");
            return false;
        }
        return true;
    }

    private async Task<bool> DownloadPatcheToResource(string folder, PatchIndexGameResource patch)
    {
        var patchInfos = patch.GroupInfos.ToList();

        for (int i = 0; i < patchInfos.Count(); i++)
        {
            var downloadUrl = _downloadBaseUrl + patchInfos[i].Dest;
            var filePath = BuildFileHelper.BuildFilePath(folder, patchInfos[i]);
            string? krdiffPath = "";
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            krdiffPath = await DownloadFileByKrDiff(patchInfos[i].Dest, filePath);
            if (krdiffPath == null)
            {
                Logger.WriteError("下载差异文件取消或出现异常");
                return false;
            }
            await DecompressKrdiffFile(folder, filePath, i, patchInfos.Count);
        }
        return true;
    }

    private async Task<int> DecompressKrdiffFile(
        string folder,
        string? krdiffPath,
        int curent,
        int total,
        string? tempFolder = null
    )
    {
        if (krdiffPath == null)
            return -1000;
        DiffDecompressManager manager = new DiffDecompressManager(
            folder,
            tempFolder ?? folder,
            krdiffPath
        );
        IProgress<(double, double)> progress = new Progress<(double, double)>();
        ((Progress<(double, double)>)progress).ProgressChanged += async (s, e) =>
        {
            if (gameContextOutputDelegate == null)
                return;
            await gameContextOutputDelegate
                .Invoke(
                    this,
                    new GameContextOutputArgs
                    {
                        Type = GameContextActionType.Decompress,
                        CurrentSize = (long)e.Item1,
                        TotalSize = (long)e.Item2,
                        DownloadSpeed = 0,
                        VerifySpeed = 0,
                        RemainingTime = TimeSpan.FromMicroseconds(0),
                        IsAction = _downloadState?.IsActive ?? false,
                        IsPause = _downloadState?.IsPaused ?? false,
                        TipMessage = "正在解压合并资源",
                        CurrentDecompressCount = curent,
                        MaxDecompressValue = total,
                    }
                )
                .ConfigureAwait(false);
        };
        var result = await manager.StartAsync(progress);
        Logger.WriteInfo($"解压程序结果{result}");
        return result;
    }

    private async Task<bool> UpdateGameToResources(string folder, List<IndexResource> resource)
    {
        _downloadState.IsActive = true;
        _totalProgressTotal = resource.Sum(x => x.Size);
        await UpdateFileProgress(GameContextActionType.Verify, 0);

        #region 下载逻辑
        try
        {
            for (int i = 0; i < resource.Count; i++)
            {
                Logger.WriteInfo($"开始处理更新文件{resource[i].Dest}");
                if (IsDownloadCanceled())
                {
                    this._downloadState.IsActive = false;
                    await SetNoneStatusAsync().ConfigureAwait(false);
                    return false;
                }
                var filePath = BuildFileHelper.BuildFilePath(folder, resource[i]);
                if (File.Exists(filePath))
                {
                    if (resource[i].ChunkInfos == null)
                    {
                        var checkResult = await VaildateFullFile(resource[i].Md5, filePath);
                        if (checkResult)
                        {
                            await DownloadFileByFull(
                                resource[i].Dest,
                                resource[i].Size,
                                filePath,
                                new()
                                {
                                    Start = 0,
                                    End = resource[i].Size - 1,
                                    Md5 = resource[i].Md5,
                                }
                            );
                        }
                        else
                        {
                            await UpdateFileProgress(
                                    GameContextActionType.Verify,
                                    resource[i].Size,
                                    true
                                )
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        for (int c = 0; c < resource[i].ChunkInfos.Count; c++)
                        {
                            var fileName = System.IO.Path.GetFileName(filePath);
                            var needDownload = await ValidateFileChunks(
                                resource[i].ChunkInfos[c],
                                filePath
                            );
                            if (needDownload)
                            {
                                if (i == resource[i].ChunkInfos.Count - 1)
                                {
                                    await DownloadFileByChunks(
                                        resource[i].Dest,
                                        filePath,
                                        resource[i].ChunkInfos[c].Start,
                                        resource[i].ChunkInfos[c].End,
                                        true,
                                        resource[i].Size
                                    );
                                }
                                else
                                {
                                    await DownloadFileByChunks(
                                        resource[i].Dest,
                                        filePath,
                                        resource[i].ChunkInfos[c].Start,
                                        resource[i].ChunkInfos[c].End,
                                        false
                                    );
                                }
                            }
                            else
                            {
                                await UpdateFileProgress(
                                        GameContextActionType.Verify,
                                        resource[i].ChunkInfos[c].End
                                            - resource[i].ChunkInfos[c].Start,
                                        true
                                    )
                                    .ConfigureAwait(false);
                            }
                        }
                    }
                }
                else
                {
                    await DownloadFileByFull(
                        resource[i].Dest,
                        resource[i].Size,
                        filePath,
                        new IndexChunkInfo()
                        {
                            Start = 0,
                            End = resource[i].Size - 1,
                            Md5 = resource[i].Md5,
                        }
                    );
                }
            }
        }
        catch (IOException ex)
        {
            Debug.WriteLine(ex.Message);
            await this.SetNoneStatusAsync().ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            _downloadState.IsActive = false;
            await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "False");
            await SetNoneStatusAsync().ConfigureAwait(false);
            return false;
        }
        catch (Exception ex)
        {
            Logger.WriteError(ex.Message);
            _downloadState.IsActive = false;
            await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "False");
            await SetNoneStatusAsync().ConfigureAwait(false);
            return false;
        }
        finally
        {
            _downloadCTS?.Dispose();
            _downloadCTS = null;
            _isDownload = false;
        }
        #endregion
        _downloadState.IsActive = false;
        return true;
    }

    async Task CancelDownloadAsync()
    {
        if (this.gameContextOutputDelegate != null)
        {
            await this
                .gameContextOutputDelegate.Invoke(
                    this,
                    new GameContextOutputArgs()
                    {
                        Type = GameContextActionType.None,
                        ErrorString = "未找到版本更新信息！",
                    }
                )
                .ConfigureAwait(false);
        }
        this._isDownload = false;
        _downloadState?.IsStop = true;
        if (_downloadCTS != null)
        {
            await _downloadCTS.CancelAsync();
            _downloadCTS.Dispose();
            _downloadCTS = null;
        }
        return;
    }

    #region 校验逻辑
    /// <summary>
    /// 校验
    /// </summary>
    /// <param name="file"></param>
    /// <param name="filePath"></param>
    /// <returns></returns>
    private async Task<bool> ValidateFileChunks(
        IndexChunkInfo file,
        string filePath,
        bool isPred = false
    )
    {
        using (
            var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                262144,
                true
            )
        )
        {
            try
            {
                //if (fs.Length < file.End + 1) // 检查文件长度是否足够
                //{
                //    Debug.WriteLine($"文件长度不足: {fs.Length} < {file.End + 1}");
                //    return true;
                //}
                var memoryPool = ArrayPool<byte>.Shared;
                var downloadCts = _downloadCTS;
                if (downloadCts == null || _downloadState?.IsStop == true)
                {
                    throw new OperationCanceledException();
                }
                long offset = file.Start;
                long remaining = file.End - file.Start + 1;
                bool isValid = true;
                fs.Seek(offset, SeekOrigin.Begin);
                using (var md5 = MD5.Create())
                {
                    long accumulatedBytes = 0L;
                    while (remaining > 0 && isValid)
                    {
                        await this._downloadState.PauseToken.WaitIfPausedAsync();
                        var buffer = memoryPool.Rent(MaxBufferSize);
                        try
                        {
                            if (downloadCts.IsCancellationRequested || _downloadState?.IsStop == true)
                            {
                                throw new OperationCanceledException();
                            }
                            int bytesRead = await fs.ReadAsync(
                                    buffer,
                                    0,
                                    MaxBufferSize,
                                    downloadCts.Token
                                )
                                .ConfigureAwait(false);
                            if (bytesRead == 0)
                            {
                                break;
                            }
                            md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                            remaining -= bytesRead;
                            accumulatedBytes += bytesRead;
                            if (accumulatedBytes >= UpdateThreshold)
                            {
                                await UpdateFileProgress(
                                        GameContextActionType.Verify,
                                        accumulatedBytes,
                                        false,
                                        isPred
                                    )
                                    .ConfigureAwait(false);
                                accumulatedBytes = 0;
                            }
                        }
                        catch (IOException ex)
                        {
                            Logger.WriteError(ex.Message);
                        }
                        finally
                        {
                            memoryPool.Return(buffer);
                        }
                    }
                    if (accumulatedBytes > 0 && accumulatedBytes < UpdateThreshold)
                    {
                        await UpdateFileProgress(
                                GameContextActionType.Verify,
                                accumulatedBytes,
                                false,
                                isPred
                            )
                            .ConfigureAwait(false);
                    }
                    md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    string hash = BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower();
                    isValid = hash == file.Md5.ToLower();
                    Logger.WriteInfo($"分片校验结果{hash}|{file.Md5}");
                    return !isValid;
                }
            }
            catch (IOException ex)
            {
                Logger.WriteError(ex.Message);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            finally
            {
                fs.Close();
                fs.Dispose();
            }
        }
    }

    private async Task<bool> VaildateFullFile(string md5Value, string filePath, bool isPred = false)
    {
        const int bufferSize = 262144; // 80KB缓冲区
        using var md5 = MD5.Create();
        var memoryPool = ArrayPool<byte>.Shared;
        var downloadCts = _downloadCTS;
        if (downloadCts == null || _downloadState?.IsStop == true)
        {
            throw new OperationCanceledException();
        }
        const long UpdateThreshold = 1048576; // 1MB进度更新阈值
        FileStream? fs = null;
        try
        {
            using (
                fs = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: bufferSize,
                    true
                )
            )
            {
                bool isBreak = false;
                long accumulatedBytes = 0L;
                while (true)
                {
                    if (downloadCts.IsCancellationRequested || _downloadState?.IsStop == true)
                    {
                        throw new OperationCanceledException();
                    }
                    //暂停锁
                    await this._downloadState.PauseToken.WaitIfPausedAsync();
                    byte[] buffer = memoryPool.Rent(bufferSize);
                    try
                    {
                        int bytesRead = await fs.ReadAsync(
                                buffer.AsMemory(0, bufferSize),
                                downloadCts.Token
                            )
                            .ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            isBreak = true;
                            break;
                        }
                        md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                        accumulatedBytes += bytesRead; // 添加此行以累加字节数
                        if (accumulatedBytes >= UpdateThreshold)
                        {
                            await UpdateFileProgress(
                                    GameContextActionType.Verify,
                                    accumulatedBytes,
                                    false,
                                    isPred
                                )
                                .ConfigureAwait(false);
                            accumulatedBytes = 0;
                        }
                    }
                    finally
                    {
                        memoryPool.Return(buffer);
                    }
                }
                if (accumulatedBytes < UpdateThreshold)
                {
                    await UpdateFileProgress(
                            GameContextActionType.Verify,
                            accumulatedBytes,
                            false,
                            isPred: isPred
                        )
                        .ConfigureAwait(false);
                }
            }

            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            string hash = BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower();

            return !(hash == md5Value);
        }
        catch (OperationCanceledException)
        {
            throw new OperationCanceledException();
        }
        catch (IOException ex)
        {
            Logger.WriteError(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.WriteError(ex.Message);
            return false;
        }
        finally
        {
            fs?.Close();
            fs?.Dispose();
        }
    }

    #endregion

    #region 下载逻辑
    private async Task DownloadFileByChunks(
        string dest,
        string filePath,
        long start,
        long end,
        bool isLast = false,
        long allSize = 0L,
        bool isPred = false
    )
    {
        using (
            var fileStream = new FileStream(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                262144,
                true
            )
        )
        {
            try
            {
                var downloadCts = _downloadCTS;
                if (downloadCts == null || _downloadState?.IsStop == true)
                {
                    throw new OperationCanceledException();
                }
                long accumulatedBytes = 0;
                if (start == 0 && end == -1)
                {
                    Logger.WriteError($"文件{filePath}，分片数据错误，start={start},end={end}");
                }
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    _downloadBaseUrl.TrimEnd('/') + "/" + dest.TrimStart('/')
                );
                request.Headers.Range = new RangeHeaderValue(start, end);
                using var response = await HttpClientService.GameDownloadClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    downloadCts.Token
                );
                var stream = await response.Content.ReadAsStreamAsync(downloadCts.Token);
                if (start < 0 || end < start)
                {
                    Logger.WriteError($"分片范围无效: {start}-{end}");
                    throw new ArgumentException($"分片范围无效: {start}-{end}");
                }

                long totalWritten = 0;
                long chunkTotalSize = end - start + 1;
                var memoryPool = ArrayPool<byte>.Shared;
                fileStream.Seek(start, SeekOrigin.Begin);
                bool isBreak = false;
                while (totalWritten < chunkTotalSize)
                {
                    if (downloadCts.IsCancellationRequested || _downloadState?.IsStop == true)
                    {
                        throw new OperationCanceledException();
                    }
                    await _downloadState.PauseToken.WaitIfPausedAsync().ConfigureAwait(false); // 暂停检查也异步化
                    int bytesToRead = (int)Math.Min(MaxBufferSize, chunkTotalSize - totalWritten);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bytesToRead);
                    try
                    {
                        int bytesRead = await stream
                            .ReadAsync(buffer.AsMemory(0, bytesToRead), downloadCts.Token)
                            .ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            isBreak = true;
                            break;
                        }
                        await _downloadState
                            .SpeedLimiter.LimitAsync(bytesRead)
                            .ConfigureAwait(false);
                        await fileStream
                            .WriteAsync(buffer.AsMemory(0, bytesRead), downloadCts.Token)
                            .ConfigureAwait(false);
                        totalWritten += bytesRead;
                        accumulatedBytes += bytesRead;
                        if (accumulatedBytes >= UpdateThreshold)
                        {
                            await UpdateFileProgress(
                                    GameContextActionType.Download,
                                    accumulatedBytes,
                                    true,
                                    isPred
                                )
                                .ConfigureAwait(false);
                            accumulatedBytes = 0;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteError(ex.Message);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    await UpdateFileProgress(
                            GameContextActionType.Download,
                            accumulatedBytes,
                            isPred: isPred
                        )
                        .ConfigureAwait(false);
                }
                if (isLast)
                    fileStream.SetLength(allSize);
                stream.Close();
                await stream.DisposeAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                await fileStream.FlushAsync(_downloadCTS?.Token ?? CancellationToken.None);
                await fileStream.FlushAsync();
                await fileStream.DisposeAsync();
            }
        }
    }

    public async Task SetNoneStatusAsync(bool isPred = false)
    {
        if (this._downloadState != null)
        {
            this._downloadState.IsActive = false;
            if (!this._downloadState.IsStop)
            {
                this._downloadState.IsStop = false;
            }
        }
        if (this.gameContextOutputDelegate != null && !isPred)
        {
            await this.gameContextOutputDelegate.Invoke(
                this,
                new GameContextOutputArgs()
                {
                    Type = GameContextActionType.None,
                    CurrentSize = _totalProgressSize,
                    TotalSize = _totalfileSize,
                    DownloadSpeed = _downloadSpeed,
                    VerifySpeed = VerifySpeed,
                    RemainingTime = this.RemainingTime,
                }
            );
        }
        else
        {
            await this.gameContextProdOutputDelegate.Invoke(
                this,
                new GameContextOutputArgs()
                {
                    Type = GameContextActionType.None,
                    CurrentSize = _totalProgressSize,
                    TotalSize = _totalfileSize,
                    DownloadSpeed = _downloadSpeed,
                    VerifySpeed = VerifySpeed,
                    RemainingTime = this.RemainingTime,
                }
            );
        }
    }

    public async Task SetSpeedLimitAsync(long bytesPerSecond)
    {
        await _downloadState.SetSpeedLimitAsync(bytesPerSecond);
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LimitSpeed,
            bytesPerSecond.ToString()
        );
    }

    private async Task DownloadFileByFull(
        string dest,
        long size,
        string filePath,
        IndexChunkInfo chunk,
        bool ispred = false
    )
    {
        long accumulatedBytes = 0;
        using (
            var fileStream = new FileStream(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                262144,
                true
            )
        )
        {
            try
            {
                if (chunk.Start == 0 && chunk.End == -1)
                {
                    Logger.WriteError(
                        $"文件{filePath}，分片数据错误，start={chunk.Start},end={chunk.End}"
                    );
                    return;
                }
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    _downloadBaseUrl.TrimEnd('/') + "/" + dest.TrimStart('/')
                );
                request.Headers.Range = new RangeHeaderValue(chunk.Start, chunk.End);
                using var response = await HttpClientService
                    .GameDownloadClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        _downloadCTS.Token
                    )
                    .ConfigureAwait(false); // 非UI上下文切换

                response.EnsureSuccessStatusCode();
                var stream = await response
                    .Content.ReadAsStreamAsync(_downloadCTS.Token)
                    .ConfigureAwait(false);
                if (chunk.Start < 0 || chunk.End < chunk.Start)
                {
                    Logger.WriteError($"分片范围无效，start={chunk.Start},end={chunk.End}");
                    throw new ArgumentException($"分片范围无效: {chunk.Start}-{chunk.End}");
                }

                long totalWritten = 0;
                long chunkTotalSize = chunk.End - chunk.Start + 1;
                var memoryPool = ArrayPool<byte>.Shared;

                fileStream.Seek(chunk.Start, SeekOrigin.Begin);
                bool isBreak = false;
                while (totalWritten < chunkTotalSize)
                {
                    if (_downloadCTS.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }
                    await _downloadState.PauseToken.WaitIfPausedAsync().ConfigureAwait(false); // 暂停检查也异步化
                    int bytesToRead = (int)Math.Min(MaxBufferSize, chunkTotalSize - totalWritten);
                    byte[] buffer = memoryPool.Rent(bytesToRead);
                    int bytesRead = await stream
                        .ReadAsync(buffer.AsMemory(0, bytesToRead), _downloadCTS.Token)
                        .ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        isBreak = true;
                    }
                    await _downloadState.SpeedLimiter.LimitAsync(bytesRead).ConfigureAwait(false);
                    await fileStream
                        .WriteAsync(buffer.AsMemory(0, bytesRead), _downloadCTS.Token)
                        .ConfigureAwait(false);
                    totalWritten += bytesRead;
                    accumulatedBytes += bytesRead;
                    if (accumulatedBytes >= UpdateThreshold)
                    {
                        await UpdateFileProgress(
                                GameContextActionType.Download,
                                accumulatedBytes,
                                true,
                                ispred
                            )
                            .ConfigureAwait(false);
                        accumulatedBytes = 0; // 重置累积计数器
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    await UpdateFileProgress(
                            GameContextActionType.Download,
                            accumulatedBytes,
                            true,
                            ispred
                        )
                        .ConfigureAwait(false);
                }
                if (totalWritten != chunkTotalSize)
                {
                    throw new IOException($"分片写入不完整: {totalWritten}/{chunkTotalSize}");
                }
                fileStream.SetLength(size);
            }
            catch (Exception ex)
            {
                Logger.WriteError($"下载文件{filePath}出现异常" + ex.Message);
            }
            finally
            {
                await fileStream.FlushAsync().ConfigureAwait(false);
                await fileStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<string?> DownloadFileByKrDiff(string dest, string filePath)
    {
        long accumulatedBytes = 0;
        using (
            var fileStream = new FileStream(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                262144,
                true
            )
        )
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    _downloadBaseUrl.TrimEnd('/') + "/" + dest.TrimStart('/')
                );
                using var response = await HttpClientService
                    .GameDownloadClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        _downloadCTS.Token
                    )
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var stream = await response
                    .Content.ReadAsStreamAsync(_downloadCTS.Token)
                    .ConfigureAwait(false);
                long totalWritten = 0;
                long chunkTotalSize = long.Parse(
                    response.Content.Headers.GetValues("Content-Length").First()
                );
                var memoryPool = ArrayPool<byte>.Shared;
                fileStream.Seek(0, SeekOrigin.Begin);
                bool isBreak = false;
                while (totalWritten < chunkTotalSize)
                {
                    if (_downloadCTS.IsCancellationRequested)
                    {
                        return null;
                    }
                    await _downloadState.PauseToken.WaitIfPausedAsync().ConfigureAwait(false); // 暂停检查也异步化
                    int bytesToRead = (int)Math.Min(MaxBufferSize, chunkTotalSize - totalWritten);
                    byte[] buffer = memoryPool.Rent(bytesToRead);
                    int bytesRead = await stream
                        .ReadAsync(buffer.AsMemory(0, bytesToRead), _downloadCTS.Token)
                        .ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        isBreak = true;
                    }
                    await _downloadState.SpeedLimiter.LimitAsync(bytesRead).ConfigureAwait(false);
                    await fileStream
                        .WriteAsync(buffer.AsMemory(0, bytesRead), _downloadCTS.Token)
                        .ConfigureAwait(false);
                    totalWritten += bytesRead;
                    accumulatedBytes += bytesRead;
                    if (accumulatedBytes >= UpdateThreshold)
                    {
                        await UpdateFileProgress(
                                GameContextActionType.Download,
                                accumulatedBytes,
                                true,
                                false,
                                "下载差异文件"
                            )
                            .ConfigureAwait(false);
                        accumulatedBytes = 0;
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    await UpdateFileProgress(
                            GameContextActionType.Download,
                            accumulatedBytes,
                            true,
                            false,
                            "下载差异文件"
                        )
                        .ConfigureAwait(false);
                }
                if (totalWritten != chunkTotalSize)
                {
                    throw new IOException($"分片写入不完整: {totalWritten}/{chunkTotalSize}");
                }
                await fileStream.FlushAsync();
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.WriteError(ex.Message);
                return null;
            }
            finally
            {
                await fileStream.FlushAsync();
                await fileStream.DisposeAsync();
            }
        }
    }
    #endregion

    #region 辅助方法

    private bool IsDownloadCanceled()
    {
        return _downloadCTS == null || _downloadCTS.IsCancellationRequested || _downloadState?.IsStop == true;
    }

    private async Task UpdateFileProgress(
        GameContextActionType type,
        long fileSize,
        bool isAdd = true,
        bool isPred = false,
        string tip = ""
    )
    {
        if (type == GameContextActionType.Download)
        {
            Interlocked.Add(ref _totalDownloadedBytes, fileSize);
            if (isAdd)
                Interlocked.Add(ref _totalProgressSize, fileSize);
        }
        else if (type == GameContextActionType.Verify)
        {
            if (!isAdd)
                Interlocked.Add(ref _totalVerifiedBytes, fileSize);
            if (isAdd)
                Interlocked.Add(ref _totalProgressSize, fileSize);
        }
        var elapsed = (DateTime.Now - _lastSpeedUpdateTime).TotalSeconds;
        if (elapsed >= 1)
        {
            _downloadSpeed = _totalDownloadedBytes / elapsed;
            _verifySpeed = _totalVerifiedBytes / elapsed;
            // 重置计数器和时间
            Interlocked.Exchange(ref _totalDownloadedBytes, 0);
            Interlocked.Exchange(ref _totalVerifiedBytes, 0);
            var currentBytes = Interlocked.Read(ref _totalDownloadedBytes);
            _lastSpeedBytes = currentBytes;
            _lastSpeedUpdateTime = DateTime.Now;
        }

        if (isPred && gameContextProdOutputDelegate != null)
        {
            await gameContextProdOutputDelegate
                .Invoke(
                    this,
                    new GameContextOutputArgs
                    {
                        Type = type,
                        CurrentSize = _totalProgressSize,
                        TotalSize = _totalfileSize,
                        DownloadSpeed = _downloadSpeed,
                        VerifySpeed = _verifySpeed,
                        RemainingTime = RemainingTime,
                        IsAction = _downloadState?.IsActive ?? false,
                        IsPause = _downloadState?.IsPaused ?? false,
                        TipMessage = tip,
                    }
                )
                .ConfigureAwait(false);
        }
        else if (isPred == false && gameContextOutputDelegate != null)
        {
            await gameContextOutputDelegate
                .Invoke(
                    this,
                    new GameContextOutputArgs
                    {
                        Type = type,
                        CurrentSize = _totalProgressSize,
                        TotalSize = _totalfileSize,
                        DownloadSpeed = _downloadSpeed,
                        VerifySpeed = _verifySpeed,
                        RemainingTime = RemainingTime,
                        IsAction = _downloadState?.IsActive ?? false,
                        IsPause = _downloadState?.IsPaused ?? false,
                        TipMessage = tip,
                    }
                )
                .ConfigureAwait(false);
        }
    }

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

    #endregion

    #region 公共辅助方法


    private async Task InitializeProgress(List<IndexResource> resource)
    {
        _totalfileSize = resource.Sum(x => x.Size);
        _totalFileTotal = resource.Count - 1;
        _totalProgressSize = 0L;
        _totalProgressTotal = 0L;
        _totalVerifiedBytes = 0;
        _totalDownloadedBytes = 0;
        this._downloadState = new DownloadState();
        await _downloadState.SetSpeedLimitAsync(this.SpeedValue);
        if (gameContextOutputDelegate == null)
            return;
        await gameContextOutputDelegate.Invoke(
            this,
            new GameContextOutputArgs
            {
                CurrentSize = 0,
                TotalSize = resource.Sum(x => x.Size),
                Type = GameContextActionType.Download,
            }
        );
    }
    #endregion

    public async Task RepirGameAsync()
    {
        Logger.WriteInfo("开始修复游戏");
        var installFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        var launcher = await this.GetGameLauncherSourceAsync();
        if (launcher == null)
        {
            Logger.WriteInfo("无网络，无法拉取文件列表");
            return;
        }
        await Task.Run(async () => await StartDownloadTaskAsync(installFolder, launcher, true));
    }
}
