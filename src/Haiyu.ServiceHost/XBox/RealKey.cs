using System.Runtime.InteropServices;

namespace Haiyu.ServiceHost.XBox;

public static class RealKey
{
    #region ========== 1. 输入设备类型常量 (INPUT_*) 【int】- 必选，指定事件类型 ==========
    /// <summary>鼠标输入事件</summary>
    public const int INPUT_MOUSE = 0;
    /// <summary>键盘输入事件</summary>
    public const int INPUT_KEYBOARD = 1;
    /// <summary>硬件输入事件(极少用)</summary>
    public const int INPUT_HARDWARE = 2;

    #endregion ========== 鼠标相关常量 【全部补全】 ==========
    #region 2. 鼠标操作指令掩码 (MOUSEEVENTF_*) 【uint】- 鼠标核心动作，支持 | 按位或组合
    public const uint MOUSEEVENTF_MOVE = 0x0001;          // 鼠标移动
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;      // 鼠标左键 按下
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;        // 鼠标左键 抬起
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;     // 鼠标右键 按下
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;       // 鼠标右键 抬起
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;    // 鼠标中键/滚轮键 按下
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;      // 鼠标中键/滚轮键 抬起
    public const uint MOUSEEVENTF_XDOWN = 0x0080;         // 鼠标侧键(X1/X2) 按下
    public const uint MOUSEEVENTF_XUP = 0x0100;           // 鼠标侧键(X1/X2) 抬起
    public const uint MOUSEEVENTF_WHEEL = 0x0800;         // 鼠标滚轮 垂直滚动(上下)
    public const uint MOUSEEVENTF_HWHEEL = 0x1000;        // 鼠标滚轮 水平滚动(左右)
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;      // 鼠标绝对坐标模式(必加，精准移动)
    public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;   // 多显示器全屏坐标支持

    #endregion
    #region 3. 鼠标专用辅助常量 【int】
    /// <summary>鼠标滚轮标准滚动单位，固定值，向上=+120，向下=-120，向左=-120，向右=+120</summary>
    public const int WHEEL_DELTA = 120;
    /// <summary>鼠标侧键X1键标识(搭配XDown/XUp使用)</summary>
    public const int XBUTTON1 = 0x0001;
    /// <summary>鼠标侧键X2键标识(搭配XDown/XUp使用)</summary>
    public const int XBUTTON2 = 0x0002;

    #endregion ========== 键盘相关常量 【全部补全】 ==========
    #region 4. 键盘事件状态掩码 (KEYEVENTF_*) 【uint】- 键盘按键状态，支持 | 按位或组合
    /// <summary>键盘按键 抬起（默认是按下，加此值为抬起）</summary>
    public const uint KEYEVENTF_KEYUP = 0x0002;
    /// <summary>扩展键标识(如Alt/Ctrl/Win键需要搭配)</summary>
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    /// <summary>扫描码模式(部分特殊键需要)</summary>
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    #endregion
    #region 5. 键盘虚拟按键码 (VK_*) 【ushort】- 键盘所有按键的唯一标识，完整全集
    // ========== 基础控制键 ==========
    public const ushort VK_BACK = 0x08;        // 退格键 (Backspace)
    public const ushort VK_TAB = 0x09;         // Tab键
    public const ushort VK_CLEAR = 0x0C;       // Clear键
    public const ushort VK_RETURN = 0x0D;      // 回车键 (Enter)
    public const ushort VK_SHIFT = 0x10;       // Shift键
    public const ushort VK_CONTROL = 0x11;     // Ctrl键
    public const ushort VK_MENU = 0x12;        // Alt键
    public const ushort VK_PAUSE = 0x13;       // Pause键
    public const ushort VK_CAPITAL = 0x14;     // Caps Lock大小写锁定
    public const ushort VK_ESCAPE = 0x1B;      // ESC键
    public const ushort VK_SPACE = 0x20;       // 空格键
    public const ushort VK_PRIOR = 0x21;       // PageUp键
    public const ushort VK_NEXT = 0x22;        // PageDown键
    public const ushort VK_END = 0x23;         // End键
    public const ushort VK_HOME = 0x24;        // Home键

    // ========== 方向键 ==========
    public const ushort VK_LEFT = 0x25;        // ← 左方向键
    public const ushort VK_UP = 0x26;          // ↑ 上方向键
    public const ushort VK_RIGHT = 0x27;       // → 右方向键
    public const ushort VK_DOWN = 0x28;        // ↓ 下方向键

    // ========== 插入/删除 ==========
    public const ushort VK_INSERT = 0x2D;      // Insert键
    public const ushort VK_DELETE = 0x2E;      // Delete键

    // ========== 数字键(主键盘区) ==========
    public const ushort VK_0 = 0x30;
    public const ushort VK_1 = 0x31;
    public const ushort VK_2 = 0x32;
    public const ushort VK_3 = 0x33;
    public const ushort VK_4 = 0x34;
    public const ushort VK_5 = 0x35;
    public const ushort VK_6 = 0x36;
    public const ushort VK_7 = 0x37;
    public const ushort VK_8 = 0x38;
    public const ushort VK_9 = 0x39;

