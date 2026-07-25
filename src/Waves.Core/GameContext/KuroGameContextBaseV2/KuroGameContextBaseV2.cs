namespace Waves.Core.GameContext;

/// <summary>
/// 库洛游戏核心上下文基类V2，重构版本，增强结构性
/// </summary>
public abstract partial class KuroGameContextBaseV2 : IGameContextV2
{
    private bool isLimtSpeed;

    /// <summary>
    /// 阻塞用户启动的下载发布器
    /// </summary>
    public IGameEventPublisher<GameContextOutputArgs> GameEventPublisher { get; internal set; }

    /// <summary>
    /// 系统消息发布器
    /// </summary>
    public SystemEventPublisher SystemEventPublisher { get; internal set; }

    /// <summary>
    /// Http 请求服务，包含下载Client与配置Client
    /// </summary>
    public IHttpClientService HttpClientService { get; set; }

    /// <summary>
    /// CDN测试工具
    /// </summary>
    public CDNSpeedTester CDNSpeedTester { get; private set; }

    /// <summary>
    /// 日志组件
    /// </summary>
    public LoggerService Logger { get; set; }

    /// <summary>
    /// API配置项，包含游戏相关的API地址与参数等
    /// </summary>
    public KuroGameApiConfig Config { get; private set; }

    public string ContextName { get; }

    /// <summary>
    /// 核心配置文件读取
    /// </summary>
    public GameLocalConfig GameLocalConfig { get; private set; }
    public object SpeedValue { get; private set; }

    /// <summary>
    /// 是否限速
    /// </summary>
    public bool IsLimitSpeed
    {
        get => isLimtSpeed;
        set { this.isLimtSpeed = value; }
    }

    /// <summary>
    /// 游戏配置文件夹
    /// </summary>
    public string GamerConfigPath { get; set; }

    /// <summary>
    /// 核心进度状态跟踪器，聚合内部事件，供UI初始读取和长效绑定的最新状态
    /// </summary>
    public GameProgressTracker ProgressState { get; } = new();

    public abstract string GameContextNameKey { get; }

    public abstract GameType GameType { get; }

    public abstract Type ContextType { get; }

    #region 下载字段
    /// <summary>
    /// 阻塞用户启动的下载状态管理
    /// </summary>
    public DownloadState? DownloadState { get; private set; }

    public DownloadState? ProdDownloadState { get; private set; }

    public string DisplayName { get; }

    /// <summary>
    /// CDN测速工具
    /// </summary>
    private CDNSpeedTester _cdnSpeedTester = new();
    #endregion

    private IAsyncDisposable? _currentRunningAction;
    private long _operationGeneration;

    public KuroGameContextBaseV2(KuroGameApiConfig config, string contextName, string display)
    {
        Logger = new LoggerService();
        Config = config;
        ContextName = contextName;
        this.DisplayName = display;
    }

    /// <summary>
    /// 初始化配置核心
    /// </summary>
    /// <returns></returns>
    public virtual async Task InitAsync()
    {
        this.HttpClientService.BuildClient();
        Directory.CreateDirectory(GamerConfigPath);
        this.GameLocalConfig = new GameLocalConfig(GamerConfigPath + "\\Settings.bat");
        var logPath = GamerConfigPath + "\\logs\\log.log";
        Logger.InitLogger(logPath, Serilog.RollingInterval.Day);
        CDNSpeedTester = new CDNSpeedTester();
        if (this.GameEventPublisher != null)
        {
            await ProgressState.StartTrackingAsync(this.GameEventPublisher);
        }

        if (this.GameEventPublisher != null && this.SystemEventPublisher != null)
        {
            await this.GameEventPublisher.SubscribeAsync(async args =>
            {
                if (args == null)
                    return;
                if (!string.IsNullOrWhiteSpace(args.TipMessage))
                {
                    SystemEventPublisher.Publish(
                        new SystemMessagerModel { Time = DateTime.Now, Message = args.TipMessage }
                    );
                }
            });
        }

        await InitSettingAsync();
    }

    /// <summary>
    /// 初始化配置项目
    /// </summary>
    /// <returns></returns>
    private async Task InitSettingAsync()
    {
        var config = await this.ReadContextConfigAsync();
        if (config.LimitSpeed > 0)
        {
            this.SpeedValue = config.LimitSpeed;
            this.IsLimitSpeed = true;
        }
    }

