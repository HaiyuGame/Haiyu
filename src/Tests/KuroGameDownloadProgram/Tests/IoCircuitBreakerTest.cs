using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.ContextsV2.Punish;
using Waves.Core.Models;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Waves.Settings;

namespace KuroGameDownloadProgram.Tests;

/// <summary>
/// 使用战双帕弥什官服核心的游戏校验流程测试 IO 熔断。
/// </summary>
public static class IoCircuitBreakerTest
{
    public static async Task StartTest(string[]? args = null)
    {
        // 与 Haiyu.AppContext.InitGameCoreAsync 使用相同的核心目录和 keyed DI 初始化方式。
        GameContextFactory.GameBassPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Waves"
        );

        using var host = Host.CreateDefaultBuilder(args ?? [])
            .ConfigureServices(services =>
            {
                services.AddGameContext();
            })
            .Build();

        var settings = host.Services.GetRequiredService<AppSettings>();
        var originalMax = await settings.GetMaxIoConcurrentAsync();
        var context = host.Services.GetRequiredKeyedService<IGameContextV2>(
            nameof(PunishMainGameContextV2)
        );
        var breaker = host.Services.GetRequiredService<IIoCircuitBreaker>();

        await context.InitAsync();
        var gameFolder = await context.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder
        );
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            throw new InvalidOperationException(
                "战双官服游戏目录尚未在 Haiyu 中配置，请先在 Haiyu 中定位一次战双官服目录。"
            );
        }

        var verifyStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var operationFinished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        void OnProgressChanged(GameProgressTracker tracker)
        {
            var action = tracker.LastArgs.Type;
            if (action == GameContextActionType.Verify)
                verifyStarted.TrySetResult();
            if (action == GameContextActionType.None || tracker.IsCancel)
                operationFinished.TrySetResult();
        }

        context.ProgressState.OnProgressChanged += OnProgressChanged;

        try
        {
            await settings.SetMaxIoConcurrentAsync(1);

            Assert(breaker.TryAcquire(), "首个战双校验任务应获取熔断器名额");
            try
            {
                // false 表示保留现有文件，执行核心资源检查/校验流程。
                var started = await context.RepairGameAsync(false, []);
                Assert(started, "战双官服核心校验任务启动失败");

                await verifyStarted.Task.WaitAsync(TimeSpan.FromMinutes(2));
                Console.WriteLine("战双官服核心已进入 Verify 阶段，提交第二个校验任务……");

                var secondTaskAccepted = breaker.TryAcquire();
                if (secondTaskAccepted)
                    breaker.Release();

                Assert(!secondTaskAccepted, "首个校验运行期间，第二个校验任务应立即熔断");
                Console.WriteLine("[PASS] 重复的战双官服校验任务已被熔断");
            }
            finally
            {
                // RepairGameAsync 会启动后台流程，名额必须覆盖真实核心任务生命周期。
                await context.StopCannelTaskAsync();
                try
                {
                    await operationFinished.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    // 核心取消后未再次推送终态时，继续执行资源释放。
                }
                breaker.Release();
            }

            Assert(breaker.TryAcquire(), "校验任务结束后熔断器应恢复可用状态");
            breaker.Release();
            Console.WriteLine("[PASS] 战双官服核心熔断集成测试全部通过");
        }
        finally
        {
            context.ProgressState.OnProgressChanged -= OnProgressChanged;
            await settings.SetMaxIoConcurrentAsync(originalMax);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[FAIL] {message}");
    }
}
