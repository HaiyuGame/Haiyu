using System.Diagnostics;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.Rpc;

namespace Haiyu.BackendService;

public class BackendRpcMethods
{
    private readonly BackendEventSink _eventSink;
    private readonly BackendGameContextService _gameContextService;

    public BackendRpcMethods(BackendEventSink eventSink, BackendGameContextService gameContextService)
    {
        _eventSink = eventSink;
        _gameContextService = gameContextService;
    }

    public Dictionary<string, Func<string, List<RpcParams>, Task<string>>> CreateMethods() =>
        new()
        {
            { RpcMethod.BackendPing.ToRpcName(), PingAsync },
            { RpcMethod.BackendPollEvents.ToRpcName(), PollEventsAsync },
            { RpcMethod.BackendLaunchProcess.ToRpcName(), LaunchProcessAsync },
            { RpcMethod.GameContextList.ToRpcName(), ListGameContextsAsync },
            { RpcMethod.GameContextGetStatus.ToRpcName(), GetGameContextStatusAsync },
            { RpcMethod.GameContextGetDefaultLauncher.ToRpcName(), GetDefaultLauncherAsync },
            { RpcMethod.GameContextGetBackground.ToRpcName(), GetLauncherBackgroundAsync },
            { RpcMethod.GameContextGetLauncherSource.ToRpcName(), GetLauncherSourceAsync },
            { RpcMethod.GameContextReadConfig.ToRpcName(), ReadContextConfigAsync },
            { RpcMethod.GameContextStartDownload.ToRpcName(), StartDownloadAsync },
            { RpcMethod.GameContextPauseDownload.ToRpcName(), PauseDownloadAsync },
            { RpcMethod.GameContextResumeDownload.ToRpcName(), ResumeDownloadAsync },
            { RpcMethod.GameContextStopDownload.ToRpcName(), StopDownloadAsync },
            { RpcMethod.GameContextSetSpeedLimit.ToRpcName(), SetSpeedLimitAsync },
            { RpcMethod.GameContextStartGame.ToRpcName(), StartGameAsync },
            { RpcMethod.GameContextStopGame.ToRpcName(), StopGameAsync }
        };

    private Task<string> PingAsync(string method, List<RpcParams> rpcParams)
    {
        return Task.FromResult("ok");
    }

    private Task<string> PollEventsAsync(string method, List<RpcParams> rpcParams)
    {
        var events = _eventSink.DequeueAll();
        var payload = JsonSerializer.Serialize(events, BackendJsonContext.Default.ListBackendEvent);
        return Task.FromResult(payload);
    }

    private Task<string> LaunchProcessAsync(string method, List<RpcParams> rpcParams)
    {
        if (!TryGetValue(rpcParams, "path", out var path) || string.IsNullOrWhiteSpace(path))
        {
            throw new RpcException(400, false, "Missing required 'path' parameter.");
        }

        TryGetValue(rpcParams, "args", out var args);

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = args ?? string.Empty,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        _eventSink.Enqueue("ProcessLaunched", path);

        return Task.FromResult("started");
    }

    private Task<string> ListGameContextsAsync(string method, List<RpcParams> rpcParams)
    {
        var contexts = _gameContextService.GetContextKeys();
        return Task.FromResult(JsonSerializer.Serialize(contexts, BackendJsonContext.Default.ListString));
    }

    private async Task<string> GetGameContextStatusAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var status = await _gameContextService.GetStatusAsync(contextKey).ConfigureAwait(false);
        return JsonSerializer.Serialize(status, BackendJsonContext.Default.GameContextStatus);
    }

    private async Task<string> GetDefaultLauncherAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var result = await _gameContextService.GetDefaultLauncherAsync(contextKey).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, BackendJsonContext.Default.LIndex);
    }

    private async Task<string> GetLauncherBackgroundAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var backgroundCode = GetRequiredValue(rpcParams, "backgroundCode");
        var result = await _gameContextService.GetLauncherBackgroundAsync(contextKey, backgroundCode).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, BackendJsonContext.Default.LauncherBackgroundData);
    }

    private async Task<string> GetLauncherSourceAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var result = await _gameContextService.GetLauncherSourceAsync(contextKey).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, GameLauncherSourceContext.Default.GameLauncherSource);
    }

    private async Task<string> ReadContextConfigAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var result = await _gameContextService.ReadConfigAsync(contextKey).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, BackendJsonContext.Default.GameContextConfig);
    }

    private async Task<string> StartDownloadAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var folder = GetRequiredValue(rpcParams, "folder");
        var sourceJson = GetRequiredValue(rpcParams, "sourceJson");
        var isDeleteValue = TryGetValue(rpcParams, "isDelete", out var isDeleteRaw) ? isDeleteRaw : null;
        var isDelete = bool.TryParse(isDeleteValue, out var deleteFlag) && deleteFlag;

        var source = JsonSerializer.Deserialize<GameLauncherSource>(
            sourceJson,
            BackendJsonContext.Default.GameLauncherSource);
        await _gameContextService.StartDownloadAsync(contextKey, folder, source, isDelete).ConfigureAwait(false);
        return "ok";
    }

    private async Task<string> PauseDownloadAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var result = await _gameContextService.PauseDownloadAsync(contextKey).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, BackendJsonContext.Default.Boolean);
    }

    private async Task<string> ResumeDownloadAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var result = await _gameContextService.ResumeDownloadAsync(contextKey).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, BackendJsonContext.Default.Boolean);
    }

    private async Task<string> StopDownloadAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var result = await _gameContextService.StopDownloadAsync(contextKey).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, BackendJsonContext.Default.Boolean);
    }

    private async Task<string> SetSpeedLimitAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var bytesPerSecondValue = GetRequiredValue(rpcParams, "bytesPerSecond");
        if (!long.TryParse(bytesPerSecondValue, out var bytesPerSecond))
        {
            throw new RpcException(400, false, "Invalid 'bytesPerSecond' value.");
        }

        await _gameContextService.SetSpeedLimitAsync(contextKey, bytesPerSecond).ConfigureAwait(false);
        return "ok";
    }

    private async Task<string> StartGameAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        var result = await _gameContextService.StartGameAsync(contextKey).ConfigureAwait(false);
        return JsonSerializer.Serialize(result, BackendJsonContext.Default.Boolean);
    }

    private async Task<string> StopGameAsync(string method, List<RpcParams> rpcParams)
    {
        var contextKey = GetRequiredValue(rpcParams, "contextKey");
        await _gameContextService.StopGameAsync(contextKey).ConfigureAwait(false);
        return "ok";
    }

    private static bool TryGetValue(IEnumerable<RpcParams> parameters, string key, out string? value)
    {
        value = parameters.FirstOrDefault(parameter => string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string GetRequiredValue(IEnumerable<RpcParams> parameters, string key)
    {
        if (TryGetValue(parameters, key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new RpcException(400, false, $"Missing required '{key}' parameter.");
    }
}
