using Waves.Core.Models.Options;

namespace Waves.Core.GameContext;

partial class KuroGameContextBaseV2
{
    private int _resourceOperationActive;
    internal bool IsResourceOperationActive => Volatile.Read(ref _resourceOperationActive) != 0;

    public Task<bool> UpdateGameResourceAsync(string? downloadFolder = null, GameResourceParameter? parameter = null) =>
        QueueResourceOperationAsync(() => GetUpdateGameResourceAsync(parameter),
            new InstallOption { DownloadFolder = downloadFolder }, parameter);

    public Task<bool> StartProdDownloadGameResourceAsync(string? downloadFolder = null, GameResourceParameter? parameter = null) =>
        QueueResourceOperationAsync(() => GetGameProdownloadResourceAsync(parameter),
            new InstallOption { IsProd = true, DownloadFolder = downloadFolder }, parameter);

    private bool IsExecutable(GameVersionInfo plan) => plan.Availability == GameResourceAvailability.Ready
        && !string.IsNullOrWhiteSpace(plan.NewGameVersion)
        && (plan.DefaultResource.Any(x => x.IsSelected) || plan.ZipResources.Any(x => x.IsSelected) || plan.PatchResources.Any(x => x.IsSelected));

    private async Task<bool> QueueResourceOperationAsync(Func<Task<GameVersionInfo>> query,
        InstallOption option, GameResourceParameter? parameter)
    {
        if (Interlocked.CompareExchange(ref _resourceOperationActive, 1, 0) != 0) return false;
        try
        {
            var plan = await query();
            if (!IsExecutable(plan))
            {
                Interlocked.Exchange(ref _resourceOperationActive, 0);
                return false;
            }
            var baseFolder = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder);
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                Interlocked.Exchange(ref _resourceOperationActive, 0);
                return false;
            }
            await ResolveDownloadFolderAsync(option);
            option.DownloadFolder = option.ResolveDownloadFolder(baseFolder);
            var generation = Interlocked.Increment(ref _operationGeneration);
            GameContextOutputArgs.CurrentGeneration.Value = generation;
            _ = Task.Run(async () =>
            {
                try { await ExecuteResourcePlanAsync(plan, option, parameter); }
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
            return true;
        }
        catch
        {
            Interlocked.Exchange(ref _resourceOperationActive, 0);
            throw;
        }
    }

    private async Task<DownloadState> CreateResourceStateAsync(bool isProd, CancellationToken token = default)
    {
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = await GetInitDownloadState(isProd);
        if (isProd)
        {
            _prodDownloadCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            state.CancelToken = _prodDownloadCts;
        }
        else state.CancelToken = _downloadCts;
        state.IsActive = true;
        return state;
    }

