using Waves.Core.Models.Options;

namespace Waves.Core.GameContext;

partial class KuroGameContextBaseV2
{
    private int _resourceOperationActive;
    internal bool IsResourceOperationActive => Volatile.Read(ref _resourceOperationActive) != 0;

    public Task<bool> UpdateGameResourceAsync(
        string? downloadFolder = null,
        GameResourceParameter? parameter = null
    ) =>
        QueueResourceOperationAsync(
            () => GetUpdateGameResourceAsync(parameter),
            new InstallOption { DownloadFolder = downloadFolder },
            parameter
        );

    public Task<bool> StartProdDownloadGameResourceAsync(
        string? downloadFolder = null,
        GameResourceParameter? parameter = null
    ) =>
        QueueResourceOperationAsync(
            () => GetGameProdownloadResourceAsync(parameter),
            new InstallOption { IsProd = true, DownloadFolder = downloadFolder },
            parameter
        );

    private bool IsExecutable(GameVersionInfo plan) =>
        plan.Availability == GameResourceAvailability.Ready
        && !string.IsNullOrWhiteSpace(plan.NewGameVersion)
        && (
            plan.DefaultResource.Any(x => x.IsSelected)
            || plan.ZipResources.Any(x => x.IsSelected)
            || plan.PatchResources.Any(x => x.IsSelected)
        );

