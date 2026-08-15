namespace Waves.Core.Services.CloudGameServices;

partial class WavesCloudGameService
{
    private CancellationTokenSource? _taskCTS;
    private Task? _loopTask;
    private CloudGameLoginData? _currentData;

    private PhoneTokenData? TokenData { get; set; }
    private AccessData? AccessData { get; set; }
    private EndLoginData? EndLoginData { get; set; }
    private string? TrackerId { get; set; }

    public async Task<CloudGameLoginSession?> GetCurrentUserSession()
    {
        var cancellationToken = _taskCTS?.Token ?? CancellationToken.None;
        if (!HasCurrentSession && !await LoginCurrentAccountAsync(cancellationToken))
        {
            return null;
        }

        return GetSession();
    }

    public async Task<bool> SetCurrentUserSession(string userId)
    {
        var user = await ConfigManager.GetUserAsync(userId);
        if (user is null)
        {
            return false;
        }
        var currentLogin = await this.GetCurrentUserSession();
        if(currentLogin != null && currentLogin.GetId() == userId)
        {
            return false;
        }
        await StopSessionLoopAsync();
        await ClearSession();

        _currentData = user;
        _taskCTS = new CancellationTokenSource();

        if (!await LoginCurrentAccountAsync(_taskCTS.Token))
        {
            await StopSessionLoopAsync();
            await ClearSession();
            return false;
        }

        _loopTask = RunLoopAsync(_taskCTS.Token);
        return true;
    }

    private bool HasCurrentSession =>
        _currentData is not null
        && TokenData is not null
        && AccessData is not null
        && EndLoginData is not null;

    private CloudGameLoginSession GetSession()
    {
        if (!HasCurrentSession)
        {
            throw new InvalidOperationException("当前云游戏账号的登录会话尚未建立。");
        }

        return new CloudGameLoginSession
        {
            OrginData = _currentData!,
            PhoneToken = TokenData!,
            AccessData = AccessData!,
            EndLoginData = EndLoginData!,
            TraceId = TrackerId ?? string.Empty,
            SaveTime = DateTime.Now,
        };
    }

    private async Task<bool> LoginCurrentAccountAsync(CancellationToken cancellationToken)
    {
        if (_currentData is null)
        {
            return false;
        }
        
        var phoneResult = await RefreshPhoneTokenAsync(_currentData, cancellationToken);
        if (phoneResult?.Code != 0 || phoneResult.Data is null)
        {
            return false;
        }

        var accessResult = await GetAccessToken(
            _currentData,
            phoneResult.Data.Code,
            cancellationToken
        );
        if (accessResult?.Code != 0 || accessResult.Data is null)
        {
            return false;
        }

        var tokenResult = await GetTokenAsync(
            _currentData,
            accessResult.Data.AccessToken,
            cancellationToken
        );
        if (tokenResult?.Code != 0 || tokenResult.Data is null)
        {
            return false;
        }

        TokenData = phoneResult.Data;
        AccessData = accessResult.Data;
        EndLoginData = tokenResult.Data;
        return true;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!HasCurrentSession)
                {
                    continue;
                }

                await FetchMesageAsync(GetSession(), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task StopSessionLoopAsync()
    {
        var cancellation = _taskCTS;
        var loopTask = _loopTask;

        _taskCTS = null;
        _loopTask = null;

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        if (loopTask is not null)
        {
            try
            {
                await loopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation.Dispose();
    }

    private async Task ClearSession()
    {
        if(_currentData != null)
            await this.ConfigManager.DeleteUserAsync(_currentData.GetId());
        TokenData = null;
        AccessData = null;
        EndLoginData = null;
        TrackerId = null;
        _currentData = null;
    }

    public async Task DeleteUserAsync(string id)
    {
        if(_currentData != null && _currentData.GetId() == id)
        {
            await StopSessionLoopAsync();
        }
        this._currentData = null;
        this.TokenData = null;
        this.AccessData = null;
        this.EndLoginData = null;
        await this.ConfigManager.DeleteUserAsync(id);
    }
}