    /// <summary>
    /// 读取核心配置项目，包含一些持久化参数
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<GameContextConfig> ReadContextConfigAsync(CancellationToken token = default)
    {
        GameContextConfig config = new();
        var speed = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.LimitSpeed);
        var dx11 = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.IsDx11);
        if (int.TryParse(speed, out var rate))
        {
            config.LimitSpeed = rate;
        }
        else
            config.LimitSpeed = 0;
        if (string.IsNullOrWhiteSpace(dx11))
            config.IsDx11 = false;
        if (bool.TryParse(dx11, out var isDx11))
        {
            config.IsDx11 = isDx11;
        }
        else
            config.IsDx11 = false;
        return config;
    }

    public async Task<bool> StopCannelTaskAsync()
    {
        try
        {
            if (_currentRunningAction != null)
            {
                if (_currentRunningAction is IProgressSetup cancelTask)
                {
                    if (cancelTask.CanStop)
                    {
                        if (this.DownloadState != null)
                        {
                            DownloadState.IsStop = true;
                            DownloadState.IsActive = false;
                        }
                        await _currentRunningAction.DisposeAsync();
                        if (this.DownloadState != null)
                            await this.DownloadState.CancelToken.CancelAsync();
                    }
                    else
                    {
                        this.GameEventPublisher.Publish(
                            new GameContextOutputArgs()
                            {
                                Type = GameContextActionType.TipMessage,
                                TipMessage = "当前任务不支持取消",
                            }
                        );
                        return true;
                    }
                }
            }
            if (DownloadState != null)
            {
                DownloadState.IsStop = true;
                DownloadState.IsActive = false;
            }
            var cancelGen = Interlocked.Increment(ref _operationGeneration);
            GameContextOutputArgs.CurrentGeneration.Value = cancelGen;
            this.GameEventPublisher.Publish(
                new GameContextOutputArgs() { Type = GameContextActionType.None }
            );
            return true;
        }
        catch (Exception ex)
        {
            var message = $"{this.ContextName}取消任务失败:{ex.Message}";
            SystemEventPublisher.Publish(new SystemMessagerModel() { Message = message });
            Logger.WriteError(message);
            return false;
        }
    }

    public async Task<bool> PauseDownloadAsync()
    {
        if (DownloadState != null)
        {
            if (DownloadState.IsActive && _currentRunningAction != null)
            {
                if (_currentRunningAction is IProgressSetup cancelTask)
                {
                    if (cancelTask.CanPause)
                    {
                        await DownloadState.PauseAsync();
                    }
                    else
                    {
                        this.GameEventPublisher.Publish(
                            new GameContextOutputArgs()
                            {
                                Type = GameContextActionType.TipMessage,
                                TipMessage = "当前任务不支持",
                            }
                        );
                    }
                }
            }
            else
            {
                await DownloadState.PauseAsync();
            }
        }
        else if (ProdDownloadState != null)
        {
            if (ProdDownloadState.IsActive && _currentRunningAction != null)
            {
                if (_currentRunningAction is IProgressSetup cancelTask)
                {
                    if (cancelTask.CanPause)
                    {
                        await ProdDownloadState.PauseAsync();
                    }
                    else
                    {
                        this.GameEventPublisher.Publish(
                            new GameContextOutputArgs()
                            {
                                Type = GameContextActionType.TipMessage,
                                TipMessage = "当前任务不支持",
                            }
                        );
                    }
                }
            }
            else
            {
                await ProdDownloadState.PauseAsync();
            }
        }
        return true;
    }

    public async Task<bool> ResumeDownloadAsync()
    {
        if (DownloadState != null)
        {
            if (DownloadState.IsPaused)
            {
                await DownloadState.ResumeAsync();
            }
        }
        else if (ProdDownloadState != null)
        {
            if (ProdDownloadState.IsPaused)
            {
                await ProdDownloadState.ResumeAsync();
            }
        }

        return true;
    }

    public async Task SetDownloadSpeedAsync(long mbValue)
    {
        await this.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LimitSpeed,
            mbValue.ToString()
        );
        if (DownloadState == null)
            return;
        await DownloadState.SetSpeedLimitAsync(mbValue * 1024 * 1024);
    }

    public async Task<FileVersion> GetLocalFileVersionAsync(string fileName, string displayName)
    {
        var gameFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        var file = Directory
            .GetFiles(gameFolder, fileName, SearchOption.AllDirectories)
            .FirstOrDefault();
        if (file == null)
        {
            return new FileVersion() { DisplayName = displayName, Version = "未找到文件" };
        }
        FileVersionInfo fileinfo = FileVersionInfo.GetVersionInfo(file);
        return new FileVersion()
        {
            DisplayName = displayName,
            Subtitle = fileinfo.InternalName,
            FilePath = file,
            Version =
                $"{fileinfo.FileMajorPart}.{fileinfo.FileMinorPart}.{fileinfo.FileBuildPart}.{fileinfo.FilePrivatePart}",
        };
    }

    public async Task<FileVersion> GetLocalDLSSAsync()
    {
        return await GetLocalFileVersionAsync("nvngx_dlss.dll", "Xess");
    }

    public async Task<FileVersion> GetLocalDLSSGenerateAsync()
    {
        return await GetLocalFileVersionAsync("nvngx_dlssg.dll", "Dlss 帧生成");
    }

    public async Task<FileVersion> GetLocalXeSSGenerateAsync()
    {
        return await GetLocalFileVersionAsync("libxess.dll", "Xess");
    }

    public async Task<GameContextStatus> GetGameContextStatusAsync(
        CancellationToken token = default
    )
    {
        GameContextStatus status = new GameContextStatus();
        var localVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        var gameBaseFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        var gameProgramFile = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassProgram
        );
        var updateing = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameUpdateing
        );
        bool.TryParse(
            await GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdIsAdvance),
            out var ProdIsAdvance
        );
        if (Directory.Exists(gameBaseFolder))
        {
            status.IsGameExists = true;
        }
        if (File.Exists(gameProgramFile))
        {
            status.IsGameInstalled = true;
        }
        if (!string.IsNullOrWhiteSpace(localVersion))
        {
            status.IsLauncher = true;
        }
        var ping = (await NetworkCheck.PingAsync(KuroGameApiConfig.BaseAddress[0]));
        if (!(ping != null && ping.Status == IPStatus.Success))
        {
            SystemEventPublisher.Publish(new() { Message = "网络未连接" });
            return status;
        }
        var indexSource = await this.GetGameLauncherSourceAsync();
        if (indexSource != null && !string.IsNullOrWhiteSpace(localVersion))
        {
            await ClearVersion(indexSource);
            var localV = Version.Parse(localVersion);
            var serverVFlage = Version.TryParse(
                indexSource.ResourceDefault.Version,
                out var serverV
            );
            var predownloadVFlage = Version.TryParse(
                indexSource.Predownload != null ? indexSource.Predownload.Version : "0.0.1",
                out var predownVersion
            );
            if (predownloadVFlage && predownVersion!.ToString() != "0.0.1" && ProdIsAdvance)
            {
                status.DisplayVersion = predownVersion.ToString();
            }
            else if (localV < serverV)
            {
                status.IsUpdate = true;
                status.DisplayVersion = indexSource.ResourceDefault.Version;
            }
            else
            {
                status.DisplayVersion = localVersion;
            }

            if (
                !string.IsNullOrWhiteSpace(updateing)
                && bool.TryParse(updateing, out var updateResult)
            )
            {
                status.IsUpdateing = updateResult;
            }
            if (
                (
                    indexSource.Predownload != null
                    && status.IsGameExists == true
                    && status.IsGameInstalled == true
                )
            )
            {
                status.IsProdownPause =
                    ProdDownloadState != null ? ProdDownloadState.IsPaused : false;
                status.IsPredownloaded = true;

                var donwResult = await GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.ProdDownloadFolderDone
                );
                var prePath = await GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.ProdDownloadPath
                );
                var prodDownVersion = await GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.ProdDownloadVersion
                );
                if (bool.TryParse(donwResult, out var predDown) && Directory.Exists(prePath))
                {
                    status.PredownloadedDone = predDown;
                }
                else
                {
                    status.PredownloadedDone = false;
                }
                status.PredownloaAcion =
                    ProdDownloadState != null ? ProdDownloadState.IsActive : false;
                status.ProdIsAdvance = ProdIsAdvance;
            }
        }
        if (DownloadState != null)
        {
            status.IsPause = this.DownloadState.IsPaused;
            status.IsAction = this.DownloadState.IsActive;
        }
        status.Gameing = this._isStarting;
        return status;
    }

    private async Task ClearVersion(GameLauncherSource indexSource)
    {
        var currentVersion = await this.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        if (currentVersion == indexSource.ResourceDefault.Version)
        {
            await this.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.ProdIsAdvance, "False");
        }
    }

    public async Task ReEmitLastOutputAsync(bool isPred = false)
    {
        this.GameEventPublisher.Publish(this.ProgressState.LastArgs);
    }

    public async Task DeleteResourceAsync(
        IProgress<(double deletedCount, double totalCount)> progress
    )
    {
        if (progress == null)
            throw new ArgumentNullException(nameof(progress));
        var rootFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
        {
            await ClearLocalConfigAsync();
            progress.Report((1, 1));
            return;
        }
        try
        {
            var allFiles = Directory
                .EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                .ToList();
            long totalFileCount = allFiles.Count;
            long deletedFileCount = 0;

            if (totalFileCount == 0)
            {
                await ClearLocalConfigAsync();
                progress.Report((1, 1));
                return;
            }

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 8 };

            const int progressReportInterval = 10;

            await Parallel.ForEachAsync(
                allFiles,
                parallelOptions,
                async (filePath, token) =>
                {
                    try
                    {
                        File.Delete(filePath);
                        long current = Interlocked.Increment(ref deletedFileCount);
                        if (current % progressReportInterval == 0 || current == totalFileCount)
                        {
                            progress.Report((current, totalFileCount));
                        }
                    }
                    catch (Exception ex)
                    {
                        var message = $"删除文件失败：{filePath}，错误：{ex.Message}";
                        SystemEventPublisher.Publish(
                            new()
                            {
                                Message = message,
                                Delay = TimeSpan.FromMinutes(1).TotalSeconds,
                            }
                        );
                    }
                }
            );

            DeleteEmptyDirectories(rootFolder);

            await ClearLocalConfigAsync();

            progress.Report((totalFileCount, totalFileCount));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SystemEventPublisher.Publish(new() { Message = $"批量删除资源失败：{ex.Message}" });
        }
    }

    #region 辅助方法
    /// <summary>
    /// 递归删除空目录
    /// </summary>
    private void DeleteEmptyDirectories(string directoryPath)
    {
        try
        {
            foreach (var subDir in Directory.EnumerateDirectories(directoryPath))
            {
                DeleteEmptyDirectories(subDir);
            }

            // 删除空文件夹
            if (
                Directory.GetFiles(directoryPath).Length == 0
                && Directory.GetDirectories(directoryPath).Length == 0
            )
            {
                Directory.Delete(directoryPath);
            }
        }
        catch (Exception ex)
        {
            SystemEventPublisher.Publish(
                new() { Message = $"删除空目录失败：{directoryPath}，错误：{ex.Message}" }
            );
        }
    }

    private async Task ClearLocalConfigAsync()
    {
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, "");
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.GameLauncherBassProgram, "");
        await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameVersion, "");
    }
    #endregion


    /// <summary>
    /// 获取账号详情,自动重试5次
    /// </summary>
    /// <param name="oAutoCode">解密后数据</param>
    /// <param name="token">释放令牌</param>
    /// <returns></returns>
    public async Task<QueryPlayerInfo?> QueryPlayerInfoAsync(
        string oAutoCode,
        CancellationToken token = default
    )
    {
        using (HttpClient client = new HttpClient())
        {
            int count = 0;
            QueryPlayerInfo? info = null;
            while (true)
            {
                HttpRequestMessage msg = new HttpRequestMessage();
                var url = PlayerInfoUser();
                if (url == null)
                    return null;
                msg.RequestUri = new Uri(url);
                msg.Method = HttpMethod.Post;
                WavesQueryLocalPlayerInfoRequest request = new WavesQueryLocalPlayerInfoRequest();
                request.OAutoCode = oAutoCode;
                var json = JsonSerializer.Serialize(
                    request,
                    LocalGameUserContext.Default.WavesQueryLocalPlayerInfoRequest
                );
                msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var reponse = await client.SendAsync(msg, token);
                var resultJson = await reponse.Content.ReadAsStringAsync(token);
                var models = JsonSerializer.Deserialize<QueryPlayerInfo>(
                    resultJson,
                    LocalGameUserContext.Default.QueryPlayerInfo
                );
                if (count > 5)
                {
                    info = models;
                    break;
                }
                if (models == null || models.Code != 0)
                {
                    count++;
                    continue;
                }
                info = models;
                break;
            }
            if (info == null)
                return null;
            info.Items = new();
            if (info.Code != 0)
            {
                return info;
            }
            foreach (var item in info.Data)
            {
                if (this.GameType == Models.Enums.GameType.Waves)
                {
                    WavesQueryPlayerItem? model = JsonSerializer.Deserialize<WavesQueryPlayerItem>(
                        item.Value,
                        LocalGameUserContext.Default.WavesQueryPlayerItem
                    );
                    if (model == null)
                        continue;
                    model.ServerName = item.Key;
                    info.Items.Add(model);
                }
                else if (this.GameType == Models.Enums.GameType.Punish)
                {
                    PunishQueryPlayerItem? model =
                        JsonSerializer.Deserialize<PunishQueryPlayerItem>(
                            item.Value,
                            LocalGameUserContext.Default.PunishQueryPlayerItem
                        );
                    if (model == null)
                        continue;
                    model.ServerName = item.Key;
                    info.Items.Add(model);
                }
            }
            return info;
        }
    }

    public async Task<QueryRoleInfo?> QueryRoleInfoAsync(
        string oautoCode,
        string playerId,
        string region,
        CancellationToken token = default
    )
    {
        int count = 0;
        QueryRoleInfo? info = null;
        using (HttpClient client = new HttpClient())
        {
            while (true)
            {
                if (count > 5)
                    break;
                HttpRequestMessage msg = new HttpRequestMessage();

                var url = RoleInfoUser();
                if (url == null)
                    return null;
                msg.RequestUri = new Uri(url);

                msg.Method = HttpMethod.Post;
                QueryLocalRoleInfoRequest request = new QueryLocalRoleInfoRequest();
                request.OAutoCode = oautoCode;
                request.PlayerId = playerId;
                request.Region = region;
                var serializeOptions = new JsonSerializerOptions(
                    LocalGameUserContext.Default.Options
                )
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                };
                var json = JsonSerializer.Serialize(request, serializeOptions);
                msg.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var reponse = await client.SendAsync(msg, token);
                var json2 = await reponse.Content.ReadAsStringAsync(token);
                var model = JsonSerializer.Deserialize<QueryRoleInfo>(
                    await reponse.Content.ReadAsStringAsync(token),
                    LocalGameUserContext.Default.QueryRoleInfo
                );
                if (model == null || model.Code == 1005 || model.Code == 1001)
                {
                    count++;
                    continue;
                }
                info = model;
                break;
            }
            if (info == null)
            {
                return null;
            }
            info.Items = [];
            foreach (var item in info.Data)
            {
                if (this.GameType == Models.Enums.GameType.Waves)
                {
                    WavesLocalGameRoleItem? roleItem =
                        JsonSerializer.Deserialize<WavesLocalGameRoleItem>(
                            item.Value,
                            LocalGameUserContext.Default.WavesLocalGameRoleItem
                        );
                    if (roleItem == null)
                        continue;
                    roleItem.ServerName = item.Key;
                    info.Items.Add(roleItem);
                }
                else if (this.GameType == Models.Enums.GameType.Punish)
                {
                    PunishLocalGameRoleItem? roleItem =
                        JsonSerializer.Deserialize<PunishLocalGameRoleItem>(
                            item.Value,
                            LocalGameUserContext.Default.PunishLocalGameRoleItem
                        );
                    if (roleItem == null)
                        continue;
                    roleItem.ServerName = item.Key;
                    info.Items.Add(roleItem);
                }
            }
        }
        return info;
    }

    public string? PlayerInfoUser()
    {
        if (this.GameType == Models.Enums.GameType.Waves)
        {
            if (this.ContextName == nameof(WavesGlobalGameContextV2))
            {
                return $"https://pc-launcher-sdk-api.kurogame.net/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                return $"https://pc-launcher-sdk-api.kurogame.com/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
        }

        if (this.GameType == Models.Enums.GameType.Punish)
        {
            //https://pc-launcher-sdk-haru-api.kurogames.com/game/queryPlayerInfo?_t=1772959214
            //https://pc-launcher-sdk-haru-api.kurogames.com/game/queryRole?_t=1772959216

            if (
                this.ContextName == nameof(PunishGlobalGameContextV2)
                || this.ContextName == nameof(PunishTwGameContextV2)
            )
            {
                return $"https://pc-launcher-sdk-haru-api.kurogames.net/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                return $"https://pc-launcher-sdk-haru-api.kurogames.com/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
        }

        return null;
    }

    public string? RoleInfoUser()
    {
        if (this.GameType == Models.Enums.GameType.Waves)
        {
            if (this.ContextName == nameof(WavesGlobalGameContextV2))
            {
                return $"https://pc-launcher-sdk-api.kurogame.net/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                return $"https://pc-launcher-sdk-api.kurogame.com/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
        }

        if (this.GameType == Models.Enums.GameType.Punish)
        {
            if (
                this.ContextName == nameof(PunishGlobalGameContextV2)
                || this.ContextName == nameof(PunishTwGameContextV2)
            )
            {
                return $"https://pc-launcher-sdk-haru-api.kurogames.net/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                return $"https://pc-launcher-sdk-haru-api.kurogames.com/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
        }

        return null;
    }

    public virtual async Task<List<KRSDKLauncherCache>?> GetLocalGameOAuthAsync(
        CancellationToken token = default
    )
    {
        try
        {
            if (this.Config.PKGId == null)
            {
                return null;
            }
            var roming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var gameLocal = Path.Combine(roming, $"KR_{this.Config.GameID}");
            var gameLaunche = Path.Combine(
                gameLocal,
                $"{this.Config.PKGId}\\KRSDKUserLauncherCache.json"
            );
            if (Directory.Exists(gameLocal) && File.Exists(gameLaunche))
            {
                var fileStr = await File.ReadAllTextAsync(gameLaunche, token);
                var model = JsonSerializer.Deserialize(
                    fileStr,
                    LauncherConfig.Default.ListKRSDKLauncherCache
                );
                return model;
            }
            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public virtual async Task<KRSDKGameTokenModel?> GetSDKGameTokenAsync(
        CancellationToken token = default
    )
    {
        try
        {
            if (this.Config.PKGId == null)
            {
                return null;
            }
            var roming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var gameLocal = Path.Combine(roming, $"KR_{this.Config.GameID}");
            var gameLaunche = Path.Combine(gameLocal, $"{this.Config.PKGId}\\KRSDKUserCache.json");
            if (Directory.Exists(gameLocal) && File.Exists(gameLaunche))
            {
                var fileStr = await File.ReadAllTextAsync(gameLaunche, token);
                var model = JsonSerializer.Deserialize(
                    fileStr,
                    LauncherConfig.Default.KRSDKGameTokenModel
                );
                return model;
            }
            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public async Task<bool> SetCurrentLoginSdkToken(
        KRSDKGameTokenCache cache,
        CancellationToken token = default
    )
    {
        try
        {
            if (cache == null)
            {
                this.SystemEventPublisher.Publish(
                    new SystemMessagerModel()
                    {
                        Message = "请输入一个正确的账号",
                        Delay = TimeSpan.FromSeconds(10).TotalSeconds,
                    }
                );
                return false;
            }
            var currentTokens = await this.GetSDKGameTokenAsync(token);
            if (currentTokens == null)
            {
                this.SystemEventPublisher.Publish(
                    new SystemMessagerModel()
                    {
                        Message = "当前本地缓存中没有此账号数据",
                        Delay = TimeSpan.FromSeconds(10).TotalSeconds,
                    }
                );
                return false;
            }
            if (currentTokens.AccountList.Any(x => x.Cuid == cache.Cuid))
            {
                currentTokens.LastLoginCuid = cache.Cuid;
            }
            var roming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var gameLocal = Path.Combine(roming, $"KR_{this.Config.GameID}");
            var gameLaunche = Path.Combine(gameLocal, $"{this.Config.PKGId}\\KRSDKUserCache.json");
            var json = JsonSerializer.Serialize(
                currentTokens,
                LauncherConfig.Default.KRSDKGameTokenModel
            );
            await File.WriteAllTextAsync(gameLaunche, json, token);
            return true;
        }
        catch (Exception ex)
        {
            this.SystemEventPublisher.Publish(
                    new SystemMessagerModel()
                    {
                        Message = $"选择账号保存异常",
                        Delay = TimeSpan.FromSeconds(10).TotalSeconds,
                    }
                );
            Logger.WriteError($"选择账号保存异常:{ex.Message},{ex.StackTrace}");
            return false;
        }
    }

    public bool IsDownloadTaskCancel()
    {
        return DownloadState != null
            && (
                DownloadState.CancelToken == null
                || DownloadState.CancelToken.IsCancellationRequested
            );
    }
}
