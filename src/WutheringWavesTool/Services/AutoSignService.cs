using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Core.Services;
using Waves.Core.Settings;

public sealed class AutoSignService : IHostedService
{
    PeriodicTimer? timer = null;
    CancellationTokenSource? _cts = null;

    private DateTime _nextExecuteTime;

    public AutoSignService() { }

    public AutoSignService(
        ITipShow tipShow,
        [FromKeyedServices("AppLog")] LoggerService loggerService,
        IKuroClient wavesClient,
        AppSettings appSettings,
        IKuroAccountService kuroAccountService
    )
    {
        TipShow = tipShow;
        LoggerService = loggerService;
        SignKuroClient = wavesClient;
        AppSettings = appSettings;
        KuroAccountService = kuroAccountService;
        _nextExecuteTime = GetNext2AmTargetTime();
    }

    public ITipShow TipShow { get; }
    public LoggerService LoggerService { get; }
    public IKuroClient SignKuroClient { get; }
    public AppSettings AppSettings { get; }
    public IKuroAccountService KuroAccountService { get; }

    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            if (AppSettings.AutoSignCommunity == false)
                return;
            int successCount = 0;
            int errorCount = 0;
            var accounts = await KuroAccountService.GetUsersAsync();
            foreach (var account in accounts)
            {
                SignKuroClient.AccountService.SetCurrentUser(account,false);
                var wavesGamers = await SignKuroClient.GetGamerAsync(
                    Waves.Core.Models.Enums.GameType.Waves,
                    token
                );
                var punish = await SignKuroClient.GetGamerAsync(
                    Waves.Core.Models.Enums.GameType.Punish,
                    token
                );
                var items = wavesGamers.Data.Concat(punish.Data);
                foreach (var item in items)
                {
                    var sign = await SignKuroClient.SignInAsync(item, token);
                    if (!sign.Success)
                        continue;
                    if (sign.Code == 1511)
                    {
                        successCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }
            }
            await TipShow.ShowMessageAsync(
                $"签到结果{successCount}个成功，总数{successCount + errorCount}",
                Symbol.Bookmarks
            );
            await SignKuroClient.AccountService.SetAutoUser();
        }
        catch (Exception ex)
        {
            LoggerService.WriteError(ex.Message);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await Task.Run(
            async () =>
            {
                try
                {
                    await RunAsync(_cts.Token);
                    LoggerService.WriteError("自动签到服务启动成功，已【立即执行一次签到】");
                    _nextExecuteTime = DateTime.Today.AddHours(2).AddDays(1);
                    while (
                        await timer.WaitForNextTickAsync(_cts.Token)
                        && !_cts.Token.IsCancellationRequested
                    )
                    {
                        if (DateTime.Now >= _nextExecuteTime)
                        {
                            await RunAsync(_cts.Token);
                            _nextExecuteTime = GetNext2AmTargetTime();
                            LoggerService.WriteError(
                                $"签到执行完成，下次签到时间更新为：{_nextExecuteTime:yyyy-MM-dd HH:mm:ss}"
                            );
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    LoggerService.WriteError("自动签到服务：正常停止轮询");
                }
                catch (Exception ex)
                {
                    LoggerService.WriteError($"自动签到服务轮询异常：{ex.Message}");
                }
            },
            _cts.Token
        );
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
        timer?.Dispose();
    }

    private DateTime GetNext2AmTargetTime()
    {
        DateTime today2Am = DateTime.Today.AddHours(2);
        return DateTime.Now > today2Am ? today2Am.AddDays(1) : today2Am;
    }
}
