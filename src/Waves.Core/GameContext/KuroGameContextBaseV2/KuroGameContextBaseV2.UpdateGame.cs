using Waves.Core.Models.Options;

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

        var previous = _launcher.ResourceDefault.Config.PatchConfig.FirstOrDefault(x =>
            x.Version == currentVersion
        );
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
        var gen = Interlocked.Increment(ref _operationGeneration);
        GameContextOutputArgs.CurrentGeneration.Value = gen;
        _ = Task.Run(async () =>
            await StartDownloadUpdateGameResourceAsync(
                _launcher,
                currentVersion,
                previous,
                _patch,
                InstallOption.CreateDefault()
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
        if (_launcher == null || currentVersion == null)
        {
            Logger.WriteError("启动预下载失败，游戏配置错误");
            SystemEventPublisher.Publish(new() { Message = "启动预下载失败，游戏配置错误" });
            return false;
        }
        var gen = Interlocked.Increment(ref _operationGeneration);
        GameContextOutputArgs.CurrentGeneration.Value = gen;
        _ = Task.Run(async () =>
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
        var previous = _launcher
            .Predownload.Config.PatchConfig.Where(x => x.Version == currentVersion)
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
        _ = Task.Run(async () =>
            await StartDownloadUpdateGameResourceAsync(
                _launcher,
                currentVersion,
                previous,
                _patch,
                InstallOption.CreateProdownlad()
            )
        );
        return true;
    }

    public async Task<DownloadState> GetInitDownloadState(bool isProd = false)
    {
        var speed = await this.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LimitSpeed,
            this._downloadCts.Token
        );

        if (isProd)
        {
            if (ProdDownloadState == null)
            {
                this.ProdDownloadState = new DownloadState();
                if (double.TryParse(speed, out var speedValue) && speedValue != 0)
                {
                    await this.ProdDownloadState.SetSpeedLimitAsync((long)speedValue * 1024 * 1024);
                }
                this.ProdDownloadState.IsActive = true;
            }
            return this.ProdDownloadState;
        }
        else
        {
            if (DownloadState == null)
            {
                this.DownloadState = new DownloadState();
                if (double.TryParse(speed, out var speedValue) && speedValue != 0)
                {
                    await this.DownloadState.SetSpeedLimitAsync((long)speedValue * 1024 * 1024);
                }
                this.DownloadState.IsActive = true;
            }
            return this.DownloadState;
        }
    }

    public string BuildInstallOptionFolder(InstallOption option, string baseFolder)
    {
        if (option.IsProd)
        {
            return Path.Combine(baseFolder, "prodDownloads");
        }
        else if (option.IsAdvance)
        {
            return Path.Combine(baseFolder, "prodDownloads");
        }
        else
        {
            return Path.Combine(baseFolder, "downloads");
        }
        return "";
    }

    /// <summary>
    /// 更新游戏
    /// </summary>
    /// <param name="_launcher"></param>
    /// <param name="currentVersion"></param>
    /// <param name="isProd"></param>
    /// <param name="isAdvance">提前安装</param>
    /// <returns></returns>
    private async Task<bool> StartDownloadUpdateGameResourceAsync(
        GameLauncherSource _launcher,
        string currentVersion,
        PatchConfig previous,
        PatchIndexGameResource _patch,
        InstallOption option
    )
    {
        try
        {
            #region 初始化资源
            this.Setups.Clear();
            this.CurrentSetups = 0;
            var baseFolder = await this.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder
            );
            this._downloadCts = new();
            var state = await GetInitDownloadState(option.IsProd);
            state.IsActive = true;
            if (option.IsProd)
            {
                _prodDownloadCts = new CancellationTokenSource();
                state.CancelToken = _prodDownloadCts;
            }
            else
            {
                _downloadCts = new CancellationTokenSource();
                state.CancelToken = _downloadCts;
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
            if (baseFolder == null)
            {
                await SetCurrentStateNull(option.IsProd);
                SystemEventPublisher.Publish(new() { Message = "未找到游戏安装文件" });
                return false;
            }
            string downloadBaseFolder = this.BuildInstallOptionFolder(option, baseFolder);
            if (option.IsProd)
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
                if (state.CancelToken.IsCancellationRequested)
                {
                    this.GameEventPublisher.Publish(new() { Type = GameContextActionType.None });
                    state.IsActive = false;
                    state.IsStop = true;
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
                        Prod = option.IsProd,
                    }
                );
                var cdn = await GetBaseUrl(
                    _launcher,
                    _launcher.ResourceDefault.ResourcesBasePath,
                    previous.BaseUrl,
                    downloadTasks[i].Items.ToList(),
                    option,
                    downloadTasks[i].isResource
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
                        { "downloadState", state },
                        { "baseUrl", cdn },
                        { "isProd", option.IsProd },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = downloadMethod;
                CurrentSetups = i;
                await this.GameEventPublisher.PublishStepAsync(
                    downloadTasks[i].Name,
                    CurrentSetups,
                    Setups,
                    isProd: option.IsProd
                );
                await Task.Delay(100);
                await downloadMethod.ExecuteAsync(true);
            }
            #endregion

            #region 安装资源
            if (option.IsProd)
            {
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.ProdDownloadFolderDone,
                    "True"
                );
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.ProdDownloadVersion,
                    _launcher.Predownload.Version
                );
                await this.GameLocalConfig.SaveConfigAsync(
                    GameLocalSettingName.ProdDownloadPath,
                    downloadBaseFolder
                );
                await this.SetCurrentStateNull(true);
            }
            else
            {
                await this.StartInstallGameResource(_launcher, previous, _patch, option);
            }
            #endregion
            return true;
        }
        catch (TaskCanceledException)
        {
            await SetCurrentStateNull(option.IsProd);
            return false;
        }
        catch (Exception)
        {
            await SetCurrentStateNull(option.IsProd);
            return false;
        }
    }

    public async Task<string?> GetBaseUrl(
        GameLauncherSource _launcher,
        string resourceUrl,
        string preiveResource,
        List<IndexResource> resources,
        InstallOption option,
        bool isResource = false
    )
    {
        try
        {
            GameEventPublisher.Publish(
                new GameContextOutputArgs()
                {
                    Type = GameContextActionType.CdnSelect,
                    TipMessage = "正在选择最优CDN",
                    Prod = option.IsProd,
                }
            );
            if (resources == null || resources.Count == 0)
            {
                return _launcher.ResourceDefault.CdnList.FirstOrDefault()?.Url + resourceUrl;
            }
            var firstResource = resources.FirstOrDefault();
            string baseUrl = "";
            if (firstResource != null && !string.IsNullOrWhiteSpace(firstResource.FromFolder))
            {
                baseUrl = firstResource.FromFolder;
            }
            else
            {
                baseUrl = preiveResource;
            }
            var cdnResult = await TestCdnAsync(
                _launcher.ResourceDefault.CdnList,
                baseUrl,
                resources
            );
            if (cdnResult == null || !cdnResult.Value.Success)
            {
                this.GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = GameContextActionType.TipMessage,
                        TipMessage = "未找到可用的CDN地址，默认使用第一个CDN",
                        Prod = option.IsProd,
                    }
                );
                return _launcher.ResourceDefault.CdnList.FirstOrDefault()?.Url + resourceUrl;
            }
            var valueUrl = cdnResult!.Value.Url + baseUrl;
            return valueUrl;
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
        InstallOption option
    )
    {
        var gen = Interlocked.Increment(ref _operationGeneration);
        GameContextOutputArgs.CurrentGeneration.Value = gen;
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
        this._downloadCts = new();
        var state = await GetInitDownloadState(false); //安装更新不要用预下载状态
        this._installGameResourceCts = new CancellationTokenSource();
        state.CancelToken = _installGameResourceCts;
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
        string downloadBaseFolder = this.BuildInstallOptionFolder(option, baseFolder);

        GameEventPublisher.Publish(
            new GameContextOutputArgs()
            {
                Type = GameContextActionType.CdnSelect,
                TipMessage = "正在选择最优CDN",
                Prod = false,
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
        IndexGameResource? resource = new();
        string checkBaseUrl = "";
        if (option.IsAdvance)
        {
            var cdnUrl =
                launcher
                    .ResourceDefault.CdnList.Where(x => x.P != 0)
                    .OrderBy(x => x.P)
                    .FirstOrDefault()
                ?? null;
            if (cdnUrl == null)
            {
                Logger.WriteError("CDN地址配置错误，无法更新游戏");
                SystemEventPublisher.Publish(new() { Message = "CDN地址配置错误，无法更新游戏" });
                return;
            }
            var resourceIndexUrl =
               launcher.ResourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).First().Url
               + launcher.Predownload.Config.IndexFile;
            checkBaseUrl = launcher.Predownload.Config.BaseUrl;
            resource = await this.GetGameResourceAsync(resourceIndexUrl);
        }
        else
        {
            var resourceIndexUrl =
                launcher.ResourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).First().Url
                + launcher.ResourceDefault.Config.IndexFile;
            checkBaseUrl = launcher.ResourceDefault.ResourcesBasePath;
            resource = await this.GetGameResourceAsync(resourceIndexUrl);
        }
        if (resource != null)
        {
            installTasks.Add(
                (
                    resource!.Resource,
                    "校验全部文件",
                    baseFolder,
                    InstallGameResourceType.CheckAllFiles,
                    checkBaseUrl
                )
            );
        }
        else
        {
            Logger.WriteError("获取资源信息失败，最终校验启动失败，跳过此校验");
            SystemEventPublisher.Publish(
                new() { Message = "获取资源信息失败，最终校验启动失败，跳过此校验" }
            );
        }
        bool? runValue = true;
        for (int i = 0; i < installTasks.Count; i++)
        {
            if (state.CancelToken.IsCancellationRequested)
            {
                this.GameEventPublisher.Publish(new() { Type = GameContextActionType.None });
                state.IsActive = false;
                state.IsStop = true;
            }
            CurrentSetups = i;
            await this.GameEventPublisher.PublishStepAsync(
                installTasks[i].Name,
                CurrentSetups,
                Setups,
                isProd: false
            );
            await this.GameEventPublisher.PublishStepAsync(
                installTasks[i].Name,
                CurrentSetups,
                Setups,
                isProd: false
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
                runValue = (bool?)await installMethod.ExecuteAsync(true);
                if (runValue is bool boolValue && boolValue == false)
                {
                    Logger.WriteError("安装补丁文件失败");
                    SystemEventPublisher.Publish(new() { Message = "安装补丁文件失败" });
                    await SetCurrentStateNull(false);
                    GameEventPublisher.Publish(
                        new() { Type = GameContextActionType.None, Prod = false }
                    );
                    return;
                }
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
                runValue = (bool?)await installgroupMethod.ExecuteAsync(true);
                //无论执行结果，直接删除临时解压目录
                Directory.Delete(decompressTempFolder, true);
                if (runValue is bool boolValue && boolValue == false)
                {
                    Logger.WriteError("安装补丁组文件失败");
                    SystemEventPublisher.Publish(new() { Message = "安装补丁组文件失败" });
                    await SetCurrentStateNull(false);
                    Directory.Delete(downloadBaseFolder);
                    GameEventPublisher.Publish(
                        new() { Type = GameContextActionType.None, Prod = false }
                    );
                    return;
                }
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
                    option.IsProd
                );
                installZipMethod.SetParam(
                    new Dictionary<string, object>()
                    {
                        { "zipInfos", installTasks[i].Items.ToList() },
                        { "zipDownFolder", installTasks[i].Folder },
                        { "baseGamePath", baseFolder },
                        { "downloadState", state },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = installZipMethod;
                runValue = (bool?)await installZipMethod.ExecuteAsync(true);
                if (runValue is bool boolValue && boolValue == false)
                {
                    Logger.WriteError("安装解压包失败");
                    SystemEventPublisher.Publish(new() { Message = "安装解压包失败" });
                    await SetCurrentStateNull(false);
                    GameEventPublisher.Publish(
                        new() { Type = GameContextActionType.None, Prod = false }
                    );
                    return;
                }
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
                var checkAllResource = installTasks[i].Items;

                var downloadMethod = new DownloadAndVerifyResource(this.Logger)
                {
                    ProgressName = "重新校验文件",
                };
                GameEventPublisher.Publish(
                    new GameContextOutputArgs()
                    {
                        Type = GameContextActionType.CdnSelect,
                        TipMessage = "正在选择最优CDN",
                        Prod = option.IsProd,
                    }
                );
                CdnTestResult? cdnResult = null;
                cdnResult = await TestCdnAsync(
                       launcher.ResourceDefault.CdnList,
                       installTasks[i].baseUrl,
                       checkAllResource.ToList()
                   );
                if (cdnResult == null)
                {
                    Logger.WriteError("获取资源信息失败，最终校验启动失败，跳过此校验");
                    SystemEventPublisher.Publish(
                        new() { Message = "获取资源信息失败，最终校验启动失败，跳过此校验" }
                    );
                    this.GameEventPublisher.Publish(
                        new GameContextOutputArgs() { Type = GameContextActionType.None }
                    );
                    return;
                }
                string baseUrl = Path.Combine(cdnResult.Value.Url,installTasks[i].baseUrl);
                
                downloadMethod.SetParam(
                    new Dictionary<string, object>()
                    {
                        { "resource", installTasks[i].Items.ToList() },
                        { "launcher", launcher },
                        { "isDelete", false },
                        { "folder", installTasks[i].Folder },
                        { "httpClient", HttpClientService! },
                        { "downloadState", state },
                        { "baseUrl", baseUrl },
                        { "isProd", option.IsProd },
                    },
                    this.GameEventPublisher
                );
                this._currentRunningAction = downloadMethod;
                await downloadMethod.ExecuteAsync(true);
            }
        }
        for (int i = 0; i < patch.DeleteFiles.Count; i++)
        {
            var localFile = $"{baseFolder}\\{patch.DeleteFiles[i]}".Replace('/', '\\');
            if (File.Exists(localFile))
            {
                File.Delete(localFile);
            }
            Logger.WriteInfo($"删除旧文件{System.IO.Path.GetFileName(localFile)}");
        }
        var writeConfig = new WriteGameResourceConfig(
            this.GameLocalConfig,
            launcher,
            this.Config,
            Logger
        );
        await writeConfig.WriteDownloadAndUpDateResultAsync(launcher, option);
        await Task.Delay(100);
        if (option.IsProd)
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
        if (!string.IsNullOrWhiteSpace(downloadBaseFolder))
            Directory.Delete(downloadBaseFolder, true);
        await state.CancelToken.CancelAsync();
        state.IsActive = false;
        await SetCurrentStateNull(false);
        Logger.WriteInfo($"安装完成");
        #endregion
    }

    /// <summary>
    /// 提前安装游戏资源
    /// </summary>
    /// <returns></returns>
    public async Task AdvanceInstallGameResourceAsync()
    {
        var launcher = await this.GetGameLauncherSourceAsync();
        var currentVersion = await this.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        if (launcher == null)
        {
            SystemEventPublisher.Publish(new() { Message = "未拉取到游戏数据，请检查网络" });
            return;
        }
        if (currentVersion != launcher.ResourceDefault.Version)
        {
            SystemEventPublisher.Publish(
                new() { Message = "本地版本与服务器版本不匹配，无法安装预下载" }
            );
            return;
        }
        var previous = launcher.Predownload.Config.PatchConfig.FirstOrDefault(x =>
            x.Version == currentVersion
        );
        #region 预下载情况校验

        #endregion
        #region 资源校验
        if (previous == null)
        {
            SystemEventPublisher.Publish(new() { Message = "未从预下载的数据中拉取到正确版本" });
            return;
        }
        var cdnUrl =
            launcher.ResourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).FirstOrDefault()
            ?? null;
        if (cdnUrl == null)
        {
            Logger.WriteError("CDN地址配置错误，无法更新游戏");
            SystemEventPublisher.Publish(new() { Message = "CDN地址配置错误，无法更新游戏" });
            return;
        }
        var _patch = await GetPatchGameResourceAsync(cdnUrl.Url + previous.IndexFile);
        if (_patch == null)
        {
            SystemEventPublisher.Publish(new() { Message = "预下载资源文件拉去错误" });
            return;
        }
        #endregion
        await StartDownloadUpdateGameResourceAsync(
            launcher,
            currentVersion,
            previous,
            _patch,
            InstallOption.CreateAdvance()
        );
        SystemEventPublisher.Publish(new() { Message = "预下载文件校验完毕，开始直接安装游戏" });
    }

    /// <summary>
    /// 退出下载任务
    /// </summary>
    /// <param name="isProd"></param>
    /// <returns></returns>
    private async Task SetCurrentStateNull(bool? isProd)
    {
        if (isProd == null)
        {
            this.ProdDownloadState = null;
            this.DownloadState = null;
        }
        else if (isProd.Value)
        {
            this.ProdDownloadState = null;
        }
        else
        {
            this.DownloadState = null;
        }
        foreach (var item in this.ProgressState.ActiveFiles)
        {
            ProgressState.ActiveFiles.TryRemove(item);
        }
        await Task.Delay(100);
        this.GameEventPublisher.Publish(new() { Type = GameContextActionType.None });
    }

    public async Task StartInstallGameResource(InstallOption option)
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
        if (cdnUrl == null)
        {
            Logger.WriteError("CDN地址配置错误，无法更新游戏");
            SystemEventPublisher.Publish(new() { Message = "CDN地址配置错误，无法更新游戏" });
            return;
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
            return;
        }
        await StartInstallGameResource(launcher, previous, _patch, option);
    }

    #endregion
}
