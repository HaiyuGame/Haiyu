using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Api.Models.Rpc;
using Waves.Core.GameContext;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;

namespace Haiyu.BackendService;

public class BackendGameContextService
{
    private static readonly IReadOnlyList<string> ContextKeys =
    [
        nameof(WavesMainGameContext),
        nameof(WavesGlobalGameContext),
        nameof(WavesBiliBiliGameContext),
        nameof(PunishMainGameContext),
        nameof(PunishBiliBiliGameContext),
        nameof(PunishGlobalGameContext),
        nameof(PunishTwGameContext)
    ];

    private readonly IServiceProvider _serviceProvider;
    private readonly BackendEventSink _eventSink;
    private readonly ConcurrentDictionary<string, Task> _initializationTasks = new();
    private readonly ConcurrentDictionary<string, bool> _eventSubscriptions = new();

    public BackendGameContextService(IServiceProvider serviceProvider, BackendEventSink eventSink)
    {
        _serviceProvider = serviceProvider;
        _eventSink = eventSink;
    }

    public IReadOnlyList<string> GetContextKeys() => ContextKeys;

    public async Task<GameContextStatus> GetStatusAsync(string contextKey, CancellationToken token = default)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.GetGameContextStatusAsync(token).ConfigureAwait(false);
    }

    public async Task<GameContextConfig> ReadConfigAsync(string contextKey, CancellationToken token = default)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.ReadContextConfigAsync(token).ConfigureAwait(false);
    }

    public async Task<GameLauncherSource?> GetLauncherSourceAsync(string contextKey, CancellationToken token = default)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.GetGameLauncherSourceAsync(token: token).ConfigureAwait(false);
    }

    public async Task StartDownloadAsync(string contextKey, string folder, GameLauncherSource? source, bool isDelete = false)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        await context.StartDownloadTaskAsync(folder, source, isDelete).ConfigureAwait(false);
    }

    public async Task<bool> PauseDownloadAsync(string contextKey)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.PauseDownloadAsync().ConfigureAwait(false);
    }

    public async Task<bool> ResumeDownloadAsync(string contextKey)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.ResumeDownloadAsync().ConfigureAwait(false);
    }

    public async Task<bool> StopDownloadAsync(string contextKey)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.StopDownloadAsync().ConfigureAwait(false);
    }

    public async Task<LIndex?> GetDefaultLauncherAsync(string contextKey, CancellationToken token = default)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.GetDefaultLauncherValue(token).ConfigureAwait(false);
    }

    public async Task<LauncherBackgroundData?> GetLauncherBackgroundAsync(string contextKey, string backgroundCode, CancellationToken token = default)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.GetLauncherBackgroundDataAsync(backgroundCode, token).ConfigureAwait(false);
    }

    public async Task SetSpeedLimitAsync(string contextKey, long bytesPerSecond)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        await context.SetSpeedLimitAsync(bytesPerSecond).ConfigureAwait(false);
    }

    public async Task<bool> StartGameAsync(string contextKey)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.StartGameAsync().ConfigureAwait(false);
    }

    public async Task StopGameAsync(string contextKey)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        await context.StopGameAsync().ConfigureAwait(false);
    }

    public async Task<IndexGameResource?> GetGameResourceAsync(string contextKey, ResourceDefault resourceDefault, CancellationToken token = default)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.GetGameResourceAsync(resourceDefault, token).ConfigureAwait(false);
    }

    public async Task<PatchIndexGameResource?> GetPatchGameResourceAsync(string contextKey, string url, CancellationToken token = default)
    {
        var context = await GetContextAsync(contextKey).ConfigureAwait(false);
        return await context.GetPatchGameResourceAsync(url, token).ConfigureAwait(false);
    }

    private async Task<IGameContext> GetContextAsync(string contextKey)
    {
        if (!ContextKeys.Contains(contextKey))
        {
            throw new RpcException(404, false, $"Unknown game context '{contextKey}'.");
        }

        var context = _serviceProvider.GetRequiredKeyedService<IGameContext>(contextKey);
        await _initializationTasks.GetOrAdd(contextKey, _ => context.InitAsync()).ConfigureAwait(false);
        EnsureEventSubscriptions(contextKey, context);
        return context;
    }

    private void EnsureEventSubscriptions(string contextKey, IGameContext context)
    {
        if (_eventSubscriptions.TryAdd(contextKey, true))
        {
            context.GameContextOutput += async (_, args) =>
            {
                EnqueueEvent("GameContextOutput", contextKey, args);
                await Task.CompletedTask;
            };

            context.GameContextProdOutput += async (_, args) =>
            {
                EnqueueEvent("GameContextProdOutput", contextKey, args);
                await Task.CompletedTask;
            };
        }
    }

    private void EnqueueEvent(string eventType, string contextKey, GameContextOutputArgs args)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ContextKey = contextKey,
            Args = args
        });

        _eventSink.Enqueue(eventType, payload);
    }
}
