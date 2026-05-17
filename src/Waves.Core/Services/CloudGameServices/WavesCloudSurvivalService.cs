using System;
using System.Collections.Generic;
using System.Text;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.CloudGame;
using Waves.Core.Models.Enums;

namespace Waves.Core.Services.CloudGameServices;

public delegate void CloudSurivivalMessageHandler(object sender, CloudMessageArgs session);

/// <summary>
/// 云游戏账号保活机制
/// </summary>
public partial class WavesCloudSurvivalService:IDisposable,IAsyncDisposable
{
    public IWavesCloudGameService WavesCloudGameService { get; }
    CancellationTokenSource? _cts;
    public WavesCloudUserCache Cache { get; }

    public bool IsRuning
    {
        get => Volatile.Read(ref _isRuning);
    }

    System.Threading.PeriodicTimer? timer = null;
    private volatile bool _isRuning;

    private CloudSurivivalMessageHandler? messageHandler;

    public event CloudSurivivalMessageHandler MessageHandler
    {
        add { messageHandler += value; }
        remove { messageHandler -= value; }
    }

    public WavesCloudSurvivalService(IWavesCloudGameService wavesCloudGameService)
    {
        WavesCloudGameService = wavesCloudGameService;
        Cache = new();
    }

    public async Task RefreshTaskAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
        if (timer != null)
        {
            timer.Dispose();
        }
        _cts = new CancellationTokenSource();
        var users = await WavesCloudGameService.ConfigManager.GetUsersAsync();
        timer = new System.Threading.PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = Task.Run(() => StartTask(users));
    }

    private async Task StartTask(IEnumerable<CloudGameLoginData> data)
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
                async (user, token) => await InvokeTimerTask(user, _cts.Token)
            );
        }
    }

    async Task InvokeTimerTask(CloudGameLoginData data, CancellationToken token = default)
    {
        try
        {
            if (!this.Cache.IsCheck(data))
            {
                var isLogin = await WavesCloudGameService.RefreshPhoneTokenAsync(data, token);
                if (isLogin != null && isLogin.Code == 0)
                {
                    var accessToken = await WavesCloudGameService.GetAccessToken(
                        data,
                        isLogin.Data.Code
                    );
                    if (accessToken == null || accessToken.Code != 0)
                    {
                        Cache.TryRemove(data);
                        await this.WavesCloudGameService.ConfigManager.DeleteUserAsync(
                            data.Sdkuserid
                        );
                        this.messageHandler?.Invoke(this, new(CloudCoreType.DeleteUser));
                        return;
                    }
                    var cacheToken = await WavesCloudGameService.GetTokenAsync(
                        data,
                        accessToken.Data.AccessToken,
                        token
                    );
                    if (cacheToken == null || cacheToken.Code != 0)
                    {
                        Cache.TryRemove(data);
                        await this.WavesCloudGameService.ConfigManager.DeleteUserAsync(
                            data.Sdkuserid
                        );
                        this.messageHandler?.Invoke(this, new(CloudCoreType.DeleteUser));
                        return;
                    }
                    Cache.TryAdd(data, isLogin.Data, accessToken.Data, cacheToken.Data);
                }
                else
                {
                    Cache.TryRemove(data);
                    await this.WavesCloudGameService.ConfigManager.DeleteUserAsync(data.Sdkuserid);
                    this.messageHandler?.Invoke(this, new(CloudCoreType.DeleteUser));
                    return;
                }
            }
            var fetchResult = await this.WavesCloudGameService.FetchMesageAsync(
                this.Cache.TryGet(data)
            );
            if (fetchResult.Code != 0)
            {
                Cache.TryRemove(data);
                await this.WavesCloudGameService.ConfigManager.DeleteUserAsync(data.Sdkuserid);
                this.messageHandler?.Invoke(this, new(CloudCoreType.DeleteUser));
                return;
            }
        }
        catch (Exception ex)  
        {
            this.messageHandler?.Invoke(this, new CloudMessageArgs(CloudCoreType.Message)
            {
                Message = $"异常:{ex.Message}"
            });
        }
    }

    public async Task StartAsync()
    {
        await RefreshTaskAsync();

        _isRuning = true;
    }

    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
        _cts = null;
        _isRuning = false;
    }

    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts?.Dispose();
        }
        timer?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if(_cts != null)
        {
            await _cts.CancelAsync();
            _cts?.Dispose();
        }
        timer?.Dispose();
    }
}
