using System.Numerics;
using System.Runtime.InteropServices;
using Haiyu.ServiceHost.XBox.Commons;
using Microsoft.Extensions.Hosting;

namespace Haiyu.ServiceHost;

public class XBoxService : IHostedService
{
    private XBoxController? _controller;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    // 可调参数：根据需要调整灵敏度和死区
    private const float LeftThumbDeadZone = 0.15f;
    private const int MouseSensitivity = 18; // 每个循环的像素移动放大倍数
    private const float RightThumbDeadZone = 0.2f;
    private const float ScrollSensitivity = 1.0f; // 乘以标准 WHEEL_DELTA(120)
    private const int PollIntervalMs = 12;

    // 按键状态用于避免重复发送按下事件
    private bool _xPressed;
    private bool _bPressed;

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
                if (_controller != null)
                {
                    Vector2 left = _controller.LeftThumbstick.Value;
                    if (Math.Abs(left.X) > LeftThumbDeadZone || Math.Abs(left.Y) > LeftThumbDeadZone)
                    {
                        int dx = (int)(left.X * MouseSensitivity);
                        int dy = (int)(-left.Y * MouseSensitivity);
                        if (dx != 0 || dy != 0)
                        {
                            SendMouseMove(dx, dy);
                        }
                    }

                    Vector2 right = _controller.RightThumbstick.Value;
                    if (Math.Abs(right.Y) > RightThumbDeadZone)
                    {
                        int wheel = (int)(right.Y * ScrollSensitivity * WHEEL_DELTA);
                        if (wheel != 0)
                        {
                            SendMouseWheel(wheel);
                        }
                    }
                    // X 按键 -> 鼠标左键
                    bool xState = _controller.X.Value;
                    if (xState && !_xPressed)
                    {
                        SendMouseLeftDown();
                        _xPressed = true;
                    }
                    else if (!xState && _xPressed)
                    {
                        SendMouseLeftUp();
                        _xPressed = false;
                    }
                    // B 按键 -> 鼠标右键
                    bool bState = _controller.B.Value;
                    if (bState && !_bPressed)
                    {
                        SendMouseRightDown();
                        _bPressed = true;
                    }
                    else if (!bState && _bPressed)
                    {
                        SendMouseRightUp();
                        _bPressed = false;
                    }
                }

                await Task.Delay(PollIntervalMs, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* 忽略运行时错误，防止服务崩溃 */ }
    }

    #region Win32 SendInput PInvoke

    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_ESCAPE = 0x1B;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const int WHEEL_DELTA = 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public INPUTUNION U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

    private static void SendMouseMove(int dx, int dy)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_MOVE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseWheel(int wheelDelta)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = (uint)wheelDelta,
                    dwFlags = MOUSEEVENTF_WHEEL,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseLeftDown()
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_LEFTDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseLeftUp()
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_LEFTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseRightDown()
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_RIGHTDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseRightUp()
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_RIGHTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyDown(ushort vk)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyUp(ushort vk)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    #endregion
}