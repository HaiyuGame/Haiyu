

namespace Waves.Core.Services;

public class KuroCloudGameContext : IKuroCloudGameContext
{
    internal KuroCloudGameContext(IWavesCloudGameService wavesCloudGameService)
    {
        WavesCloudGameService = wavesCloudGameService;
    }

    /// <summary>
    /// 正在运行的游戏窗口句柄
    /// </summary>
    private nint? GameingWindow { get; set; }

    /// <summary>
    /// 正在运行的游戏窗口标题
    /// </summary>
    private string? GameTitleKey { get; set; }


    public IGameEventPublisher<CloudMessageArgs> CloudGameEventPublisher { get; internal set; }

    public CloudGameProcessTracker CloudGameProcessTracker { get; private set; }

    public GameLocalConfig GameLocalConfig { get; private set; }

    public string GamerConfigPath { get; internal set; }
    public uint CurrentPayType { get; private set; }
    public IWavesCloudGameService WavesCloudGameService { get; }
    #region 排队参数
    public CancellationTokenSource? queqeCTS = null;
    public System.Threading.PeriodicTimer? _queqeTimer;
    private CommonQueueInfo? _lastQueueData;
    #endregion

    public async Task InitAsync()
    {
        if (CloudGameProcessTracker != null)
        {
            await CloudGameProcessTracker.DisposeAsync();
            CloudGameProcessTracker = null;
        }
        CloudGameProcessTracker = new CloudGameProcessTracker();
        await CloudGameProcessTracker.StartTrackingAsync(this.CloudGameEventPublisher);
        Directory.CreateDirectory(GamerConfigPath);
        this.GameLocalConfig = new GameLocalConfig(GamerConfigPath + "\\Settings.bat");
    }

    public async Task StartGameAsync(
        CloudGameLoginSession session,
        IEnumerable<CloudGameNode> nodes,
        CloudGameNode node,
        StreamQualityOptions options,
        uint payType
    )
    {
        var http = CloudGameDataHelper.CreateWebCloudClient(session);
        var bizData = CloudGameDataHelper.CreateCloudBizData(
            nodes.Select(x => new BizCloudNode()
            {
                NodeId = x.NodeList.First().NodeId,
                Result = x.NodeList.First().Delay.ToString(),
            })
        );
        var bizString = JsonSerializer.Serialize(bizData, CloudGameContext.Default.CloudBizData);
        var launchOption = CloudGameDataHelper.BuildLaunchOption(session, options);
        var paramData = CloudGameDataHelper.CreateWebLinkParameters(
            launchOption.Quality.DPI,
            launchOption.Quality.Width,
            launchOption.Quality.Height,
            launchOption.Quality.BitRateMax,
            launchOption.Quality.Fps,
            launchOption.Quality.CodecType,
            bizString,
            nodes,
            node
        );
        this.CurrentPayType = payType;
        var invokeResult =
            await this.WavesCloudGameService.CommonStartGameAsync(
                http,
                session,
                paramData,
                payType
            );
        if (invokeResult == null)
        {
            CloudGameEventPublisher.Publish(
                new CloudMessageArgs(CloudCoreType.Message) { Message = $"启动失败!" }
            );
            await Task.Delay(1000);
            //启动Web串流，通知外部ViewModel接受Session，进行Web启动
            this.CloudGameEventPublisher.Publish(new(CloudCoreType.None));
            return;
        }
        if (invokeResult.Code == 0 && invokeResult.Data != null)
        {
            var option = launchOption.Clone();
            this.CloudGameEventPublisher.Publish(new(CloudCoreType.QueueUp));
            if (queqeCTS != null)
                await queqeCTS.CancelAsync();
            option.IsComplete = true;
            option.StreamOptions = new CloudGameStreamSession()
            {
                TenantKey = CloudGameDataHelper.WelinkTenantKey,
                ProviderType = invokeResult.Data.ProviderType,
                ScriptUrl = CloudGameMethod.WelinkScriptUrl,
                GameId = CloudGameDataHelper.WelinkGameId,
                StartParameters = paramData,
                RegionName = invokeResult.Data.RegionName,
                DispatchMessage = invokeResult.Data.DispatchResult.DispatchMsg,
                TencentUserKey = invokeResult.Data.DispatchResult.UserKey,
                TencentDeviceId = invokeResult.Data.DispatchResult.DeviceId,
                TencentAllocRespJson = invokeResult.Data.DispatchResult.AllocRespJson,
                TencentToken = invokeResult.Data.DispatchResult.Tk,
                SessionKey = "1-",
            };
            this.CloudGameEventPublisher.Publish(
                new(CloudCoreType.OpeningWeb) { QueueResult = option }
            );
            return;
        }
        if (invokeResult.Code == 1712)
        {
            this.CloudGameEventPublisher.Publish(new(CloudCoreType.QueueUp));
            if (queqeCTS != null)
                await queqeCTS.CancelAsync();
            if (_queqeTimer != null)
                _queqeTimer.Dispose();
            queqeCTS = new();
            _queqeTimer = new PeriodicTimer(System.TimeSpan.FromSeconds(2));
            _ = Task.Run(async () => await QueqeTask(session, paramData, launchOption));
        }
    }

