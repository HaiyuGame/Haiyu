using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ChromeCDPSharp.Models;
using ChromeCDPSharp.Serialization;

namespace ChromeCDPSharp.Common;

public sealed class CDPClient : IAsyncDisposable
{
    private readonly Uri _debugUri;
    private readonly ConcurrentDictionary<long, PendingCommand> _pendingCommands = [];
    private readonly Dictionary<string, List<IEventSink>> _subscribers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<IEventSink>> _waiters = new(StringComparer.Ordinal);
    private readonly Lock _eventGate = new();
    private ClientWebSocket _webSocket = new();
    private CancellationTokenSource _connectionCancellationSource = new();
    private Task? _readerTask;
    private long _nextCommandId;
    private CdpConnectionState _connectionState = CdpConnectionState.None;
    private bool _disposed;

    public CDPClient(string webSocketDebugUrl)
    {
        if (string.IsNullOrWhiteSpace(webSocketDebugUrl))
        {
            throw new ArgumentException("WebSocket debug URL cannot be null or empty.", nameof(webSocketDebugUrl));
        }

        DebugUrl = webSocketDebugUrl;
        _debugUri = new Uri(webSocketDebugUrl, UriKind.Absolute);
    }

    public string DebugUrl { get; }

    public WebSocketState State => _webSocket.State;

    public CdpConnectionState ConnectionState => _connectionState;

    public Exception? LastException { get; private set; }