    private async Task<bool> ExecuteResourcePlanAsync(GameVersionInfo plan, InstallOption option,
        GameResourceParameter? parameter, CancellationToken token = default)
    {
        if (!IsExecutable(plan)) return false;
        var baseFolder = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder, token);
        if (string.IsNullOrWhiteSpace(baseFolder)) return false;
        await ResolveDownloadFolderAsync(option);
        var cache = option.ResolveDownloadFolder(baseFolder);
        option.DownloadFolder = cache;
        var state = await CreateResourceStateAsync(option.IsProd, token);
        HttpClientService.BuildClient();
        if (option.IsProd)
            await GameLocalConfig.SaveConfigsAsync(new Dictionary<string, string>
            {
                [GameLocalSettingName.ProdDownloadPath] = cache,
                [GameLocalSettingName.ProdDownloadVersion] = plan.NewGameVersion,
                [GameLocalSettingName.ProdDownloadFolderDone] = "False"
            });
        else await GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameUpdateing, "True");

        var tasks = new List<(IEnumerable<GameFileInfo> Files, string Name, string Folder)>();
        var patches = plan.PatchResources.Where(x => x.IsSelected && !x.IsGroup).ToList();
        var groups = plan.PatchResources.Where(x => x.IsSelected && x.IsGroup).ToList();
        if (patches.Count != 0) tasks.Add((patches, "下载补丁文件", Path.Combine(cache, "patchs")));
        if (groups.Count != 0) tasks.Add((groups, "下载补丁组文件", Path.Combine(cache, "patchGroup")));
        if (plan.ZipResources.Count != 0) tasks.Add((plan.ZipResources.Where(x => x.IsSelected), "下载压缩包更新文件", Path.Combine(cache, "zips")));
        if (plan.DefaultResource.Count != 0) tasks.Add((plan.DefaultResource.Where(x => x.IsSelected), "下载更新文件", Path.Combine(cache, "resources")));
        Setups = tasks.Select(x => x.Name).ToList();
        for (var i = 0; i < tasks.Count; i++)
        {
            state.CancelToken.Token.ThrowIfCancellationRequested();
            CurrentSetups = i;
            await GameEventPublisher.PublishStepAsync(tasks[i].Name, i, Setups, isProd: option.IsProd);
            if (!await DownloadFilesAsync(tasks[i].Files, tasks[i].Folder, state, option.IsProd, tasks[i].Name, cdns: plan.CdnCandidates)) return false;
        }
        state.CancelToken.Token.ThrowIfCancellationRequested();
        if (option.IsProd)
        {
            await GameLocalConfig.SaveConfigsAsync(new Dictionary<string, string>
            {
                [GameLocalSettingName.ProdDownloadFolderDone] = "True",
                [GameLocalSettingName.ProdDownloadVersion] = plan.NewGameVersion,
                [GameLocalSettingName.ProdDownloadPath] = cache
            }, state.CancelToken.Token);
            return true;
        }
        return await InstallResourcePlanAsync(plan, option, parameter, state);
    }

    private async Task<bool> DownloadFilesAsync(IEnumerable<GameFileInfo> files, string folder,
        DownloadState state, bool isProd, string name, bool deleteExtra = false, List<string>? skip = null, bool forceFullVerify = false, IEnumerable<GameResourceCdn>? cdns = null)
    {
        _currentRunningAction = null;
        var list = files.ToList();
        if (list.Count == 0) return false;
        // 对真实的每文件候选 URL 测速，保留不同 FromFolder 和失败后的 CDN 重试。
        var sample = list.MinBy(x => Math.Abs(x.Size - 50L * 1024 * 1024))!;
        GameEventPublisher.Publish(new() { Type = GameContextActionType.CdnSelect, Prod = isProd, TipMessage = "正在选择最优CDN" });
        var preferred = await CDNSpeedTester.SelectResourceUrlAsync(sample.UrlCandidates.Count == 0 ? [sample.Url] : sample.UrlCandidates,
            TimeSpan.FromSeconds(40), state.CancelToken.Token, cdns);
        if (preferred is not null)
        {
            var authority = new Uri(preferred).GetLeftPart(UriPartial.Authority);
            foreach (var file in list)
                file.Url = file.UrlCandidates.FirstOrDefault(x => new Uri(x).GetLeftPart(UriPartial.Authority) == authority) ?? file.Url;
        }
        var fast = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.FastVerify, state.CancelToken.Token);
        var action = new DownloadAndVerifyResource(Logger) { ProgressName = name };
        action.SetParam(new Dictionary<string, object>
        {
            ["resource"] = list, ["isDelete"] = deleteExtra, ["folder"] = folder,
            ["httpClient"] = HttpClientService, ["downloadState"] = state, ["isProd"] = isProd,
            ["fastVerify"] = !forceFullVerify && bool.TryParse(fast, out var enabled) && enabled,
            ["skipVerifyFile"] = skip ?? []
        }, GameEventPublisher);
        _currentRunningAction = action;
        return await action.ExecuteAsync(true) is true && !state.CancelToken.IsCancellationRequested;
    }

    public async Task StartInstallGameResource(GameVersionInfo plan, InstallOption option, GameResourceParameter? parameter = null)
    {
        if (Interlocked.CompareExchange(ref _resourceOperationActive, 1, 0) != 0) return;
        try
        {
            if (!IsExecutable(plan)) return;
            GameContextOutputArgs.CurrentGeneration.Value = Interlocked.Increment(ref _operationGeneration);
            await ResolveDownloadFolderAsync(option);
            var state = await CreateResourceStateAsync(false);
            _installGameResourceCts = state.CancelToken;
            await InstallResourcePlanAsync(plan, option, parameter, state);
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

    private async Task<bool> InstallResourcePlanAsync(GameVersionInfo plan, InstallOption option,
        GameResourceParameter? parameter, DownloadState state)
    {
        if (!IsExecutable(plan)) return false;
        var baseFolder = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.GameLauncherBassFolder, state.CancelToken.Token);
        if (string.IsNullOrWhiteSpace(baseFolder)) return false;
        var current = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion, state.CancelToken.Token) ?? "";
        if (!string.IsNullOrEmpty(plan.OldGameVersion) && current != plan.OldGameVersion) return false;
        await ResolveDownloadFolderAsync(option);
        var cache = option.ResolveDownloadFolder(baseFolder);
        _installGameResourceCts = state.CancelToken;
        // 提前安装和普通更新始终按清单目标版本获取完整校验索引，缺失时停止安装。
        var verification = await GetVerificationResourceAsync(plan.NewGameVersion, parameter, state.CancelToken.Token);
        if (!IsExecutable(verification) || verification.ZipResources.Count != 0 || verification.PatchResources.Count != 0) return false;
        var steps = new List<(IProgressSetup Action, string Name)>();
        void Add(IProgressSetup action, string name, Dictionary<string, object> args)
        {
            action.SetParam(args, GameEventPublisher);
            steps.Add((action, name));
        }
        var patches = plan.PatchResources.Where(x => x.IsSelected && !x.IsGroup).ToList();
        var groups = plan.PatchResources.Where(x => x.IsSelected && x.IsGroup).ToList();
        if (patches.Count != 0) Add(new InstallKrdiffResource(Logger), "安装补丁文件", new()
        {
            ["krdiffs"] = patches, ["diffFolderPath"] = Path.Combine(cache, "patchs"), ["gameBaseFolder"] = baseFolder
        });
        if (groups.Count != 0) Add(new InstallKrdiffGroupResource(Logger), "安装补丁组文件", new()
        {
            ["krpdiffs"] = groups, ["groupFileInfos"] = groups,
            ["diffFolderPath"] = Path.Combine(cache, "patchGroup"), ["baseFolderPath"] = baseFolder,
            ["decompressTempFolder"] = Path.Combine(baseFolder, "decompressFolder")
        });
        if (plan.ZipResources.Count != 0) Add(new InstallKrZipResource(Logger), "安装压缩包", new()
        {
            ["zipInfos"] = plan.ZipResources.Where(x => x.IsSelected).ToList(), ["zipDownFolder"] = Path.Combine(cache, "zips"),
            ["baseGamePath"] = baseFolder, ["downloadState"] = state
        });
        if (plan.DefaultResource.Count != 0) Add(new MoveFileResource(Logger), "移动更新文件", new()
        {
            ["files"] = plan.DefaultResource.Where(x => x.IsSelected).ToDictionary(x => BuildFileHelper.ResolveFilePath(Path.Combine(cache, "resources"), x.Dest),
                x => BuildFileHelper.ResolveFilePath(baseFolder, x.Dest))
        });
        Setups = steps.Select(x => x.Name).Append("重新校验文件").ToList();
        for (var i = 0; i < steps.Count; i++)
        {
            state.CancelToken.Token.ThrowIfCancellationRequested();
            CurrentSetups = i;
            await GameEventPublisher.PublishStepAsync(steps[i].Name, i, Setups, isProd: false);
            _currentRunningAction = (IAsyncDisposable)steps[i].Action;
            if (await steps[i].Action.ExecuteAsync(true) is not true) return false;
        }
        state.CancelToken.Token.ThrowIfCancellationRequested();
        CurrentSetups = steps.Count;
        await GameEventPublisher.PublishStepAsync("重新校验文件", CurrentSetups, Setups, isProd: false);
        if (!await DownloadFilesAsync(verification.DefaultResource.Where(x => x.IsSelected), baseFolder, state, false, "重新校验文件", forceFullVerify: true, cdns: verification.CdnCandidates)) return false;
        state.CancelToken.Token.ThrowIfCancellationRequested();
        foreach (var dest in plan.DeleteFiles)
        {
            var path = BuildFileHelper.ResolveFilePath(baseFolder, dest);
            if (File.Exists(path)) File.Delete(path);
        }
        state.CancelToken.Token.ThrowIfCancellationRequested();
        var writer = new WriteGameResourceConfig(GameLocalConfig, plan.NewGameVersion, Config, Logger);
        await writer.WriteDownloadAndUpDateResultAsync(option, state.CancelToken.Token);
        foreach (var (files, child) in new[] {
            (plan.PatchResources.Where(x => x.IsSelected && !x.IsGroup).Cast<GameFileInfo>(), "patchs"),
            (plan.PatchResources.Where(x => x.IsSelected && x.IsGroup).Cast<GameFileInfo>(), "patchGroup"),
            (plan.ZipResources.Cast<GameFileInfo>(), "zips"), (plan.DefaultResource.AsEnumerable(), "resources") })
            foreach (var file in files)
            {
                var path = BuildFileHelper.ResolveFilePath(Path.Combine(cache, child), file.Dest);
                if (File.Exists(path)) File.Delete(path);
            }
        state.IsActive = false;
        Logger.WriteInfo("安装完成");
        return true;
    }

    public async Task AdvanceInstallGameResourceAsync(GameResourceParameter? parameter = null)
    {
        var summary = await GetResourceSummaryAsync(parameter);
        var local = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.LocalGameVersion);
        if (local != summary.OfficialVersion || summary.Predownload.Availability != GameResourceAvailability.Ready) return;
        await QueueResourceOperationAsync(() => GetGameProdownloadResourceAsync(parameter),
            new InstallOption { IsAdvance = true }, parameter);
    }

    public async Task StartInstallGameResource(InstallOption option, GameResourceParameter? parameter = null)
    {
        var summary = await GetResourceSummaryAsync(parameter);
        var recorded = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadVersion);
        var done = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadFolderDone);
        if ((option.IsProd || option.IsAdvance) && (!bool.TryParse(done, out var completed) || !completed
            || recorded != (option.IsAdvance ? summary.PredownloadVersion : summary.OfficialVersion))) return;
        var plan = option.IsAdvance ? await GetGameProdownloadResourceAsync(parameter) : await GetUpdateGameResourceAsync(parameter);
        await StartInstallGameResource(plan, option, parameter);
    }

    private async Task ResolveDownloadFolderAsync(InstallOption option)
    {
        if (string.IsNullOrWhiteSpace(option.DownloadFolder) && (option.IsProd || option.IsAdvance))
            option.DownloadFolder = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadPath);
    }

    public string BuildInstallOptionFolder(InstallOption option, string baseFolder) => option.ResolveDownloadFolder(baseFolder);

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
        if (isProd != true && DownloadState is not null) DownloadState.IsActive = false;
        if (isProd != false && ProdDownloadState is not null) ProdDownloadState.IsActive = false;
        _currentRunningAction = null;
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

}
