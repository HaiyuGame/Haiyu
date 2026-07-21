using System.Diagnostics;
using System.Net.Http.Json;
using ChromeCDPSharp.Models;
using ChromeCDPSharp.Serialization;

namespace ChromeCDPSharp.Common;

public sealed class AdbClient : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly Lock _forwardGate = new();
    private readonly HashSet<ForwardedPort> _forwardedPorts = [];

    public string AdbPath { get; private set; }

    public AdbClient()
    {
        AdbPath = ResolveAdbExecutablePath();
    }

    public void InitAdbServer(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("ADB executable path cannot be null or empty.", nameof(exePath));
        }

        RemoveTrackedForwardsAsync().GetAwaiter().GetResult();
        AdbPath = exePath;
    }

    public async Task<IReadOnlyList<AdbDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        CommandResult result = await RunAdbAsync("devices -l", cancellationToken);
        EnsureSuccess(result, "Failed to list adb devices.");

        return ParseDevices(result.StandardOutput);
    }

    public async Task<string> GetSingleDeviceSerialAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AdbDeviceInfo> devices = await GetDevicesAsync(cancellationToken);
        AdbDeviceInfo[] onlineDevices = devices
            .Where(static device => device.IsOnline)
            .ToArray();

        return onlineDevices.Length switch
        {
            0 => throw new InvalidOperationException("No Android device is online in adb."),
            > 1 => throw new InvalidOperationException("Multiple adb devices are connected. First version supports exactly one device."),
            _ => onlineDevices[0].Serial
        };
    }

    public async Task<IReadOnlyList<WebViewSocketInfo>> GetWebViewSocketsAsync(string deviceSerial, CancellationToken cancellationToken = default)
    {
        CommandResult result = await RunAdbAsync($"-s {Quote(deviceSerial)} shell cat /proc/net/unix", cancellationToken);
        EnsureSuccess(result, "Failed to read /proc/net/unix from the device.");

        WebViewSocketInfo[] sockets = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseSocketLine)
            .Where(static socket => socket is not null)
            .DistinctBy(static socket => socket!.SocketName, StringComparer.Ordinal)
            .Cast<WebViewSocketInfo>()
            .ToArray();

        if (sockets.Length == 0)
        {
            throw new InvalidOperationException("No debuggable Android WebView sockets were found. Make sure the app enabled WebView debugging.");
        }

        return sockets;
    }

    public async Task ForwardAsync(string deviceSerial, int localPort, string socketName, CancellationToken cancellationToken = default)
    {
        await RunAndEnsureAsync($"-s {Quote(deviceSerial)} forward tcp:{localPort} localabstract:{Quote(socketName)}", "Failed to forward adb port.", cancellationToken);
        lock (_forwardGate)
        {
            _forwardedPorts.Add(new ForwardedPort(deviceSerial, localPort));
        }
    }

    public async Task RemoveForwardAsync(string deviceSerial, int localPort, CancellationToken cancellationToken = default)
    {
        await RunAdbAsync($"-s {Quote(deviceSerial)} forward --remove tcp:{localPort}", cancellationToken);
        lock (_forwardGate)
        {
            _forwardedPorts.Remove(new ForwardedPort(deviceSerial, localPort));
        }
    }

    public async Task RemoveTrackedForwardsAsync(CancellationToken cancellationToken = default)
    {
        ForwardedPort[] forwardedPorts;
        lock (_forwardGate)
        {
            forwardedPorts = [.. _forwardedPorts];
        }

        foreach (ForwardedPort forwardedPort in forwardedPorts)
        {
            try
            {
                await RemoveForwardAsync(forwardedPort.DeviceSerial, forwardedPort.LocalPort, cancellationToken);
            }
            catch (InvalidOperationException)
            {
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
    }

    public async Task<IReadOnlyList<DevToolsTargetInfo>> GetDevToolsTargetsAsync(
        string deviceSerial,
        string socketName,
        int localPort,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceSerial);
        ArgumentException.ThrowIfNullOrWhiteSpace(socketName);

        await RemoveForwardAsync(deviceSerial, localPort, cancellationToken);
        await ForwardAsync(deviceSerial, localPort, socketName, cancellationToken);

        return await GetDevToolsTargetsFromForwardedPortAsync(localPort, cancellationToken);
    }

    public async Task<string> GetWebSocketDebuggerUrlAsync(
        string deviceSerial,
        string socketName,
        int localPort = 9222,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DevToolsTargetInfo> targets = await GetDevToolsTargetsAsync(deviceSerial, socketName, localPort, cancellationToken);
        DevToolsTargetInfo target = targets.FirstOrDefault(static target => target.IsPageLike)
            ?? throw new InvalidOperationException("No page-like DevTools target was exposed by the selected WebView socket.");

        if (string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl))
        {
            throw new InvalidOperationException("The selected DevTools target does not expose a WebSocket debugger URL.");
        }

        return NormalizeForwardedWebSocketDebuggerUrl(target.WebSocketDebuggerUrl, localPort);
    }

    public async Task<string> GetWebSocketDebuggerUrlAsync(
        string deviceSerial,
        int localPort = 9222,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebViewSocketInfo> sockets = await GetWebViewSocketsAsync(deviceSerial, cancellationToken);
        return await GetWebSocketDebuggerUrlAsync(deviceSerial, sockets[0].SocketName, localPort, cancellationToken);
    }

    public static IReadOnlyList<AdbDeviceInfo> ParseDevices(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        List<AdbDeviceInfo> devices = [];

        foreach (string rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AdbDeviceInfo? device = ParseDeviceLine(line);
            if (device is not null)
            {
                devices.Add(device);
            }
        }

        return devices;
    }

    public static AdbDeviceInfo? ParseDeviceLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        string[] parts = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        string serial = parts[0];
        string state = parts[1];

        Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 2; i < parts.Length; i++)
        {
            string part = parts[i];
            int separatorIndex = part.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == part.Length - 1)
            {
                properties[part] = string.Empty;
                continue;
            }

            string key = part[..separatorIndex];
            string value = part[(separatorIndex + 1)..];
            properties[key] = value;
        }

        properties.TryGetValue("product", out string? product);
        properties.TryGetValue("model", out string? model);
        properties.TryGetValue("device", out string? deviceName);
        properties.TryGetValue("transport_id", out string? transportId);
        properties.TryGetValue("usb", out string? usb);
        properties.TryGetValue("features", out string? features);

        return new AdbDeviceInfo(
            serial,
            state,
            product,
            model,
            deviceName,
            transportId,
            usb,
            features,
            properties);
    }

    private async Task RunAndEnsureAsync(string arguments, string message, CancellationToken cancellationToken)
    {
        CommandResult result = await RunAdbAsync(arguments, cancellationToken);
        EnsureSuccess(result, message);
    }

    private async Task<IReadOnlyList<DevToolsTargetInfo>> GetDevToolsTargetsFromForwardedPortAsync(int port, CancellationToken cancellationToken)
    {
        Uri[] endpoints =
        [
            new($"http://127.0.0.1:{port}/json/list"),
            new($"http://127.0.0.1:{port}/json")
        ];

        foreach (Uri endpoint in endpoints)
        {
            try
            {
                List<DevToolsTargetInfo>? targets = await _httpClient.GetFromJsonAsync(endpoint, CdpJsonContext.Default.ListDevToolsTargetInfo, cancellationToken);
                if (targets is { Count: > 0 })
                {
                    return targets
                        .Select(target => target with
                        {
                            WebSocketDebuggerUrl = NormalizeForwardedWebSocketDebuggerUrl(target.WebSocketDebuggerUrl, port)
                        })
                        .ToArray();
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        throw new InvalidOperationException("Failed to discover DevTools targets from the forwarded WebView endpoint.");
    }

    private static string NormalizeForwardedWebSocketDebuggerUrl(string webSocketDebuggerUrl, int localPort)
    {
        if (string.IsNullOrWhiteSpace(webSocketDebuggerUrl))
        {
            return webSocketDebuggerUrl;
        }

        Uri uri = new(webSocketDebuggerUrl, UriKind.Absolute);
        if (uri.Scheme is not ("ws" or "wss"))
        {
            return webSocketDebuggerUrl;
        }

        UriBuilder builder = new(uri)
        {
            Scheme = "ws",
            Host = "127.0.0.1",
            Port = localPort
        };

        return builder.Uri.ToString();
    }

    private static WebViewSocketInfo? ParseSocketLine(string line)
    {
        int atIndex = line.IndexOf('@');
        if (atIndex < 0)
        {
            return null;
        }

        string socketName = line[(atIndex + 1)..].Trim();
        if (!socketName.Contains("webview_devtools_remote", StringComparison.Ordinal))
        {
            return null;
        }

        int lastUnderscoreIndex = socketName.LastIndexOf('_');
        string processPart = lastUnderscoreIndex >= 0 ? socketName[(lastUnderscoreIndex + 1)..] : string.Empty;
        _ = int.TryParse(processPart, out int pid);

        return new WebViewSocketInfo(socketName, pid);
    }

    private async Task<CommandResult> RunAdbAsync(string arguments, CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = AdbPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start adb from '{AdbPath}'.", ex);
        }

        Task<string> readOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> readError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new CommandResult(process.ExitCode, await readOutput, await readError);
    }

    private static string Quote(string value) => value.Contains(' ') ? $"\"{value}\"" : value;

    private static string ResolveAdbExecutablePath()
    {
        foreach (string projectRoot in EnumerateCandidateRoots())
        {
            string bundledAdbPath = Path.Combine(projectRoot, ".tools", "platform-tools", "adb.exe");
            if (File.Exists(bundledAdbPath))
            {
                return bundledAdbPath;
            }
        }

        return "adb";
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static void EnsureSuccess(CommandResult result, string message)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        string detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        throw new InvalidOperationException($"{message} adb exited with code {result.ExitCode}. {detail.Trim()}");
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record ForwardedPort(string DeviceSerial, int LocalPort);

    public void Dispose()
    {
        try
        {
            RemoveTrackedForwardsAsync().GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
        }

        _httpClient.Dispose();
    }
}

public sealed record AdbDeviceInfo(
    string Serial,
    string State,
    string? Product,
    string? Model,
    string? Device,
    string? TransportId,
    string? Usb,
    string? Features,
    IReadOnlyDictionary<string, string> Properties)
{
    public bool IsOnline => string.Equals(State, "device", StringComparison.OrdinalIgnoreCase);
    public bool IsOffline => string.Equals(State, "offline", StringComparison.OrdinalIgnoreCase);
    public bool IsUnauthorized => string.Equals(State, "unauthorized", StringComparison.OrdinalIgnoreCase);
}

public sealed record WebViewSocketInfo(string SocketName, int ProcessId);
