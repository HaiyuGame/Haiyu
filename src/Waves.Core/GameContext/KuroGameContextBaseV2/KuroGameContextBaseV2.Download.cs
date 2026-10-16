using Waves.Core.Models.Options;

namespace Waves.Core.GameContext;

partial class KuroGameContextBaseV2
{
    private CancellationTokenSource _downloadCts = null!;
    private CancellationTokenSource _prodDownloadCts = null!;
    private CancellationTokenSource _installGameResourceCts = null!;

    public async Task<bool> StartDownloadTaskAsync(
        string folder,
        bool isDelete = false,
        CancellationToken token = default,
        GameResourceParameter? parameter = null
    )
    {
        if (
            string.IsNullOrWhiteSpace(folder)
            || Interlocked.CompareExchange(ref _resourceOperationActive, 1, 0) != 0
        )
            return false;
        var queued = false;
        GameContextOutputArgs.CurrentGeneration.Value = Interlocked.Increment(ref _operationGeneration);
        GameEventPublisher.Publish(new() { Type = GameContextActionType.Preparing, IsAction = true, TipMessage = "正在获取资源列表…" });
        try
        {
            var plan = await GetInstallGameResourceAsync(parameter, token);
            if (!IsExecutable(plan))
            {
                Interlocked.Exchange(ref _resourceOperationActive, 0);
                return false;
            }
            var saveConfig = new Dictionary<string, string>
            {
                [GameLocalSettingName.GameLauncherBassFolder] = folder,
                [GameLocalSettingName.LocalGameUpdateing] = "True",
            };
            // 下载前只记录路径和进行中状态；分包版本必须等安装校验成功后统一提交。
            if (!await GameLocalConfig.SaveConfigsAsync(saveConfig, token))
                throw new IOException("安装准备配置保存失败，未启动下载。");
            _ = Task.Run(async () =>
            {
                try
                {
                    await StartDownloadAsync(folder, plan, isDelete, null, parameter, token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.WriteError($"安装资源下载失败：{ex}");
                    SystemEventPublisher.Publish(
                        new() { Message = $"安装资源下载失败：{ex.Message}" }
                    );
                }
                finally
                {
                    await SetCurrentStateNull(false);
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
                GameEventPublisher.Publish(new() { Type = GameContextActionType.None });
        }
    }

    private async Task<bool> StartDownloadAsync(
        string folder,
        GameVersionInfo plan,
        bool deleteExtra,
        List<string>? skip,
        GameResourceParameter? parameter,
        CancellationToken token = default
    )
    {
        if (!IsExecutable(plan))
            return false;
        if (plan.ZipResources.Count != 0 || plan.PatchResources.Count != 0)
            return await ExecuteResourcePlanAsync(
                plan,
                InstallOption.CreateDefault(),
                parameter,
                token
            );
        HttpClientService.BuildClient();
        var state = await CreateResourceStateAsync(false, token);
        Setups = ["下载校验", "保存数据"];
        CurrentSetups = 0;
        await GameEventPublisher.PublishStepAsync("下载校验", 0, Setups);
        if (
            !await DownloadFilesAsync(
                plan.DefaultResource.Where(x => x.IsSelected),
                folder,
                state,
                false,
                "下载校验",
                deleteExtra,
                skip,
                cdns: plan.CdnCandidates
            )
        )
            return false;
        state.CancelToken.Token.ThrowIfCancellationRequested();
        CurrentSetups = 1;
        await GameEventPublisher.PublishStepAsync("保存数据", 1, Setups);
        var writer = new WriteGameResourceConfig(
            GameLocalConfig,
            plan.NewGameVersion,
            Config,
            Logger
        );
        await writer.WriteDownloadComplateAsync(GameEventPublisher, true, state.CancelToken.Token, plan);
        state.IsActive = false;
        return true;
    }

    public async Task<bool> RepairGameAsync(
        bool isDelete = true,
        List<string>? skipFilePath = null,
        GameResourceParameter? parameter = null
    )
    {
        if (!IoCircuitBreaker.TryAcquire())
            return false;
        if (Interlocked.CompareExchange(ref _resourceOperationActive, 1, 0) != 0)
        {
            IoCircuitBreaker.Release();
            return false;
        }
        try
        {
            var folder = await GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder
            );
            if (string.IsNullOrWhiteSpace(folder))
                return false;
            // 修复面向所选组合的当前正式资源，不以界面版本字典锁定旧目标或预下载目标。
            var repairParameter = new GameResourceParameter
            {
                BundleName = parameter?.BundleName,
                VerificationSource = GameVerificationSource.Official
            };
            var summary = await GetResourceSummaryAsync(repairParameter);
            var plan = await GetVerificationResourceAsync(summary.OfficialVersion, repairParameter);
            if (
                !IsExecutable(plan)
                || plan.ZipResources.Count != 0
                || plan.PatchResources.Count != 0
            )
                return false;
            // 修复（包括材质切换）不是版本更新，不设置更新续传标记。
            GameContextOutputArgs.CurrentGeneration.Value = Interlocked.Increment(
                ref _operationGeneration
            );
            return await StartDownloadAsync(folder, plan, isDelete, skipFilePath, repairParameter);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Logger.WriteError($"校验游戏失败：{ex}");
            return false;
        }
        finally
        {
            try
            {
                // 成功、取消和失败都结束修复状态；不写入目标版本或安装成功状态。
                try
                {
                    await GameLocalConfig.SaveConfigAsync(
                        GameLocalSettingName.LocalGameUpdateing,
                        "False"
                    );
                }
                finally
                {
                    // 配置写入失败也要发送结束事件，释放下载卡片和任务状态。
                    await SetCurrentStateNull(false);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _resourceOperationActive, 0);
                IoCircuitBreaker.Release();
                await SetCurrentStateNull(false);
            }
        }
    }
}
