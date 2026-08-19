using System.Net.Sockets;
using System.Net.WebSockets;
using ChromeCDPSharp.Common;
using ChromeCDPSharp.Models;
using ChromeCDPSharp.Serialization;
using Waves.Core.Common;
using Windows.ApplicationModel.DataTransfer;
using Windows.Security.Credentials.UI;
using ZXing.Aztec.Internal;

namespace Haiyu.ViewModel.ToolkitsViewModel;



public partial class AutoKuroTokenViewModel : ViewModelBase
{
    private const string RoleListApi = "https://api.kurobbs.com/aki/widget/getData";

    private readonly AdbClient _adbClient = new();
    private readonly List<IDisposable> _networkSubscriptions = [];
    private readonly HashSet<string> _trackedRequestIds = [];
    private readonly Lock _trackedRequestGate = new();
    private readonly Queue<string> _logLines = [];

    private CDPClient? _cdpClient;
    private string? _webSocketDebuggerUrl;
    private string? _lastReadableResponseRequestId;
    private Dictionary<string, object?>? _requestHeader;

    public AutoKuroTokenViewModel(IPickersService pickersService)
    {
        PickerService = pickersService;
    }

    public IPickersService PickerService { get; }

    public Window? Window { get; internal set; }

    [ObservableProperty]
    public partial string AdbPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Port { get; set; } = 9222;

    [ObservableProperty]
    public partial ObservableCollection<AdbDeviceInfo> Devices { get; private set; } = [];

    [ObservableProperty]
    public partial AdbDeviceInfo? SelectDevice { get; set; }

    [ObservableProperty]
    public partial WebSocketState WebSocketState { get; set; }

    [ObservableProperty]
    public partial CdpConnectionState CdpState { get; set; }

    [ObservableProperty]
    public partial string LogText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Did { get; set; }

    [ObservableProperty]
    public partial string Token { get; set; }

    [ObservableProperty]
    public partial string PlayerId { get; set; }

    [RelayCommand]
    public async Task SelectAdbPathAsync()
    {
        var openFile = await PickerService.GetFileOpenPicker([".exe"]);
        if (
            openFile is null
            || !openFile.Path.Contains("adb.exe", StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        AdbPath = openFile.Path;
        _adbClient.InitAdbServer(AdbPath);
    }

    [RelayCommand]
    public async Task RefreshDeviceAsync()
    {
        Devices = (await _adbClient.GetDevicesAsync(CTS.Token)).ToObservableCollection();
    }

    [RelayCommand]
    public async Task AutoConnectAsync()
    {
        if (SelectDevice is null)
        {
            AppendLog(LanguageService.GetStringByText("请先选择一个安卓设备。"));
            return;
        }

        var sockets = await _adbClient.GetWebViewSocketsAsync(SelectDevice.Serial);
        if (sockets.Count == 0)
        {
            AppendLog(LanguageService.GetStringByText("未找到 WebView 调试 Socket。"));
            return;
        }

        (WebViewSocketInfo Socket, DevToolsTargetInfo Target)? selectedTarget = null;
        foreach (var socket in sockets)
        {
            IReadOnlyList<DevToolsTargetInfo> targets;
            try
            {
                targets = await _adbClient.GetDevToolsTargetsAsync(
                    SelectDevice.Serial,
                    socket.SocketName,
                    Port,
                    CTS.Token
                );
            }
            catch (Exception ex)
            {
                AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("跳过 Socket {0}: {1}"), socket.SocketName, ex.Message));
                continue;
            }

            foreach (var target in targets.Where(static target => target.IsPageLike))
            {
                AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("发现页面: [{0}] {1} {2}"), socket.SocketName, target.Title, target.Url));
            }