    private async Task<bool> QueueResourceOperationAsync(
        Func<Task<GameVersionInfo>> query,
        InstallOption option,
        GameResourceParameter? parameter
    )
    {
        if (Interlocked.CompareExchange(ref _resourceOperationActive, 1, 0) != 0)
            return false;
        var queued = false;
        GameContextOutputArgs.CurrentGeneration.Value = Interlocked.Increment(
            ref _operationGeneration
        );
        GameEventPublisher.Publish(
            new()
            {
                Type = GameContextActionType.Preparing,
                IsAction = true,
                Prod = option.IsProd,
                TipMessage = "正在获取资源列表…",
            }
        );
        try
        {
            var plan = await query();
            if (!IsExecutable(plan))
            {
                Interlocked.Exchange(ref _resourceOperationActive, 0);
                return false;
            }
            var baseFolder = await GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder
            );
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                Interlocked.Exchange(ref _resourceOperationActive, 0);
                return false;
            }
            await ResolveDownloadFolderAsync(option);
            option.DownloadFolder = option.ResolveDownloadFolder(baseFolder);
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!await ExecuteResourcePlanAsync(plan, option, parameter))
                    {
                        Logger.WriteError("资源任务未完成，未确认安装成功；保留当前版本记录。");
                        SystemEventPublisher.Publish(
                            new() { Message = "资源任务未完成，请查看日志后重试。" }
                        );
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.WriteError($"资源任务失败：{ex}");
                    SystemEventPublisher.Publish(new() { Message = $"资源任务失败：{ex.Message}" });
                }
                finally
                {
                    await SetCurrentStateNull(option.IsProd);
                    Interlocked.Exchange(ref _resourceOperationActive, 0);
                }
            });
            queued = true;
            return true;
        }
        catch
        {
            Interlocked.Exchange(ref _resourceOperationActive, 0);
            throw;
        }
        finally
        {
            if (!queued)
                GameEventPublisher.Publish(
                    new() { Type = GameContextActionType.None, Prod = option.IsProd }
                );
        }
    }

    private async Task<DownloadState> CreateResourceStateAsync(
        bool isProd,
        CancellationToken token = default
    )
    {
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = await GetInitDownloadState(isProd);
        if (isProd)
        {
            _prodDownloadCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            state.CancelToken = _prodDownloadCts;
        }
        else
            state.CancelToken = _downloadCts;
        state.IsActive = true;
        return state;
    }

    private async Task<bool> ExecuteResourcePlanAsync(
        GameVersionInfo plan,
        InstallOption option,
        GameResourceParameter? parameter,
        CancellationToken token = default
    )
    {
        if (!IsExecutable(plan))
            return false;
        var baseFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder,
            token
        );
        if (string.IsNullOrWhiteSpace(baseFolder))
            return false;
        await ResolveDownloadFolderAsync(option);
        var cache = option.ResolveDownloadFolder(baseFolder);
        option.DownloadFolder = cache;
        var state = await CreateResourceStateAsync(option.IsProd, token);
        HttpClientService.BuildClient();
        if (option.IsProd)
            await GameLocalConfig.SaveConfigsAsync(
                new Dictionary<string, string>
                {
                    [GameLocalSettingName.ProdDownloadPath] = cache,
                    [GameLocalSettingName.ProdDownloadVersion] = plan.NewGameVersion,
                    [GameLocalSettingName.ProdDownloadFolderDone] = "False",
                }
            );
        else
            await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");

        var tasks = new List<(IEnumerable<GameFileInfo> Files, string Name, string Folder)>();
        var patches = plan.PatchResources.Where(x => x.IsSelected && !x.IsGroup).ToList();
        var groups = plan.PatchResources.Where(x => x.IsSelected && x.IsGroup).ToList();
        if (patches.Count != 0)
            tasks.Add((patches, "下载补丁文件", Path.Combine(cache, "patchs")));
        if (groups.Count != 0)
            tasks.Add((groups, "下载补丁组文件", Path.Combine(cache, "patchGroup")));
        if (plan.ZipResources.Any(x => x.IsSelected))
            tasks.Add(
                (
                    plan.ZipResources.Where(x => x.IsSelected),
                    "下载压缩包更新文件",
                    Path.Combine(cache, "zips")
                )
            );
        if (plan.DefaultResource.Any(x => x.IsSelected))
            tasks.Add(
                (
                    plan.DefaultResource.Where(x => x.IsSelected),
                    "下载更新文件",
                    Path.Combine(cache, "resources")
                )
            );
        Setups = tasks.Select(x => x.Name).ToList();
        for (var i = 0; i < tasks.Count; i++)
        {
            state.CancelToken.Token.ThrowIfCancellationRequested();
            CurrentSetups = i;
            await GameEventPublisher.PublishStepAsync(
                tasks[i].Name,
                i,
                Setups,
                isProd: option.IsProd
            );
            if (
                !await DownloadFilesAsync(
                    tasks[i].Files,
                    tasks[i].Folder,
                    state,
                    option.IsProd,
                    tasks[i].Name,
                    cdns: plan.CdnCandidates
                )
            )
                return false;
        }
        state.CancelToken.Token.ThrowIfCancellationRequested();
        if (option.IsProd)
        {
            await GameLocalConfig.SaveConfigsAsync(
                new Dictionary<string, string>
                {
                    [GameLocalSettingName.ProdDownloadFolderDone] = "True",
                    [GameLocalSettingName.ProdDownloadVersion] = plan.NewGameVersion,
                    [GameLocalSettingName.ProdDownloadPath] = cache,
                },
                state.CancelToken.Token
            );
            return true;
        }
        return await InstallResourcePlanAsync(plan, option, parameter, state);
    }

    private async Task<bool> DownloadFilesAsync(
        IEnumerable<GameFileInfo> files,
        string folder,
        DownloadState state,
        bool isProd,
        string name,
        bool deleteExtra = false,
        List<string>? skip = null,
        IEnumerable<GameResourceCdn>? cdns = null
    )
    {
        _currentRunningAction = null;
        var list = files.ToList();
        if (list.Count == 0)
            return false;
        // 对真实的每文件候选 URL 测速，保留不同 FromFolder 和失败后的 CDN 重试。
        var sample = list.MinBy(x => Math.Abs(x.Size - 50L * 1024 * 1024))!;
        GameEventPublisher.Publish(
            new()
            {
                Type = GameContextActionType.CdnSelect,
                Prod = isProd,
                TipMessage = "正在选择最优CDN",
            }
        );
        var preferred = await CDNSpeedTester.SelectResourceUrlAsync(
            sample.UrlCandidates.Count == 0 ? [sample.Url] : sample.UrlCandidates,
            TimeSpan.FromSeconds(40),
            state.CancelToken.Token,
            cdns
        );
        if (preferred is not null)
        {
            var authority = new Uri(preferred).GetLeftPart(UriPartial.Authority);
            foreach (var file in list)
                file.Url =
                    file.UrlCandidates.FirstOrDefault(x =>
                        new Uri(x).GetLeftPart(UriPartial.Authority) == authority
                    ) ?? file.Url;
        }
        var fast = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.FastVerify,
            state.CancelToken.Token
        );
        var action = new DownloadAndVerifyResource(Logger) { ProgressName = name };
        action.SetParam(
            new Dictionary<string, object>
            {
                ["resource"] = list,
                ["isDelete"] = deleteExtra,
                ["folder"] = folder,
                ["httpClient"] = HttpClientService,
                ["downloadState"] = state,
                ["isProd"] = isProd,
                ["fastVerify"] = bool.TryParse(fast, out var enabled) && enabled,
                ["skipVerifyFile"] = skip ?? [],
            },
            GameEventPublisher
        );
        _currentRunningAction = action;
        return await action.ExecuteAsync(true) is true
            && !state.CancelToken.IsCancellationRequested;
    }

    public async Task StartInstallGameResource(
        GameVersionInfo plan,
        InstallOption option,
        GameResourceParameter? parameter = null
    )
    {
        if (Interlocked.CompareExchange(ref _resourceOperationActive, 1, 0) != 0)
            return;
        try
        {
            if (!IsExecutable(plan))
                return;
            GameContextOutputArgs.CurrentGeneration.Value = Interlocked.Increment(
                ref _operationGeneration
            );
            await ResolveDownloadFolderAsync(option);
            var state = await CreateResourceStateAsync(false);
            _installGameResourceCts = state.CancelToken;
            if (!await InstallResourcePlanAsync(plan, option, parameter, state))
            {
                state.CancelToken.Token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("安装或校验未完成，未提交目标版本配置。");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.WriteError($"安装失败：{ex}");
            SystemEventPublisher.Publish(new() { Message = $"安装失败：{ex.Message}" });
        }
        finally
        {
            await SetCurrentStateNull(false);
            Interlocked.Exchange(ref _resourceOperationActive, 0);
        }
    }

    private async Task<bool> InstallResourcePlanAsync(
        GameVersionInfo plan,
        InstallOption option,
        GameResourceParameter? parameter,
        DownloadState state
    )
    {
        if (!IsExecutable(plan))
            return false;
        var baseFolder = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder,
            state.CancelToken.Token
        );
        if (string.IsNullOrWhiteSpace(baseFolder))
            return false;
        var current =
            await GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.LocalGameVersion,
                state.CancelToken.Token
            ) ?? "";
        if (plan.ResourceVersion == "2")
        {
            // 分包计划的主包版本不一定等于汇总主版本；逐包核对计划生成时的本地快照。
            var recorded = new Dictionary<string, string?>();
            foreach (
                var name in plan
                    .ResourcePacks.Select(x => x.Name)
                    .Concat(["Common", "HD", "SD", "UHD"])
                    .Distinct(StringComparer.Ordinal)
            )
                recorded[name] = await GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.GetBunlePackVersionKey(name),
                    state.CancelToken.Token
                );
            var hasPackRecords = recorded.Values.Any(x => x is not null);
            foreach (var pack in plan.ResourcePacks)
            {
                var local = hasPackRecords ? recorded[pack.Name] ?? "" : current;
                if (local != pack.LocalVersion)
                    throw new InvalidOperationException(
                        $"分包 {pack.Name} 的本地版本已变化：计划 {pack.LocalVersion}，当前 {local}。请重新获取更新清单。"
                    );
            }
        }
        else if (!string.IsNullOrEmpty(plan.OldGameVersion) && current != plan.OldGameVersion)
            throw new InvalidOperationException(
                $"本地版本已变化：计划 {plan.OldGameVersion}，当前 {current}。请重新获取更新清单。"
            );
        await ResolveDownloadFolderAsync(option);
        var cache = option.ResolveDownloadFolder(baseFolder);
        _installGameResourceCts = state.CancelToken;
        // 提前安装和普通更新始终按清单目标版本获取完整校验索引，缺失时停止安装。
        // 校验锁定本次下载清单的逐包目标，避免沿用界面中的本地版本而误选预下载配置。
        var verificationParameter = new GameResourceParameter
        {
            BundleName = plan.BundleName ?? parameter?.BundleName,
            VerificationPackVersions =
                plan.ResourceVersion == "2"
                    ? plan.ResourcePacks.ToDictionary(x => x.Name, x => x.TargetVersion)
                    : null,
            // 预下载只下载；普通更新和预下载发布后的安装使用正式配置。
            // 只有提前安装仍未发布的版本才使用预下载配置。
            VerificationSource = option.IsAdvance
                ? GameVerificationSource.Predownload
                : GameVerificationSource.Official,
        };
        var verification = await GetVerificationResourceAsync(
            plan.NewGameVersion,
            verificationParameter,
            state.CancelToken.Token
        );
        if (
            !IsExecutable(verification)
            || verification.ZipResources.Count != 0
            || verification.PatchResources.Count != 0
        )
            return false;
        var steps = new List<(IProgressSetup Action, string Name)>();
        void Add(IProgressSetup action, string name, Dictionary<string, object> args)
        {
            action.SetParam(args, GameEventPublisher);
            steps.Add((action, name));
        }
        var patches = plan.PatchResources.Where(x => x.IsSelected && !x.IsGroup).ToList();
        var groups = plan.PatchResources.Where(x => x.IsSelected && x.IsGroup).ToList();
        #region 补丁V2版本
        var grayPreView = this.GrayOpenService.GetOpen((uint)GrayConfigId.CoreDiffV2,false);
        #endregion
        if (patches.Count != 0)
            Add(
                new InstallKrdiffResource(Logger),
                "安装补丁文件",
                new()
                {
                    ["krdiffs"] = patches,
                    ["diffFolderPath"] = Path.Combine(cache, "patchs"),
                    ["gameBaseFolder"] = baseFolder,
                    ["isPreview"] = grayPreView,
                }
            );
        if (groups.Count != 0)
            Add(
                new InstallKrdiffGroupResource(Logger),
                "安装补丁组文件",
                new()
                {
                    ["krpdiffs"] = groups,
                    ["groupFileInfos"] = groups,
                    ["diffFolderPath"] = Path.Combine(cache, "patchGroup"),
                    ["baseFolderPath"] = baseFolder,
                    ["decompressTempFolder"] = Path.Combine(baseFolder, "decompressFolder"),
                    ["isPreview"] = grayPreView,
                }
            );
        if (plan.ZipResources.Any(x => x.IsSelected))
            Add(
                new InstallKrZipResource(Logger),
                "安装压缩包",
                new()
                {
                    ["zipInfos"] = plan.ZipResources.Where(x => x.IsSelected).ToList(),
                    ["zipDownFolder"] = Path.Combine(cache, "zips"),
                    ["baseGamePath"] = baseFolder,
                    ["downloadState"] = state,
                }
            );
        if (plan.DefaultResource.Any(x => x.IsSelected))
            Add(
                new MoveFileResource(Logger),
                "移动更新文件",
                new()
                {
                    ["files"] = plan
                        .DefaultResource.Where(x => x.IsSelected)
                        .ToDictionary(
                            x =>
                                BuildFileHelper.ResolveFilePath(
                                    Path.Combine(cache, "resources"),
                                    x.Dest
                                ),
                            x => BuildFileHelper.ResolveFilePath(baseFolder, x.Dest)
                        ),
                }
            );
        Setups = steps.Select(x => x.Name).Append("重新校验文件").Append("写入配置文件").ToList();
        for (var i = 0; i < steps.Count; i++)
        {
            state.CancelToken.Token.ThrowIfCancellationRequested();
            CurrentSetups = i;
            await GameEventPublisher.PublishStepAsync(steps[i].Name, i, Setups, isProd: false);
            _currentRunningAction = (IAsyncDisposable)steps[i].Action;
            if (await steps[i].Action.ExecuteAsync(true) is not true)
                return false;
        }
        state.CancelToken.Token.ThrowIfCancellationRequested();
        CurrentSetups = steps.Count;
        await GameEventPublisher.PublishStepAsync(
            "重新校验文件",
            CurrentSetups,
            Setups,
            isProd: false
        );
        if (
            !await DownloadFilesAsync(
                verification.DefaultResource.Where(x => x.IsSelected),
                baseFolder,
                state,
                false,
                "重新校验文件",
                cdns: verification.CdnCandidates
            )
        )
            return false;
        state.CancelToken.Token.ThrowIfCancellationRequested();
        foreach (var dest in plan.DeleteFiles)
        {
            var path = BuildFileHelper.ResolveFilePath(baseFolder, dest);
            if (File.Exists(path))
                File.Delete(path);
        }
        state.CancelToken.Token.ThrowIfCancellationRequested();
        CurrentSetups = steps.Count + 1;
        await GameEventPublisher.PublishStepAsync(
            "写入配置文件",
            CurrentSetups,
            Setups,
            isProd: false
        );
        var writer = new WriteGameResourceConfig(
            GameLocalConfig,
            plan.NewGameVersion,
            Config,
            Logger
        );
        await writer.WriteDownloadAndUpDateResultAsync(option, state.CancelToken.Token, plan);
        foreach (
            var (files, child) in new[]
            {
                (
                    plan.PatchResources.Where(x => x.IsSelected && !x.IsGroup).Cast<GameFileInfo>(),
                    "patchs"
                ),
                (
                    plan.PatchResources.Where(x => x.IsSelected && x.IsGroup).Cast<GameFileInfo>(),
                    "patchGroup"
                ),
                (plan.ZipResources.Cast<GameFileInfo>(), "zips"),
                (plan.DefaultResource.AsEnumerable(), "resources"),
            }
        )
        foreach (var file in files)
        {
            var path = BuildFileHelper.ResolveFilePath(Path.Combine(cache, child), file.Dest);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.WriteError($"安装已完成，缓存清理失败：{path}；{ex.Message}");
            }
        }
        state.IsActive = false;
        Logger.WriteInfo("安装完成");
        return true;
    }

    public async Task AdvanceInstallGameResourceAsync(GameResourceParameter? parameter = null)
    {
        var summary = await GetResourceSummaryAsync(parameter);
        var local = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion);
        if (
            local != summary.OfficialVersion
            || summary.Predownload.Availability != GameResourceAvailability.Ready
        )
            return;
        await QueueResourceOperationAsync(
            () => GetGameProdownloadResourceAsync(parameter),
            new InstallOption { IsAdvance = true },
            parameter
        );
    }

    public async Task StartInstallGameResource(
        InstallOption option,
        GameResourceParameter? parameter = null
    )
    {
        var summary = await GetResourceSummaryAsync(parameter);
        var recorded = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.ProdDownloadVersion
        );
        var done = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.ProdDownloadFolderDone
        );
        if (
            (option.IsProd || option.IsAdvance)
            && (
                !bool.TryParse(done, out var completed)
                || !completed
                || recorded
                    != (option.IsAdvance ? summary.PredownloadVersion : summary.OfficialVersion)
            )
        )
            return;
        var plan = option.IsAdvance
            ? await GetGameProdownloadResourceAsync(parameter)
            : await GetUpdateGameResourceAsync(parameter);
        await StartInstallGameResource(plan, option, parameter);
    }

    private async Task ResolveDownloadFolderAsync(InstallOption option)
    {
        if (string.IsNullOrWhiteSpace(option.DownloadFolder) && (option.IsProd || option.IsAdvance))
            option.DownloadFolder = await GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadPath
            );
    }

    public string BuildInstallOptionFolder(InstallOption option, string baseFolder) =>
        option.ResolveDownloadFolder(baseFolder);

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

    private async Task SetCurrentStateNull(bool? isProd)
    {
        var generation = Volatile.Read(ref _operationGeneration);
        var action = _currentRunningAction;
        _currentRunningAction = null;
        var states = new[]
        {
            isProd != true ? DownloadState : null,
            isProd != false ? ProdDownloadState : null,
        };
        try
        {
            foreach (var state in states)
            {
                if (state is null)
                    continue;
                state.IsActive = false;
                if (state.CancelToken is not null)
                    await state.CancelToken.CancelAsync();
            }
            if (action is not null)
                await action.DisposeAsync();
        }
        catch (Exception ex)
        {
            Logger.WriteError($"任务收尾失败：{ex}");
        }
        finally
        {
            if (isProd != true)
                DownloadState = null;
            if (isProd != false)
                ProdDownloadState = null;
            Setups = [];
            CurrentSetups = 0;
            ProgressState.ActiveFiles.Clear();
            GameEventPublisher.Publish(
                new()
                {
                    Type = GameContextActionType.None,
                    Generation = generation,
                    Prod = isProd == true,
                    IsAction = false,
                    IsPause = false,
                }
            );
        }
    }
}