    public async Task StopQueueAsync()
    {
        await ClearActiveAsync();
    }

    /// <summary>
    /// 不需要判断Type
    /// </summary>
    /// <returns></returns>
    public async Task<KuroCLoudGameCoreState> GetCloudStateAsync()
    {
        KuroCLoudGameCoreState args = new KuroCLoudGameCoreState();
        var key = this.GameTitleKey;
        if (_lastQueueData != null)
        {
            args.IsQueue = true;
            args.QueueWaitTime = _lastQueueData.WaitingTime;
            args.QueueQty = _lastQueueData.SeatNo;
            args.Region = _lastQueueData.RegionName;
        }
        //这里的WindowHandle和WindowTitleKey是为了兼容外部ViewModel的设计，由于内核抽象原因并不具备窗口管理功能
        //由外部ViewModel 通过 WindowsAPI来确定游戏是否在运行。
        args.WindowHandle = GameingWindow;
        args.WindowTitleKey = GameTitleKey;
        return await Task.FromResult(args);
    }

    public void SetGameingWindow(nint handle, string titleKey)
    {
        this.GameingWindow = handle;
        this.GameTitleKey = titleKey;
    }

    private async Task QueqeTask(
        CloudGameLoginSession session,
        WelinkStartParameters welinkParam,
        BrowserSessionLaunchOptions launchOption
    )
    {
        var option = launchOption.Clone();
        if (_queqeTimer == null || _queqeTimer.Period == TimeSpan.Zero || queqeCTS == null)
        {
            this.CloudGameEventPublisher.Publish(
                new(CloudCoreType.Message) { Message = "排队计时器异常！请重试进入游戏" }
            );
            return;
        }
        try
        {
            int errCount = 0;
            while (true)
            {
                if (queqeCTS == null || queqeCTS.IsCancellationRequested)
                {
                    await ClearActiveAsync().ConfigureAwait(false);
                    return;
                }
                await _queqeTimer.WaitForNextTickAsync(queqeCTS.Token).ConfigureAwait(false);
                if (errCount > 2000)
                {
                    //排队失败,66*5分钟超时
                    this.CloudGameEventPublisher.Publish(
                        new(CloudCoreType.Message)
                        {
                            Message =
                                $"排队异常无果，总耗时{(errCount * _queqeTimer.Period.Seconds) * 5}s",
                        }
                    );
                    this.CloudGameEventPublisher.Publish(new(CloudCoreType.None));
                    return;
                }
                var http = CloudGameDataHelper.CreateWebCloudClient(session);
                var queueResult =
                    await this.WavesCloudGameService.CommonQueueInfoAsync(
                        http,
                        session
                    );
                if (queueResult == null)
                {
                    errCount++;
                    continue;
                }
                if (
                    queueResult.Code == 0
                    && queueResult.Data is not null
                    && (queueResult.Data.Code == 0 || queueResult.Data.Code == 200)
                )
                {
                    await queqeCTS.CancelAsync();
                    option.IsComplete = true;
                    option.StreamOptions = new CloudGameStreamSession()
                    {
                        TenantKey = CloudGameDataHelper.WelinkTenantKey,
                        ProviderType = queueResult.Data.ProviderType,
                        ScriptUrl = CloudGameMethod.WelinkScriptUrl,
                        GameId = CloudGameDataHelper.WelinkGameId,
                        StartParameters = welinkParam,
                        RegionName = queueResult.Data.RegionName,
                        DispatchMessage = queueResult.Data.DispatchResult.DispatchMsg,
                        TencentUserKey = queueResult.Data.DispatchResult.UserKey,
                        TencentDeviceId = queueResult.Data.DispatchResult.DeviceId,
                        TencentAllocRespJson = queueResult.Data.DispatchResult.AllocRespJson,
                        TencentToken = queueResult.Data.DispatchResult.Tk,
                        SessionKey = "1-",
                    };
                    this.CloudGameEventPublisher.Publish(
                        new(CloudCoreType.OpeningWeb) { QueueResult = option }
                    );
                    if (_lastQueueData != null)
                    {
                        _lastQueueData = null;
                    }
                    return;
                }
                if (queueResult.Code == 1712)
                {
                    option.IsComplete = false;
                    this.CloudGameEventPublisher.Publish(
                        new(CloudCoreType.QueueUp)
                        {
                            QueueResult = option,
                            IsQueue = true,
                            QueueQty = queueResult.Data?.SeatNo ?? 0,
                            QueueTime = queueResult.Data?.WaitingTime ?? 0,
                            CurrentRegion = queueResult.Data?.RegionName ?? "",
                            PayType = this.CurrentPayType,
                        }
                    );
                    _lastQueueData = queueResult.Data;
                }
                if (queueResult.Code == 1724)
                {
                    option.IsComplete = false;
                    this.CloudGameEventPublisher.Publish(
                        new(CloudCoreType.Message)
                        {
                            Message = queueResult.Msg ?? "排队异常，请重试",
                            QueueResult = option,
                        }
                    );
                    await queqeCTS.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        { //不捕获取消异常 }
        }
    }

    /// <summary>
    /// 取消当前活动排队
    /// </summary>
    /// <returns></returns>
    public async Task ClearActiveAsync()
    {
        _queqeTimer?.Dispose();
        if (queqeCTS != null)
            await queqeCTS.CancelAsync();
        queqeCTS = null;
        this.CloudGameEventPublisher.Publish(new(CloudCoreType.None));
    }

    public void ClearWindow()
    {
        this.GameingWindow = null;
        this.GameTitleKey = null;
    }

    public async Task<StreamQualityOptions?> GetOptionsAsync(int dpi, int width, int height)
    {
        try
        {
            var quality = await this.GameLocalConfig.GetConfigAsync(
                CloudGameLocalSettingName.QualityType
            );
            var fps = await this.GameLocalConfig.GetConfigAsync(CloudGameLocalSettingName.Fps);
            if (!int.TryParse(fps, out var targetFps))
            {
                return null;
            }
            var enable = await this.GameLocalConfig.GetConfigAsync(
                CloudGameLocalSettingName.EnableImageEnhancement
            );
            if (
                bool.TryParse(enable, out var enableImage)
                && Enum.TryParse<CloudQualityType>(quality, out var quEnum)
            )
            {
                var mode = new StreamQualityOptions(
                    CloudGameMethod.DefaultBitRate,
                    CloudGameMethod.MinBitRate,
                    targetFps,
                    width,
                    height,
                    CloudGameMethod.DefaultCodecType,
                    "0",
                    enableImage,
                    dpi,
                    quEnum
                );
                return CloudGameDataHelper.ScaleQualityToPhysical(mode, false);
            }
            else
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}
