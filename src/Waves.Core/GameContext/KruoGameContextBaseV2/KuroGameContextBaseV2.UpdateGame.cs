using System.Buffers.Text;
using System.Text.RegularExpressions;
using Waves.Api.Models;
using Waves.Core.Common;
using Waves.Core.Common.Downloads;
using Waves.Core.GameContext.Common.FilesAction;
using Waves.Core.GameContext.KruoGameContextBaseV2.Common;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

partial class KuroGameContextBaseV2
{
    #region 更新方法
    public async Task<bool> UpdateGameResourceAsync()
    {
        var _launcher = await this.GetGameLauncherSourceAsync();
        var currentVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );

        #region 获取配置
        DownloadState = new DownloadState();
        DownloadState.IsActive = true;
        if (_launcher == null || string.IsNullOrWhiteSpace(currentVersion))
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return false;
        }

        var previous = _launcher
            .ResourceDefault.Config.PatchConfig.Where(x => x.Version == currentVersion)
            .FirstOrDefault();
        if (previous == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return false;
        }
        var cdnUrl =
            _launcher
                .ResourceDefault.CdnList.Where(x => x.P != 0)
                .OrderBy(x => x.P)
                .FirstOrDefault()
            ?? null;
        if (cdnUrl == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return false;
        }
        var _patch = await GetPatchGameResourceAsync(cdnUrl.Url + previous.IndexFile);
        if (_patch == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return false;
        }
        #endregion
        Task.Run(async () =>
            await StartDownloadUpdateGameResourceAsync(
                _launcher,
                currentVersion,
                previous,
                _patch,
                false
            )
        );
        return true;
    }

    /// <summary>
    /// 预下载
    /// </summary>
    /// <returns></returns>
    public async Task<bool> StartProdDownloadGameResourceAsync()
    {
        var _launcher = await this.GetGameLauncherSourceAsync();
        var currentVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );

        Task.Run(async () =>
        {
            await StartProdDownloadGameResourceAsync(_launcher, currentVersion);
        });
        return true;
    }

    private async Task<bool> StartProdDownloadGameResourceAsync(
        GameLauncherSource _launcher,
        string currentVersion
    )
    {
        //var previous = _launcher
        //    .Predownload.Config.PatchConfig.Where(x => x.Version == currentVersion)
        //    .FirstOrDefault();
        var previous = _launcher
            .ResourceDefault.Config.PatchConfig.Where(x => x.Version == currentVersion)
            .FirstOrDefault();
        if (previous == null)
        {
            return false;
        }
        if (previous == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return false;
        }
        var cdnUrl =
            _launcher
                .ResourceDefault.CdnList.Where(x => x.P != 0)
                .OrderBy(x => x.P)
                .FirstOrDefault()
            ?? null;
        if (cdnUrl == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return false;
        }
        var _patch = await GetPatchGameResourceAsync(cdnUrl.Url + previous.IndexFile);
        if (_patch == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return false;
        }
        Task.Run(async () =>
            await StartDownloadUpdateGameResourceAsync(
                _launcher,
                currentVersion,
                previous,
                _patch,
                true
            )
        );
        return true;
    }

    /// <summary>
    /// 更新游戏
    /// </summary>
    /// <param name="_launcher"></param>
    /// <param name="currentVersion"></param>
    /// <param name="isProd"></param>
    /// <returns></returns>
    private async Task<bool> StartDownloadUpdateGameResourceAsync(
        GameLauncherSource _launcher,
        string currentVersion,
        PatchConfig previous,
        PatchIndexGameResource _patch,
        bool isProd = false
    )
    {
        try
        {
            #region 初始化资源
            var baseFolder = await this.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder
            );
            this.DownloadState = new DownloadState();
            if (isProd)
            {
                _prodDownloadCts = new CancellationTokenSource();
                DownloadState.CancelToken = _prodDownloadCts;
            }
            else
            {
                _downloadCts = new CancellationTokenSource();
                DownloadState.CancelToken = _downloadCts;
            }
            var downloadResource = new List<IndexResource>();
            var patchResource = new List<IndexResource>();
            var groupResource = new List<IndexResource>();
            var zipResource = new List<IndexResource>();

            foreach (var x in _patch.Resource)
            {
                if (x.Dest.Contains("krdiff"))
                    patchResource.Add(x);
                else if (x.Dest.Contains("krpdiff"))
                    groupResource.Add(x);
                else if (x.Dest.Contains("krzip"))
                    zipResource.Add(x);
                else
                    downloadResource.Add(x);
            }
            string downloadBaseFolder = "";
            if (isProd)
            {
                downloadBaseFolder = Path.Combine(baseFolder, "prodDownloads");
            }
            else
            {
                downloadBaseFolder = Path.Combine(baseFolder, "downloads");
            }
            if (isProd)
            {
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.ProdDownloadPath,
                    downloadBaseFolder
                );
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.ProdDownloadFolderDone,
                    "False"
                );
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.ProdDownloadVersion,
                    previous.Version
                );
            }
            DownloadUpdateFolderConfig folderConfig = new();
            #endregion

            #region 初始化步骤显示
            var downloadTasks =
                new List<(
                    IEnumerable<IndexResource> Items,
                    string Name,
                    string Folder,
                    string baseUrl,
                    bool isResource
                )>();
            if (patchResource.Any())
            {
                this.Setups.Add("下载补丁文件");
                folderConfig.PatchFolder = Path.Combine(downloadBaseFolder, "patchs");
                downloadTasks.Add(
                    (
                        patchResource,
                        "下载补丁文件",
                        folderConfig.PatchFolder,
                        previous.BaseUrl,
                        false
                    )
                );
            }
            if (groupResource.Any())
            {
                this.Setups.Add("下载补丁组文件");
                folderConfig.PatchGroupFolder = Path.Combine(downloadBaseFolder, "patchGroup");
                downloadTasks.Add(
                    (
                        groupResource,
                        "下载补丁组文件",
                        folderConfig.PatchGroupFolder,
                        previous.BaseUrl,
                        false
                    )
                );
            }
            if (zipResource.Any())
            {
                this.Setups.Add("下载压缩包更新文件");
                folderConfig.ZipFolder = Path.Combine(downloadBaseFolder, "zips");
                downloadTasks.Add(
                    (
                        zipResource,
                        "下载压缩包更新文件",
                        folderConfig.ZipFolder,
                        previous.BaseUrl,
                        false
                    )
                );
            }
            if (downloadResource.Any())
            {
                this.Setups.Add("下载更新文件");
                folderConfig.DownloadFolder = Path.Combine(downloadBaseFolder, "resources");
                downloadTasks.Add(
                    (
                        downloadResource,
                        "下载更新文件",
                        folderConfig.DownloadFolder,
                        _launcher.ResourceDefault.ResourcesBasePath,
                        true
                    )
                );
            }
            #endregion


            #region  下载资源
            for (int i = 0; i < downloadTasks.Count; i++)
            {
                if (DownloadState.CancelToken.IsCancellationRequested)
                {
                    this.GameEventPublisher.Publish(new() { Type = GameContextActionType.None });
                    DownloadState.IsActive = false;
                    DownloadState.IsStop = true;
                    return false;
                }
                var downloadMethod = new DownloadAndVerifyResource(this.Logger)
                {
                    ProgressName = downloadTasks[i].Name,
                };
                GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = GameContextActionType.CdnSelect,
                        TipMessage = "正在选择最优CDN",
                        Prod = isProd,
                    }
                );
                var cdn = await GetBaseUrl(
                    _launcher,
                    _launcher.ResourceDefault.ResourcesBasePath,
                    previous.BaseUrl,
                    downloadTasks[i].Items.ToList(),
                    downloadTasks[i].isResource,
                    isProd
                );
                if (string.IsNullOrWhiteSpace(cdn))
                {
                    this.GameEventPublisher.Publish(
                        new GameContextOutputArgs()
                        {
                            Type = GameContextActionType.TipMessage,
                            TipMessage = "未找到可用的CDN地址，无法进行下载",
                        }
                    );
                    return false;
                }
                downloadMethod.SetParam(
                    new Dictionary<string, object>()
                    {
                        { "resource", downloadTasks[i].Items },
                        { "launcher", _launcher },
                        { "isDelete", false },
                        { "folder", downloadTasks[i].Folder },
                        { "httpClient", HttpClientService! },
                        { "downloadState", DownloadState! },
                        { "baseUrl", cdn },
                        { "isProd", isProd },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = downloadMethod;
                CurrentSetups = i;
                await this.GameEventPublisher.PublishStepAsync(
                    downloadTasks[i].Name,
                    CurrentSetups,
                    Setups,
                    isProd: isProd
                );
                await Task.Delay(100);
                await downloadMethod.ExecuteAsync(true);
            }
            #endregion

            #region 安装资源
            if (isProd) //如果是预下载则跳出
            {
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.ProdDownloadFolderDone,
                    "True"
                );
                //await this.GameLocalConfig.SaveConfigAsync(
                //    GameLocalSettingName.ProdDownloadVersion,
                //    _launcher.Predownload.Version
                //);
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.ProdDownloadVersion,
                    _launcher.Predownload.Version
                );
                this.GameEventPublisher.Publish(
                    new GameContextOutputArgs() { Type = GameContextActionType.None, Prod = isProd }
                );
                return true;
            }
            else
            {
                await this.StartInstallGameResource(_launcher, previous, _patch);
            }
            DownloadState.IsActive = false;
            #endregion
            return true;
        }
        catch (TaskCanceledException)
        {
            DownloadState.IsStop = true;
            return false;
        }
        catch (Exception)
        {
            DownloadState!.IsActive = false;
            return false;
        }
    }

    public async Task<string?> GetBaseUrl(
        GameLauncherSource _launcher,
        string resourceUrl,
        string preiveResource,
        List<IndexResource> resources,
        bool isResource = false,
        bool isProd = false
    )
    {
        try
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = GameContextActionType.CdnSelect,
                    TipMessage = "正在选择最优CDN",
                    Prod = isProd,
                }
            );
            var cdnResult = await TestCdnAsync(
                _launcher.ResourceDefault.CdnList,
                isResource ? resourceUrl : preiveResource,
                resources
            );
            if (cdnResult == null || !cdnResult.Value.Success)
            {
                this.GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = GameContextActionType.TipMessage,
                        TipMessage = "未找到可用的CDN地址，无法进行下载",
                        Prod = isProd,
                    }
                );
            }
            var baseUrl = cdnResult!.Value.Url + (isResource ? resourceUrl : preiveResource);
            return baseUrl;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 开始安装游戏资源
    /// </summary>
    /// <param name="launcher"></param>
    /// <param name="previous"></param>
    /// <param name="patch"></param>
    /// <param name="isProd"></param>
    /// <returns></returns>
    public async Task StartInstallGameResource(
        GameLauncherSource launcher,
        PatchConfig previous,
        PatchIndexGameResource patch,
        bool isProd = false
    )
    {
        #region 获取资源
        var baseFolder = await this.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        if (baseFolder == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到游戏安装路径，无法进行安装",
                }
            );
            GameEventPublisher.Publish(
                new GameContextOutputArgs() { Type = Models.Enums.GameContextActionType.None }
            );

            return;
        }
        #endregion

        DownloadUpdateFolderConfig folderConfig = new();
        #region 初始化资源
        this.DownloadState = new DownloadState();
        this._installGameResourceCts = new CancellationTokenSource();
        this.DownloadState.CancelToken = _installGameResourceCts;
        var downloadResource = new List<IndexResource>();
        var patchResource = new List<IndexResource>();
        var groupResource = new List<IndexResource>();
        var zipResource = new List<IndexResource>();
        foreach (var x in patch.Resource)
        {
            if (x.Dest.Contains("krdiff"))
                patchResource.Add(x);
            else if (x.Dest.Contains("krpdiff"))
                groupResource.Add(x);
            else if (x.Dest.Contains("krzip"))
                zipResource.Add(x);
            else
                downloadResource.Add(x);
        }
        string downloadBaseFolder = "";
        if (isProd)
        {
            downloadBaseFolder = Path.Combine(baseFolder, "prodDownloads");
        }
        else
        {
            downloadBaseFolder = Path.Combine(baseFolder, "downloads");
        }
        GameEventPublisher.Publish(
            new GameContextOutputArgs()
            {
                Type = GameContextActionType.CdnSelect,
                TipMessage = "正在选择最优CDN",
                Prod = isProd,
            }
        );
        #endregion
        Setups.Clear();
        #region 初始化步骤显示
        var installTasks =
            new List<(
                IEnumerable<IndexResource> Items,
                string Name,
                string Folder,
                InstallGameResourceType,
                string baseUrl
            )>();
        if (patchResource.Any())
        {
            this.Setups.Add("安装补丁文件");
            folderConfig.PatchFolder = Path.Combine(downloadBaseFolder, "patchs");
            installTasks.Add(
                (
                    patchResource,
                    "安装补丁文件",
                    folderConfig.PatchFolder,
                    InstallGameResourceType.Krdiff,
                    previous.BaseUrl
                )
            );
        }
        if (groupResource.Any())
        {
            this.Setups.Add("安装补丁组文件");
            folderConfig.PatchGroupFolder = Path.Combine(downloadBaseFolder, "patchGroup");
            installTasks.Add(
                (
                    groupResource,
                    "安装补丁组文件",
                    folderConfig.PatchGroupFolder,
                    InstallGameResourceType.KrdiffGroup,
                    previous.BaseUrl
                )
            );
        }
        if (zipResource.Any())
        {
            this.Setups.Add("安装压缩包");
            folderConfig.ZipFolder = Path.Combine(downloadBaseFolder, "zips");
            installTasks.Add(
                (
                    zipResource,
                    "安装压缩包",
                    folderConfig.ZipFolder,
                    InstallGameResourceType.KrZip,
                    previous.BaseUrl
                )
            );
        }
        if (downloadResource.Any())
        {
            this.Setups.Add("移动更新文件");
            folderConfig.DownloadFolder = Path.Combine(downloadBaseFolder, "resources");
            installTasks.Add(
                (
                    downloadResource,
                    "移动更新文件",
                    folderConfig.DownloadFolder,
                    InstallGameResourceType.MoveFile,
                    previous.BaseUrl
                )
            );
        }
        this.Setups.Add("重新校验文件");
        folderConfig.DownloadFolder = baseFolder;
        var resource = await this.GetGameResourceAsync(launcher.ResourceDefault);
        if (resource != null)
        {
            installTasks.Add(
                (
                    resource!.Resource,
                    "安装压缩包",
                    baseFolder,
                    InstallGameResourceType.CheckAllFiles,
                    launcher.ResourceDefault.ResourcesBasePath
                )
            );
        }
        else
        {
            Logger.WriteError("获取资源信息失败，最终校验启动失败，跳过此校验");
        }
        for (int i = 0; i < installTasks.Count; i++)
        {
            if (DownloadState.CancelToken.IsCancellationRequested)
            {
                this.GameEventPublisher.Publish(new() { Type = GameContextActionType.None });
                DownloadState.IsActive = false;
                DownloadState.IsStop = true;
            }
            CurrentSetups = i;
            await this.GameEventPublisher.PublishStepAsync(
                installTasks[i].Name,
                CurrentSetups,
                Setups,
                isProd: isProd
            );
            await this.GameEventPublisher.PublishStepAsync(
                installTasks[i].Name,
                CurrentSetups,
                Setups,
                isProd: isProd
            );
            if (installTasks[i].Item4 == InstallGameResourceType.Krdiff)
            {
                InstallKrdiffResource installMethod = new InstallKrdiffResource(this.Logger);
                installMethod.SetParam(
                    new()
                    {
                        { "krdiffs", patchResource },
                        { "diffFolderPath", installTasks[i].Folder },
                        { "gameBaseFolder", baseFolder },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = installMethod;
                await installMethod.ExecuteAsync(true);
            }
            if (installTasks[i].Item4 == InstallGameResourceType.KrdiffGroup)
            {
                InstallKrdiffGroupResource installgroupMethod = new InstallKrdiffGroupResource(
                    this.Logger
                );
                var decompressTempFolder = Path.Combine(baseFolder, "decompressFolder");
                installgroupMethod.SetParam(
                    new()
                    {
                        { "krpdiffs", groupResource },
                        { "diffFolderPath", installTasks[i].Folder },
                        { "baseFolderPath", baseFolder },
                        { "groupFileInfos", patch.GroupInfos },
                        { "decompressTempFolder", decompressTempFolder },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = installgroupMethod;
                var runValue = await installgroupMethod.ExecuteAsync(true);
                //无论执行结果，直接删除临时解压目录
                Directory.Delete(decompressTempFolder, true);
            }
            if (installTasks[i].Item4 == InstallGameResourceType.KrZip)
            {
                InstallKrZipResource installZipMethod = new InstallKrZipResource(Logger)
                {
                    ProgressName = "安装压缩包",
                };
                await GameEventPublisher.PublisAsync(
                    GameContextActionType.BottomText,
                    "准备开始解压压缩包",
                    isProd
                );
                installZipMethod.SetParam(
                    new Dictionary<string, object>()
                    {
                        { "zipInfos", installTasks[i].Items.ToList() },
                        { "zipDownFolder", installTasks[i].Folder },
                        { "baseGamePath", baseFolder },
                        { "downloadState", DownloadState! },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = installZipMethod;
                await installZipMethod.ExecuteAsync(true);
            }
            if (installTasks[i].Item4 == InstallGameResourceType.MoveFile)
            {
                MoveFileResource moveFileMethod = new MoveFileResource(Logger)
                {
                    ProgressName = "移动文件",
                };
                Dictionary<string, string> files = new Dictionary<string, string>();
                files = installTasks[i]
                    .Items.ToDictionary(
                        x => Path.Combine(installTasks[i].Folder, x.Dest),
                        x => Path.Combine(baseFolder, x.Dest)
                    );
                moveFileMethod.SetParam(
                    new Dictionary<string, object>() { { "files", files } },
                    this.GameEventPublisher
                );
                this._currentRunningAction = moveFileMethod;
                await moveFileMethod.ExecuteAsync(true);
            }
            if (installTasks[i].Item4 == InstallGameResourceType.CheckAllFiles)
            {
                var downloadMethod = new DownloadAndVerifyResource(this.Logger)
                {
                    ProgressName = "重新校验文件",
                };
                GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = GameContextActionType.CdnSelect,
                        TipMessage = "正在选择最优CDN",
                        Prod = isProd,
                    }
                );
                var cdnResult = await TestCdnAsync(
                    launcher.ResourceDefault.CdnList,
                    launcher.ResourceDefault.ResourcesBasePath,
                    patch.Resource
                );
                if (cdnResult == null)
                {
                    Logger.WriteError("获取资源信息失败，最终校验启动失败，跳过此校验");
                    this.GameEventPublisher.Publish(
                        new GameContextOutputArgs() { Type = GameContextActionType.None }
                    );
                    return;
                }
                var baseUrl = cdnResult.Value.Url + launcher.ResourceDefault.ResourcesBasePath;
                downloadMethod.SetParam(
                    new Dictionary<string, object>()
                    {
                        { "resource", installTasks[i].Items.ToList() },
                        { "launcher", launcher },
                        { "isDelete", false },
                        { "folder", installTasks[i].Folder },
                        { "httpClient", HttpClientService! },
                        { "downloadState", DownloadState! },
                        { "baseUrl", baseUrl },
                        { "isProd", isProd },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = downloadMethod;
                await downloadMethod.ExecuteAsync(true);
            }
        }
        var writeConfig = new WriteGameResourceConfig(
            this.GameLocalConfig,
            launcher,
            this.Config,
            Logger
        );
        await writeConfig.WriteDownloadAndUpDateResultAsync(launcher);
        if (isProd)
        {
            await this.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.ProdDownloadPath, "");
            await this.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.ProdDownloadFolderDone,
                "False"
            );
            await this.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.ProdDownloadVersion,
                ""
            );
        }
        DownloadState.IsActive = false;
        //删除下载文件夹
        if (!string.IsNullOrWhiteSpace(downloadBaseFolder))
            Directory.Delete(downloadBaseFolder, true);
        this.GameEventPublisher.Publish(
            new GameContextOutputArgs() { Type = GameContextActionType.None }
        );
        #endregion
    }

    public async Task StartInstallGameResource(bool isProd = false)
    {
        var currentVersion = await this.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        var launcher = await this.GetGameLauncherSourceAsync();
        var previous = launcher
            .ResourceDefault.Config.PatchConfig.Where(x => x.Version == currentVersion)
            .FirstOrDefault();
        if (previous == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return;
        }
        var cdnUrl =
            launcher.ResourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).FirstOrDefault()
            ?? null;
        var _patch = await GetPatchGameResourceAsync(cdnUrl.Url + previous.IndexFile);
        if (_patch == null)
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = Models.Enums.GameContextActionType.TipMessage,
                    TipMessage = "未找到更新配置文件，无法进行下载",
                }
            );
            return;
        }
        await StartInstallGameResource(launcher, previous, _patch, isProd);
    }
    #endregion
}
