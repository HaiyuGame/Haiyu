using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Common;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.Models;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

partial class KuroGameContextBase
{
    private bool IsDownloadCanceled()
    {
        return IsDownloadCanceled(false);
    }

    private bool IsDownloadCanceled(bool isPred)
    {
        var cts = isPred ? _prodDownloadCTS : _downloadCTS;
        var state = isPred ? _prodDownloadState : _downloadState;
        return cts == null || cts.IsCancellationRequested || state?.IsStop == true;
    }

    private CancellationTokenSource GetCTS(bool isPred)
    {
        return isPred ? (_prodDownloadCTS ?? _downloadCTS) : _downloadCTS;
    }

    private DownloadState GetState(bool isPred)
    {
        return isPred ? (_prodDownloadState ?? _downloadState) : _downloadState;
    }

    public int GetVerifyLimit()
    {
        if (
            this.ContextName == nameof(PunishMainGameContext)
            || this.ContextName == nameof(PunishGlobalGameContext)
            || this.ContextName == nameof(PunishTwGameContext)
        )
        {
            return 200;
        }
        else
        {
            return 2000;
        }
    }

    /// <summary>
    /// 重新推送最后一次的下载/提示信息（用于页面切换后恢复显示）
    /// </summary>
    public async Task ReEmitLastOutputAsync(bool isPred = false)
    {
        if (isPred)
        {
            if (_lastProdOutputArgs == null)
                return;
            if (gameContextProdOutputDelegate != null)
            {
                await gameContextProdOutputDelegate.Invoke(this, _lastProdOutputArgs).ConfigureAwait(false);
            }
        }
        else
        {
            if (_lastOutputArgs == null)
                return;
            if (gameContextOutputDelegate != null)
            {
                await gameContextOutputDelegate.Invoke(this, _lastOutputArgs).ConfigureAwait(false);
            }
        }
    }


    private async Task UpdateFileProgress(
        GameContextActionType type,
        long fileSize,
        bool isAdd = true,
        bool isPred = false,
        string tip = ""
    )
    {
        if (type == GameContextActionType.Download)
        {
            Interlocked.Add(ref _totalDownloadedBytes, fileSize);
            if (isAdd)
                Interlocked.Add(ref _totalProgressSize, fileSize);
        }
        else if (type == GameContextActionType.Verify)
        {
            if (!isAdd)
                Interlocked.Add(ref _totalVerifiedBytes, fileSize);
            if (isAdd)
                Interlocked.Add(ref _totalProgressSize, fileSize);
        }
        var elapsed = (DateTime.Now - _lastSpeedUpdateTime).TotalSeconds;
        if (elapsed >= 1)
        {
            _downloadSpeed = _totalDownloadedBytes / elapsed;
            _verifySpeed = _totalVerifiedBytes / elapsed;
            Interlocked.Exchange(ref _totalDownloadedBytes, 0);
            Interlocked.Exchange(ref _totalVerifiedBytes, 0);
            var currentBytes = Interlocked.Read(ref _totalDownloadedBytes);
            _lastSpeedBytes = currentBytes;
            _lastSpeedUpdateTime = DateTime.Now;
        }

        var args = new GameContextOutputArgs
        {
            Type = type,
            CurrentSize = _totalProgressSize,
            TotalSize = _totalfileSize,
            DownloadSpeed = _downloadSpeed,
            VerifySpeed = _verifySpeed,
            RemainingTime = RemainingTime,
            IsAction = _downloadState?.IsActive ?? false,
            IsPause = _downloadState?.IsPaused ?? false,
            TipMessage = tip,
        };
        if (isPred)
            _lastProdOutputArgs = args;
        else
            _lastOutputArgs = args;

        if (isPred && gameContextProdOutputDelegate != null)
        {
            await gameContextProdOutputDelegate.Invoke(this, args).ConfigureAwait(false);
        }
        else if (!isPred && gameContextOutputDelegate != null)
        {
            await gameContextOutputDelegate.Invoke(this, args).ConfigureAwait(false);
        }
    }
}