    public event EventHandler<CdpConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public event EventHandler<Exception>? EventHandlerException;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_webSocket.State == WebSocketState.Open)
        {
            return;
        }

        if (_webSocket.State != WebSocketState.None)
        {
            ResetTransport();
        }

        SetConnectionState(CdpConnectionState.Connecting, message: "Connecting to CDP WebSocket.");

        try
        {
            await _webSocket.ConnectAsync(_debugUri, cancellationToken);
            SetConnectionState(CdpConnectionState.Connected, message: "Connected to CDP WebSocket.");
            _readerTask = Task.Run(() => ReaderLoopAsync(_connectionCancellationSource.Token));
        }
        catch (Exception ex)
        {
            LastException = ex;
            SetConnectionState(CdpConnectionState.Faulted, ex, "Failed to connect to CDP WebSocket.");
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectCoreAsync(clearEventRegistrations: true, cancellationToken);
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_webSocket.State == WebSocketState.Open)
        {
            await DisconnectCoreAsync(clearEventRegistrations: false, cancellationToken);
        }
        else
        {
            FailAllPending(new OperationCanceledException("CDP session is reconnecting."));
            FailAllWaiters(new OperationCanceledException("CDP session is reconnecting."));
        }

        ResetTransport();
        await ConnectAsync(cancellationToken);
    }

    public async Task<TResponse> SendCommandAsync<TParams, TResponse>(
        string method,
        TParams? parameters,
        JsonTypeInfo<TParams>? paramsTypeInfo,
        JsonTypeInfo<CdpCommandResponse<TResponse>> responseTypeInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(responseTypeInfo);
        ThrowIfDisposed();
        EnsureConnected();

        if (parameters is not null && paramsTypeInfo is null)
        {
            throw new ArgumentNullException(nameof(paramsTypeInfo), "AOT mode requires JsonTypeInfo for command params.");
        }

        long commandId = Interlocked.Increment(ref _nextCommandId);
        PendingCommand pendingCommand = new(commandId, method);
        if (!_pendingCommands.TryAdd(commandId, pendingCommand))
        {
            throw new InvalidOperationException($"Failed to register CDP command {commandId}.");
        }

        using CancellationTokenSource linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCancellationSource.Token);
        using CancellationTokenRegistration cancellationRegistration = linkedCancellationSource.Token.Register(
            static state => ((PendingCommand)state!).TrySetCanceled(),
            pendingCommand);

        try
        {
            ReadOnlyMemory<byte> payload = CreateCommandPayload(commandId, method, parameters, paramsTypeInfo);
            await _webSocket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);

            string rawResponse = await pendingCommand.WaitAsync(linkedCancellationSource.Token);
            CdpCommandResponse<TResponse>? response = JsonSerializer.Deserialize(rawResponse, responseTypeInfo);
            if (response is null)
            {
                throw new InvalidOperationException($"CDP command '{method}' returned an empty response payload.");
            }

            if (response.Error is not null)
            {
                throw new CdpProtocolException(method, response.Error);
            }

            return response.Result
                ?? throw new InvalidOperationException($"CDP command '{method}' returned no result payload.");
        }
        finally
        {
            _pendingCommands.TryRemove(commandId, out _);
        }
    }

    public IDisposable Subscribe<TEvent>(
        string method,
        JsonTypeInfo<TEvent> eventTypeInfo,
        Func<TEvent, ValueTask> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(eventTypeInfo);
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();

        EventSubscription<TEvent> subscription = new(
            method,
            eventTypeInfo,
            handler,
            remove: entry => RemoveEventSink(_subscribers, method, entry));

        AddEventSink(_subscribers, method, subscription);
        return subscription;
    }

    public Task<TEvent> WaitForEventAsync<TEvent>(
        string method,
        JsonTypeInfo<TEvent> eventTypeInfo,
        Predicate<TEvent> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(eventTypeInfo);
        ArgumentNullException.ThrowIfNull(predicate);
        ThrowIfDisposed();

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }

        CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCancellationSource.Token);
        timeoutSource.CancelAfter(timeout);

        EventWaiter<TEvent> waiter = new(
            method,
            eventTypeInfo,
            predicate,
            timeoutSource,
            cancellationToken,
            remove: entry => RemoveEventSink(_waiters, method, entry));

        AddEventSink(_waiters, method, waiter);
        waiter.RegisterCancellation();
        return waiter.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionCancellationSource.Cancel();
        FailAllPending(new OperationCanceledException("CDP session has been disposed."));
        FailAllWaiters(new OperationCanceledException("CDP session has been disposed."));
        ClearEventRegistrations();
        _webSocket.Dispose();
        _connectionCancellationSource.Dispose();
        SetConnectionState(CdpConnectionState.Disposed, message: "CDP session has been disposed.");
    }

    private async Task ReaderLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                string message = await ReceiveMessageAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                CdpIncomingMessage? incomingMessage = JsonSerializer.Deserialize(message, CdpJsonContext.Default.CdpIncomingMessage);
                if (incomingMessage is null)
                {
                    continue;
                }

                if (incomingMessage.Id is long responseId)
                {
                    if (_pendingCommands.TryGetValue(responseId, out PendingCommand? pendingCommand))
                    {
                        pendingCommand.TrySetResult(message);
                    }

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(incomingMessage.Method))
                {
                    JsonElement eventPayload = incomingMessage.Params.ValueKind == JsonValueKind.Undefined
                        ? default
                        : incomingMessage.Params.Clone();

                    DispatchEvent(incomingMessage.Method, eventPayload);
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (_connectionState is not CdpConnectionState.Disconnecting and not CdpConnectionState.Disconnected and not CdpConnectionState.Disposed)
            {
                SetConnectionState(CdpConnectionState.Disconnected, message: "CDP receive loop was canceled.");
            }
        }
        catch (WebSocketException ex)
        {
            LastException = ex;
            FailAllPending(ex);
            FailAllWaiters(ex);
            SetConnectionState(CdpConnectionState.Faulted, ex, "CDP WebSocket receive loop failed.");
        }
        catch (Exception ex)
        {
            LastException = ex;
            FailAllPending(ex);
            FailAllWaiters(ex);
            SetConnectionState(CdpConnectionState.Faulted, ex, "CDP receive loop failed.");
        }
    }

    private async Task<string> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        ArrayBufferWriter<byte> buffer = new();

        while (true)
        {
            Memory<byte> receiveBuffer = buffer.GetMemory(16 * 1024);
            ValueWebSocketReceiveResult result = await _webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                SetConnectionState(CdpConnectionState.Disconnected, message: "CDP WebSocket close frame received.");
                throw new WebSocketException("CDP WebSocket closed unexpectedly.");
            }

            buffer.Advance(result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(buffer.WrittenSpan);
            }
        }
    }

    private void DispatchEvent(string method, JsonElement payload)
    {
        IEventSink[] subscribers = GetEventSinksSnapshot(_subscribers, method);
        IEventSink[] waiters = GetEventSinksSnapshot(_waiters, method);

        foreach (IEventSink waiter in waiters)
        {
            waiter.TryHandle(payload);
        }

        foreach (IEventSink subscriber in subscribers)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await subscriber.TryHandleAsync(payload);
                    }
                    catch (Exception ex)
                    {
                        EventHandlerException?.Invoke(this, ex);
                    }
                },
                _connectionCancellationSource.Token);
        }
    }

    private static ReadOnlyMemory<byte> CreateCommandPayload<TParams>(
        long commandId,
        string method,
        TParams? parameters,
        JsonTypeInfo<TParams>? paramsTypeInfo)
    {
        ArrayBufferWriter<byte> writerBuffer = new();
        using Utf8JsonWriter writer = new(writerBuffer);
        writer.WriteStartObject();
        writer.WriteNumber("id", commandId);
        writer.WriteString("method", method);

        if (parameters is not null)
        {
            writer.WritePropertyName("params");
            JsonSerializer.Serialize(writer, parameters, paramsTypeInfo!);
        }

        writer.WriteEndObject();
        writer.Flush();
        return writerBuffer.WrittenMemory;
    }

    private void AddEventSink(Dictionary<string, List<IEventSink>> map, string method, IEventSink sink)
    {
        lock (_eventGate)
        {
            if (!map.TryGetValue(method, out List<IEventSink>? sinks))
            {
                sinks = [];
                map[method] = sinks;
            }

            sinks.Add(sink);
        }
    }

    private void RemoveEventSink(Dictionary<string, List<IEventSink>> map, string method, IEventSink sink)
    {
        lock (_eventGate)
        {
            if (!map.TryGetValue(method, out List<IEventSink>? sinks))
            {
                return;
            }

            _ = sinks.Remove(sink);
            if (sinks.Count == 0)
            {
                map.Remove(method);
            }
        }
    }

    private IEventSink[] GetEventSinksSnapshot(Dictionary<string, List<IEventSink>> map, string method)
    {
        lock (_eventGate)
        {
            return map.TryGetValue(method, out List<IEventSink>? sinks)
                ? [.. sinks]
                : [];
        }
    }

    private void FailAllPending(Exception exception)
    {
        foreach (KeyValuePair<long, PendingCommand> pending in _pendingCommands)
        {
            pending.Value.TrySetException(exception);
        }

        _pendingCommands.Clear();
    }

    private void FailAllWaiters(Exception exception)
    {
        lock (_eventGate)
        {
            foreach (List<IEventSink> sinks in _waiters.Values)
            {
                foreach (IEventSink sink in sinks)
                {
                    sink.TrySetException(exception);
                }
            }

            _waiters.Clear();
        }
    }

    private void ClearEventRegistrations()
    {
        lock (_eventGate)
        {
            _subscribers.Clear();
            _waiters.Clear();
        }
    }

    private async Task DisconnectCoreAsync(bool clearEventRegistrations, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        SetConnectionState(CdpConnectionState.Disconnecting, message: "Disconnecting CDP session.");
        _connectionCancellationSource.Cancel();

        if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                using CancellationTokenSource closeTimeout = new(TimeSpan.FromSeconds(3));
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", closeTimeout.Token);
            }
            catch (WebSocketException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }
        FailAllPending(new OperationCanceledException("CDP session has been disconnected."));
        FailAllWaiters(new OperationCanceledException("CDP session has been disconnected."));
        if (clearEventRegistrations)
        {
            ClearEventRegistrations();
        }

        SetConnectionState(CdpConnectionState.Disconnected, message: "CDP session has been disconnected.");
    }

    private void ResetTransport()
    {
        _readerTask = null;
        _webSocket.Dispose();
        _webSocket = new ClientWebSocket();
        _connectionCancellationSource.Dispose();
        _connectionCancellationSource = new CancellationTokenSource();
    }

    private void EnsureConnected()
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("CDPClient is not connected.");
        }
    }

    private void SetConnectionState(CdpConnectionState state, Exception? exception = null, string? message = null)
    {
        CdpConnectionState previousState = _connectionState;
        if (previousState == state && exception is null && message is null)
        {
            return;
        }

        _connectionState = state;
        ConnectionStateChanged?.Invoke(
            this,
            new CdpConnectionStateChangedEventArgs(
                previousState,
                state,
                _webSocket.State,
                exception,
                message));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private interface IEventSink
    {
        void TryHandle(JsonElement payload);

        ValueTask TryHandleAsync(JsonElement payload);

        void TrySetException(Exception exception);
    }

    private sealed class PendingCommand
    {
        private readonly TaskCompletionSource<string> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingCommand(long id, string method)
        {
            Id = id;
            Method = method;
        }

        public long Id { get; }

        public string Method { get; }

        public Task<string> WaitAsync(CancellationToken cancellationToken)
        {
            return _completionSource.Task.WaitAsync(cancellationToken);
        }

        public void TrySetResult(string message)
        {
            _ = _completionSource.TrySetResult(message);
        }

        public void TrySetException(Exception exception)
        {
            _ = _completionSource.TrySetException(exception);
        }

        public void TrySetCanceled()
        {
            _ = _completionSource.TrySetCanceled();
        }
    }

    private sealed class EventSubscription<TEvent> : IEventSink, IDisposable
    {
        private readonly JsonTypeInfo<TEvent> _eventTypeInfo;
        private readonly Func<TEvent, ValueTask> _handler;
        private readonly Action<IEventSink> _remove;
        private int _disposed;

        public EventSubscription(
            string method,
            JsonTypeInfo<TEvent> eventTypeInfo,
            Func<TEvent, ValueTask> handler,
            Action<IEventSink> remove)
        {
            Method = method;
            _eventTypeInfo = eventTypeInfo;
            _handler = handler;
            _remove = remove;
        }

        public string Method { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _remove(this);
        }

        public void TryHandle(JsonElement payload)
        {
        }

        public async ValueTask TryHandleAsync(JsonElement payload)
        {
            if (_disposed != 0)
            {
                return;
            }

            TEvent? typedEvent = JsonSerializer.Deserialize(payload, _eventTypeInfo);
            if (typedEvent is null)
            {
                return;
            }

            await _handler(typedEvent);
        }

        public void TrySetException(Exception exception)
        {
        }
    }

    private sealed class EventWaiter<TEvent> : IEventSink
    {
        private readonly JsonTypeInfo<TEvent> _eventTypeInfo;
        private readonly Predicate<TEvent> _predicate;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _externalCancellationToken;
        private readonly Action<IEventSink> _remove;
        private readonly TaskCompletionSource<TEvent> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _registration;
        private int _completed;

        public EventWaiter(
            string method,
            JsonTypeInfo<TEvent> eventTypeInfo,
            Predicate<TEvent> predicate,
            CancellationTokenSource cancellationTokenSource,
            CancellationToken externalCancellationToken,
            Action<IEventSink> remove)
        {
            Method = method;
            _eventTypeInfo = eventTypeInfo;
            _predicate = predicate;
            _cancellationTokenSource = cancellationTokenSource;
            _externalCancellationToken = externalCancellationToken;
            _remove = remove;
            Task = _completionSource.Task;
        }

        public string Method { get; }

        public Task<TEvent> Task { get; }

        public void RegisterCancellation()
        {
            _registration = _cancellationTokenSource.Token.Register(static state => ((EventWaiter<TEvent>)state!).Cancel(), this);
        }

        public void TryHandle(JsonElement payload)
        {
            if (_completed != 0)
            {
                return;
            }

            TEvent? typedEvent = JsonSerializer.Deserialize(payload, _eventTypeInfo);
            if (typedEvent is null || !_predicate(typedEvent))
            {
                return;
            }

            if (Interlocked.Exchange(ref _completed, 1) != 0)
            {
                return;
            }

            Cleanup();
            _ = _completionSource.TrySetResult(typedEvent);
        }

        public ValueTask TryHandleAsync(JsonElement payload)
        {
            TryHandle(payload);
            return ValueTask.CompletedTask;
        }

        public void TrySetException(Exception exception)
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0)
            {
                return;
            }

            Cleanup();
            _ = _completionSource.TrySetException(exception);
        }

        private void Cancel()
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0)
            {
                return;
            }

            Cleanup();
            if (_externalCancellationToken.IsCancellationRequested)
            {
                _ = _completionSource.TrySetCanceled(_externalCancellationToken);
                return;
            }

            _ = _completionSource.TrySetException(new TimeoutException($"Timed out while waiting for CDP event '{Method}'."));
        }

        private void Cleanup()
        {
            _registration.Dispose();
            _remove(this);
            _cancellationTokenSource.Dispose();
        }
    }
}