            var candidate = targets
                .Where(static target => target.IsPageLike)
                .Where(static target => !string.Equals(target.Url, "about:blank", StringComparison.OrdinalIgnoreCase))
                .Select(target => (Socket: socket, Target: target))
                .FirstOrDefault();
            if (selectedTarget is null && candidate.Target is not null)
            {
                selectedTarget = candidate;
            }
        }

        if (selectedTarget is null)
        {
            AppendLog(LanguageService.GetStringByText("未找到可监控的非空白 WebView 页面。"));
            return;
        }

        AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("选中页面: {0} {1}"), selectedTarget.Value.Target.Title, selectedTarget.Value.Target.Url));
        _webSocketDebuggerUrl = selectedTarget.Value.Target.WebSocketDebuggerUrl;
        await ConnectCdpClientAsync(_webSocketDebuggerUrl);
    }

    [RelayCommand]
    public async Task ManualReconnectAsync()
    {
        if (_cdpClient is null)
        {
            if (string.IsNullOrWhiteSpace(_webSocketDebuggerUrl))
            {
                AppendLog(LanguageService.GetStringByText("请先连接一次 CDP。"));
                return;
            }

            await ConnectCdpClientAsync(_webSocketDebuggerUrl);
            return;
        }

        AppendLog(LanguageService.GetStringByText("正在重连 CDP..."));
        await _cdpClient.ReconnectAsync(CTS.Token);
        ResetTrackedRequests();
        AppendLog(LanguageService.GetStringByText("CDP 已重连。"));
        await StartTrafficMonitorAsync();
    }

    [RelayCommand]
    public async Task StartTrafficMonitorAsync()
    {
        if (!IsCdpConnected())
        {
            AppendLog(LanguageService.GetStringByText("CDP 未连接，请先连接或手动重连。"));
            return;
        }

        ClearNetworkSubscriptions();
        ResetTrackedRequests();

        _networkSubscriptions.Add(
            _cdpClient!.Subscribe<RequestWillBeSentEvent>(
                "Network.requestWillBeSent",
                CdpJsonContext.Default.RequestWillBeSentEvent,
                e =>
                {
                    if (IsTargetUrl(e.Request.Url))
                    {
                        _requestHeader = e.Request.Headers;
                        if (TryGetHeader(_requestHeader, "did", out var did))
                        {
                            this.Window.DispatcherQueue.TryEnqueue(() =>
                            {
                                this.Did = did?.ToString();
                            });
                        }
                        if (TryGetHeader(_requestHeader, "token", out var token))
                        {
                            this.Window.DispatcherQueue.TryEnqueue(() =>
                            {
                                this.Token = token?.ToString();
                            });
                        }
                        AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("捕获请求: {0} {1}"), e.Request.Method, e.Request.Url));
                    }

                    return ValueTask.CompletedTask;
                }
            )
        );
        _networkSubscriptions.Add(
            _cdpClient.Subscribe<ResponseReceivedEvent>(
                "Network.responseReceived",
                CdpJsonContext.Default.ResponseReceivedEvent,
                e =>
                {
                    if (IsTargetUrl(e.Response.Url))
                    {
                        lock (_trackedRequestGate)
                        {
                            _trackedRequestIds.Add(e.RequestId);
                        }

                        AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("响应头已到达: {0} {1}"), e.Response.Status, e.Response.Url));
                    }

                    return ValueTask.CompletedTask;
                }
            )
        );
        _networkSubscriptions.Add(
            _cdpClient.Subscribe<LoadingFinishedEvent>(
                "Network.loadingFinished",
                CdpJsonContext.Default.LoadingFinishedEvent,
                async e =>
                {
                    if (RemoveTrackedRequest(e.RequestId))
                    {
                        _lastReadableResponseRequestId = e.RequestId;
                        try
                        {
                            var result = await _cdpClient!.SendCommandAsync(
                                "Network.getResponseBody",
                                new GetResponseBodyParams(e.RequestId),
                                CdpJsonContext.Default.GetResponseBodyParams,
                                CdpJsonContext.Default.CdpCommandResponseGetResponseBodyResult,
                                CTS.Token
                            );
                            var jsonO = JsonObject.Parse(result.Body);
                            var playerId = jsonO?["data"]?["userId"];
                            this.Window.DispatcherQueue.TryEnqueue(() =>
                            {
                                this.PlayerId = playerId?.ToString();
                            });
                            AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("已读取目标响应 Body: {0}"), e.RequestId));
                        }
                        catch (Exception ex)
                        {
                            AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("读取响应 Body 失败: {0}"), ex.Message));
                        }
                    }
                }
            )
        );
        _networkSubscriptions.Add(
            _cdpClient.Subscribe<LoadingFailedEvent>(
                "Network.loadingFailed",
                CdpJsonContext.Default.LoadingFailedEvent,
                e =>
                {
                    if (RemoveTrackedRequest(e.RequestId))
                    {
                        AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("响应失败，无法读取 Body: {0}"), e.ErrorText));
                    }

                    return ValueTask.CompletedTask;
                }
            )
        );

        await _cdpClient.SendCommandAsync(
            "Network.enable",
            new NetworkEnableParams(
                MaxTotalBufferSize: 100 * 1024 * 1024,
                MaxResourceBufferSize: 10 * 1024 * 1024,
                MaxPostDataSize: 1024 * 1024),
            CdpJsonContext.Default.NetworkEnableParams,
            CdpJsonContext.Default.CdpCommandResponseEmptyResult,
            CTS.Token
        );

        AppendLog(LanguageService.GetStringByText("已开始监控 Network 流量。"));
    }

    [RelayCommand]
    public async Task ReadResponseBodyAsync()
    {
        if (!IsCdpConnected())
        {
            AppendLog(LanguageService.GetStringByText("CDP 未连接，请先连接或手动重连。"));
            return;
        }

        if (string.IsNullOrWhiteSpace(_lastReadableResponseRequestId))
        {
            AppendLog(LanguageService.GetStringByText("还没有已完成的响应体，请先触发目标请求并等待 loadingFinished。"));
            return;
        }

        var result = await _cdpClient!.SendCommandAsync(
            "Network.getResponseBody",
            new GetResponseBodyParams(_lastReadableResponseRequestId),
            CdpJsonContext.Default.GetResponseBodyParams,
            CdpJsonContext.Default.CdpCommandResponseGetResponseBodyResult,
            CTS.Token
        );

        var body = result.Base64Encoded ? $"[Base64]{result.Body}" : result.Body;
        AppendLog($"Body({body.Length}): {body}");
    }

    private async Task ConnectCdpClientAsync(string webSocketDebuggerUrl)
    {
        ClearNetworkSubscriptions();
        if (_cdpClient is not null)
        {
            _cdpClient.ConnectionStateChanged -= OnCdpClientConnectionStateChanged;
            _cdpClient.EventHandlerException -= OnCdpClientEventHandlerException;
            await _cdpClient.DisposeAsync();
        }

        ResetTrackedRequests();
        _cdpClient = new CDPClient(webSocketDebuggerUrl);
        _cdpClient.ConnectionStateChanged += OnCdpClientConnectionStateChanged;
        _cdpClient.EventHandlerException += OnCdpClientEventHandlerException;
        await _cdpClient.ConnectAsync(CTS.Token);
        AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("CDP 已连接: {0}"), webSocketDebuggerUrl));
        await StartTrafficMonitorAsync();
    }

    private void OnCdpClientConnectionStateChanged(
        object? sender,
        CdpConnectionStateChangedEventArgs e
    )
    {
        Window.DispatcherQueue.TryEnqueue(() =>
        {
            WebSocketState = e.WebSocketState;
            CdpState = e.CurrentState;
        });
    }

    private void OnCdpClientEventHandlerException(object? sender, Exception e)
    {
        AppendLog(LanguageService.FormatByText(LanguageService.GetStringByText("CDP 事件处理异常: {0}"), e.Message));
    }

    private static bool IsTargetUrl(string url)
    {
        return url.Contains(RoleListApi, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCdpConnected()
    {
        return _cdpClient is not null && _cdpClient.ConnectionState == CdpConnectionState.Connected;
    }

    private void ResetTrackedRequests()
    {
        lock (_trackedRequestGate)
        {
            _trackedRequestIds.Clear();
        }

        _lastReadableResponseRequestId = null;
        _requestHeader = null;
    }

    private bool RemoveTrackedRequest(string requestId)
    {
        lock (_trackedRequestGate)
        {
            return _trackedRequestIds.Remove(requestId);
        }
    }

    private static bool TryGetHeader(
        IReadOnlyDictionary<string, object?> headers,
        string name,
        out object? value
    )
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = header.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private void AppendLog(string message)
    {
        Window.DispatcherQueue.TryEnqueue(() =>
        {
            _logLines.Enqueue($"{DateTime.Now:HH:mm:ss} {message}");
            while (_logLines.Count > 200)
            {
                _logLines.Dequeue();
            }

            LogText = string.Join(Environment.NewLine, _logLines.Reverse());
        });
    }

    private void ClearNetworkSubscriptions()
    {
        foreach (var subscription in _networkSubscriptions)
        {
            subscription.Dispose();
        }

        _networkSubscriptions.Clear();
    }

    [RelayCommand]
    async Task CopySession()
    {
        //var result = await UserConsentVerifier.RequestVerificationAsync(
        //    LanguageService.GetStringByText("复制这些信息需要你进行二次确认")
        //);
        //if (result == UserConsentVerificationResult.Verified)
        //{
            
        //}
        var package = new DataPackage();
        package.SetText($"""
            Did:{this.Did}
            Token:{this.Token}
            PlayerId:{this.PlayerId}
            """);
        Clipboard.SetContent(package);
    }

    protected override void OnDisposing()
    {
        ClearNetworkSubscriptions();
        if (_cdpClient is not null)
        {
            _cdpClient.ConnectionStateChanged -= OnCdpClientConnectionStateChanged;
            _cdpClient.EventHandlerException -= OnCdpClientEventHandlerException;
            _cdpClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _cdpClient = null;
        }

        _adbClient.Dispose();
        Window = null;
        base.OnDisposing();
    }
}