    // ========== 字母键 ==========
    public const ushort VK_A = 0x41;
    public const ushort VK_B = 0x42;
    public const ushort VK_C = 0x43;
    public const ushort VK_D = 0x44;
    public const ushort VK_E = 0x45;
    public const ushort VK_F = 0x46;
    public const ushort VK_G = 0x47;
    public const ushort VK_H = 0x48;
    public const ushort VK_I = 0x49;
    public const ushort VK_J = 0x4A;
    public const ushort VK_K = 0x4B;
    public const ushort VK_L = 0x4C;
    public const ushort VK_M = 0x4D;
    public const ushort VK_N = 0x4E;
    public const ushort VK_O = 0x4F;
    public const ushort VK_P = 0x50;
    public const ushort VK_Q = 0x51;
    public const ushort VK_R = 0x52;
    public const ushort VK_S = 0x53;
    public const ushort VK_T = 0x54;
    public const ushort VK_U = 0x55;
    public const ushort VK_V = 0x56;
    public const ushort VK_W = 0x57;
    public const ushort VK_X = 0x58;
    public const ushort VK_Y = 0x59;
    public const ushort VK_Z = 0x5A;

    // ========== 功能键 F1-F12 ==========
    public const ushort VK_F1 = 0x70;
    public const ushort VK_F2 = 0x71;
    public const ushort VK_F3 = 0x72;
    public const ushort VK_F4 = 0x73;
    public const ushort VK_F5 = 0x74;
    public const ushort VK_F6 = 0x75;
    public const ushort VK_F7 = 0x76;
    public const ushort VK_F8 = 0x77;
    public const ushort VK_F9 = 0x78;
    public const ushort VK_F10 = 0x79;
    public const ushort VK_F11 = 0x7A;
    public const ushort VK_F12 = 0x7B;

    // ========== 小键盘区 ==========
    public const ushort VK_NUMLOCK = 0x90;     // 小键盘锁
    public const ushort VK_NUMPAD0 = 0x60;
    public const ushort VK_NUMPAD1 = 0x61;
    public const ushort VK_NUMPAD2 = 0x62;
    public const ushort VK_NUMPAD3 = 0x63;
    public const ushort VK_NUMPAD4 = 0x64;
    public const ushort VK_NUMPAD5 = 0x65;
    public const ushort VK_NUMPAD6 = 0x66;
    public const ushort VK_NUMPAD7 = 0x67;
    public const ushort VK_NUMPAD8 = 0x68;
    public const ushort VK_NUMPAD9 = 0x69;
    public const ushort VK_MULTIPLY = 0x6A;    // 小键盘 *
    public const ushort VK_ADD = 0x6B;         // 小键盘 +
    public const ushort VK_SUBTRACT = 0x6D;    // 小键盘 -
    public const ushort VK_DECIMAL = 0x6E;     // 小键盘 .
    public const ushort VK_DIVIDE = 0x6F;      // 小键盘 /

    // ========== 特殊键 ==========
    public const ushort VK_LWIN = 0x5B;        // 左Win键
    public const ushort VK_RWIN = 0x5C;        // 右Win键
    public const ushort VK_APPS = 0x5D;        // 右键菜单键
    public const ushort VK_SHIFT_L = 0xA0;     // 左Shift
    public const ushort VK_SHIFT_R = 0xA1;     // 右Shift
    public const ushort VK_CONTROL_L = 0xA2;   // 左Ctrl
    public const ushort VK_CONTROL_R = 0xA3;   // 右Ctrl
    public const ushort VK_MENU_L = 0xA4;      // 左Alt
    public const ushort VK_MENU_R = 0xA5;      // 右Alt
    #endregion


    #region Win32 SendInput PInvoke


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

    public static void SendMouseMove(int dx, int dy)
    {
        var input = new INPUT
        {
            type = RealKey.INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = 0,
                    dwFlags = RealKey.MOUSEEVENTF_MOVE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static void SendMouseWheel(int wheelDelta)
    {
        var input = new INPUT
        {
            type = RealKey.INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = (uint)wheelDelta,
                    dwFlags = RealKey.MOUSEEVENTF_WHEEL,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static void SendMouseLeftDown()
    {
        var input = new INPUT
        {
            type = RealKey.INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = RealKey.MOUSEEVENTF_LEFTDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static void SendMouseLeftUp()
    {
        var input = new INPUT
        {
            type = RealKey.INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = RealKey.MOUSEEVENTF_LEFTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static void SendMouseRightDown()
    {
        var input = new INPUT
        {
            type = RealKey.INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = RealKey.MOUSEEVENTF_RIGHTDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static void SendMouseRightUp()
    {
        var input = new INPUT
        {
            type = RealKey.INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = RealKey.MOUSEEVENTF_RIGHTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static void SendKeyDown(ushort vk)
    {
        var input = new INPUT
        {
            type = RealKey.INPUT_KEYBOARD,
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

    public static void SendKeyUp(ushort vk)
    {
        var input = new INPUT
        {
            type = RealKey.INPUT_KEYBOARD,
            U = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = RealKey.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    #endregion
}
