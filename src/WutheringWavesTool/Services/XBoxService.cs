using System.Numerics;
using System.Runtime.InteropServices;
using Haiyu.ServiceHost;
using Haiyu.ServiceHost.XBox;
using Haiyu.ServiceHost.XBox.Commons;
using Haiyu.ServiceHost.XBox.helpers;
using Microsoft.Extensions.Hosting;
using Waves.Api.Models;
using Waves.Core.Settings;

namespace Haiyu.Services;

public class XBoxService
{
    public XBoxController Controller { get; private set; }

    public XBoxConfig Config { get; }
    public ITipShow TipShow { get; }

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

    public XBoxService(XBoxController xBoxController,XBoxConfig xBoxConfig,ITipShow tipShow)
    {
        Controller = xBoxController;
        Config = xBoxConfig;
        TipShow = tipShow;
    }


    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        Controller = new XBoxController();
        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task StopAsync()
    {
        try
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_pollTask != null)
                {
                    await Task.WhenAny(_pollTask, Task.Delay(3000, _cts.Token));
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
                if (Config.IsEnable == false)
                {
                    await Task.Delay(3000);
                    continue;
                }
                if (Controller != null)
                {
                    // 按键控制模拟
                    var leftClick = Controller.LeftThumbclick.Value;
                    var rightClick = Controller.LeftThumbclick.Value;
                    if(leftClick && rightClick)
                    {
                        this.Controller.BoxTrigger = !this.Controller.BoxTrigger;
                    }
                    if (!this.Controller.BoxTrigger)
                    {
                        await Task.Delay(10);
                        continue;
                    }
                    Vector2 left = Controller.LeftThumbstick.Value;
                    if (Math.Abs(left.X) > LeftThumbDeadZone || Math.Abs(left.Y) > LeftThumbDeadZone)
                    {
                        int dx = (int)(left.X * MouseSensitivity);
                        int dy = (int)(-left.Y * MouseSensitivity);
                        if (dx != 0 || dy != 0)
                        {
                            RealKey.SendMouseMove(dx, dy);
                        }
                    }
                    Vector2 right = Controller.RightThumbstick.Value;
                    if (Math.Abs(right.Y) > RightThumbDeadZone)
                    {
                        int wheel = (int)(right.Y * ScrollSensitivity * RealKey.WHEEL_DELTA);
                        if (wheel != 0)
                        {
                            RealKey.SendMouseWheel(wheel);
                        }
                    }
                    bool xState = Controller.X.Value;
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
                    bool bState = Controller.B.Value;
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