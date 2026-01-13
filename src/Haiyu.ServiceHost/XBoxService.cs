using System.Numerics;
using System.Runtime.InteropServices;
using Haiyu.ServiceHost.XBox;
using Haiyu.ServiceHost.XBox.Commons;
using Haiyu.ServiceHost.XBox.helpers;
using Microsoft.Extensions.Hosting;
using Waves.Api.Models;
using Waves.Core.Settings;

namespace Haiyu.ServiceHost;

public class XBoxService : IHostedService
{
    private XBoxController _controller;

    public XBoxConfig Config { get; }

    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    /// <summary> 触发信号 </summary>
    private const float LeftThumbDeadZone = 0.15f;
    private const int MouseSensitivity = 18;
    private const float RightThumbDeadZone = 0.2f;
    private const float ScrollSensitivity = 1.0f;
    private const int PollIntervalMs = 12;

    private bool _xPressed;
    private bool _bPressed;

    public XBoxService(XBoxController xBoxController,XBoxConfig xBoxConfig)
    {
        _controller = xBoxController;
        Config = xBoxConfig;
    }


    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _controller = new XBoxController();
        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_pollTask != null)
                {
                    await Task.WhenAny(_pollTask, Task.Delay(3000, cancellationToken));
                }
                _cts.Dispose();
                _cts = null;
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                //if(Config.IsEnable == false)
                //{
                //    await Task.Delay(10000);
                //    continue;
                //}
                if (_controller != null)
                {
                    // 按键控制模拟
                    var leftClick = _controller.LeftThumbclick.Value;
                    var rightClick = _controller.LeftThumbclick.Value;
                    if(leftClick && rightClick)
                    {
                        this._controller.BoxTrigger = !this._controller.BoxTrigger;
                    }
                    if (!this._controller.BoxTrigger)
                    {
                        await Task.Delay(10);
                        continue;
                    }
                    Vector2 left = _controller.LeftThumbstick.Value;
                    if (Math.Abs(left.X) > LeftThumbDeadZone || Math.Abs(left.Y) > LeftThumbDeadZone)
                    {
                        int dx = (int)(left.X * MouseSensitivity);
                        int dy = (int)(-left.Y * MouseSensitivity);
                        if (dx != 0 || dy != 0)
                        {
                            RealKey.SendMouseMove(dx, dy);
                        }
                    }
                    Vector2 right = _controller.RightThumbstick.Value;
                    if (Math.Abs(right.Y) > RightThumbDeadZone)
                    {
                        int wheel = (int)(right.Y * ScrollSensitivity * RealKey.WHEEL_DELTA);
                        if (wheel != 0)
                        {
                            RealKey.SendMouseWheel(wheel);
                        }
                    }
                    bool xState = _controller.X.Value;
                    if (xState && !_xPressed)
                    {
                        RealKey.SendMouseLeftDown();
                        _xPressed = true;
                    }
                    else if (!xState && _xPressed)
                    {
                        RealKey.SendMouseLeftUp();
                        _xPressed = false;
                    }
                    bool bState = _controller.B.Value;
                    if (bState && !_bPressed)
                    {
                        RealKey.SendMouseRightDown();
                        _bPressed = true;
                    }
                    else if (!bState && _bPressed)
                    {
                        RealKey.SendMouseRightUp();
                        _bPressed = false;
                    }
                }

                await Task.Delay(PollIntervalMs, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) {}
    }

}