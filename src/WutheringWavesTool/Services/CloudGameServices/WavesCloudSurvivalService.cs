using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Hosting;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts.CloudGame;

namespace Haiyu.Services.CloudGameServices;

/// <summary>
/// 云游戏账号保活机制
/// </summary>
public partial class WavesCloudSurvivalService : ObservableRecipient, IHostedService
{
    public IWavesCloudGameService WavesCloudGameService { get; }
    CancellationTokenSource? _cts;
    System.Threading.PeriodicTimer? timer;

    public WavesCloudSurvivalService(IWavesCloudGameService wavesCloudGameService)
    {
        WavesCloudGameService = wavesCloudGameService;
    }

    public async Task RefreshTaskAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }
        if (timer != null)
        {
            timer.Dispose();
        }
        var users = await WavesCloudGameService.ConfigManager.GetUsersAsync();
        timer = new System.Threading.PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = Task.Run(() => StartTask(users));
    }

    private async Task StartTask(IEnumerable<LoginData> data)
    {
        while (await timer!.WaitForNextTickAsync(_cts.Token))
        {
            if (_cts.IsCancellationRequested)
            {
                break;
            }
            await Parallel.ForEachAsync(
                data,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 2,
                    CancellationToken = _cts.Token,
                },
                async (user, token) => {

                }
            );
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshTaskAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
