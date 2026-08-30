using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Haiyu.Common;

public static partial class WindowExtension
{
    public enum CreateType
    {
        Subtitle,
    }

    public const int SW_SHOWNORMAL = 1;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ShellExecute(
        IntPtr hwnd,
        string verb,
        string file,
        string parameters,
        string directory,
        int showCmd
    );

    public const int GWL_STYLE = -16;
    public const uint WS_CAPTION = 0x00C00000;
    public const uint WS_MAXIMIZEBOX = 0x00010000;
    public const uint WS_MINIMIZEBOX = 0x00020000;
    public const uint WS_OVERLAPPED = 0x00000000;

    public const uint WS_OVERLAPPEDWINDOW = (
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX
    );

    public const uint WS_SYSMENU = 0x00080000;
    public const uint WS_THICKFRAME = 0x00040000;

    public const uint WS_EX_LAYERED = 0x80000;
    public const uint WS_EX_TRANSPARENT = 0x20;
    public const int GWL_EXSTYLE = -20;
    public const int LWA_ALPHA = 0;

    public const int GWL_HWNDPARENT = (-8);

    public static Window CreateTransparentWindow(Window win, CreateType type)
    {
        var window = new Window();
        var dpi = GetScaleAdjustment(win);
        var workArea = GetWorkarea();
        if (type == CreateType.Subtitle)
        {
            double height = 100;
            int leftMargin = 200;
            int rightMargin = 200;
            double width = workArea.Value.Right - workArea.Value.Left - leftMargin - rightMargin;
            int left = workArea.Value.Left + leftMargin;
            int top = workArea.Value.Bottom - (int)height;
            window.SetWindowSize(width / dpi, height / dpi);
            window.AppWindow.Move(new Windows.Graphics.PointInt32() { X = left, Y = top });
        }
        window.Activate();
        return window;
    }


    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial long GetWindowLongA(nint hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [LibraryImport("user32.dll")]
    public static partial int SetWindowLongA(nint hWnd, int nIndex, long dwNewLong);

    public static void SetLayerWindow(this Window window)
    {
        var hWnd = (nint)window.AppWindow.Id.Value;
        var exStyle = GetWindowLongA(hWnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_LAYERED) is 0)
            _ = SetWindowLongA(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
        var style = GetWindowLongA(hWnd, GWL_STYLE);
        _ = SetWindowLongA(hWnd, GWL_STYLE, style & ~WS_OVERLAPPEDWINDOW);
    }

    public static double GetScaleAdjustment(Window window)
    {
        nint hWnd = WindowNative.GetWindowHandle(window);
        Microsoft.UI.WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
        nint hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

        // Get DPI.
        int result = GetDpiForMonitor(
            hMonitor,
            Monitor_DPI_Type.MDT_Default,
            out uint dpiX,
            out uint _
        );
        if (result != 0)
        {
            throw new Exception("Could not get DPI for monitor.");
        }

        uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }

    public static void Penetrate(Window window)
    {
        nint hWnd = WindowNative.GetWindowHandle(window);
        GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_TRANSPARENT | WS_EX_LAYERED);
        SetLayeredWindowAttributes(hWnd, 0, 100, LWA_ALPHA);
    }

    public static void UnPenetrate(Window window)
    {
        nint hWnd = WindowNative.GetWindowHandle(window);
        uint currentExStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        uint newExStyle = currentExStyle & ~WS_EX_TRANSPARENT;
        SetWindowLong(hWnd, GWL_EXSTYLE, newExStyle);
        SetLayeredWindowAttributes(hWnd, 0, 100, LWA_ALPHA);
    }

    [LibraryImport("Shcore.dll", SetLastError = true)]
    public static partial int GetDpiForMonitor(
        nint hmonitor,
        Monitor_DPI_Type dpiType,
        out uint dpiX,
        out uint dpiY
    );

    [LibraryImport("user32.dll")]
    public static partial int SetWindowLong(nint hWnd, int nIndex, uint dwNewLong);

    [LibraryImport("user32", EntryPoint = "GetWindowLong")]
    public static partial uint GetWindowLong(nint hwnd, int nIndex);

    [LibraryImport("user32", EntryPoint = "SetLayeredWindowAttributes")]
    public static partial int SetLayeredWindowAttributes(
        nint hwnd,
        int crKey,
        int bAlpha,
        int dwFlags
    );

    public enum Monitor_DPI_Type : int
    {
        MDT_Effective_DPI = 0,
        MDT_Angular_DPI = 1,
        MDT_Raw_DPI = 2,
        MDT_Default = MDT_Effective_DPI,
    }

    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    public static POINT GetCursorPosition()
    {
        POINT lpPoint;
        GetCursorPos(out lpPoint);
        return lpPoint;
    }

    public static Common.Win32.RECT? GetWorkarea()
    {
        Common.Win32.RECT workArea = new Common.Win32.RECT();
        if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0))
        {
            return workArea;
        }
        return null;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int ShellExecute(
        nint hwnd,
        string lpOperation,
        string lpFile,
        string lpParameters,
        string lpDirectory,
        ShowCommands nShowCmd
    );

    internal static void SetWindowTopMost(HWND hwnd, bool enabled)
    {
        HWND insertAfter = enabled
            ? new HWND(-1) // HWND_TOPMOST
            : new HWND(-2); // HWND_NOTOPMOST

        PInvoke.SetWindowPos(
            hwnd,
            insertAfter,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
    }

    public enum ShowCommands : int
    {
        SW_HIDE = 0,
        SW_NORMAL = 1, // 默认值，正常方式启动
        SW_MAXIMIZE = 3, // 最大化窗口启动
        SW_MINIMIZE = 6, // 最小化窗口启动
        SW_SHOW = 5, // 显示窗口启动
        SW_SHOWDEFAULT = 10, // 使用默认设置启动
    }

    [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool ShowWindow(IntPtr hWnd, short State);


    public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 4)
        {
            return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }
        return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
    }

    [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
    public static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
    public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public static long GetWindowLongAtt(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 4)
        {
            return GetWindowLong32(hWnd, nIndex);
        }
        return GetWindowLongPtr64(hWnd, nIndex);
    }

    [DllImport("User32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
    public static extern long GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("User32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
    public static extern long GetWindowLongPtr64(IntPtr hWnd, int nIndex);
}

public static partial class LayerWindowHelper
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("CodeQuality", "IDE0079:请删除不必要的忽略")]
    private const int WS_EX_LAYERED = 0x80000;

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("CodeQuality", "IDE0079:请删除不必要的忽略")]
    private const int GWL_EXSTYLE = -20;

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial int GetWindowLongA(nint hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    public static partial int SetWindowLongA(nint hWnd, int nIndex, int dwNewLong);

    public static void SetLayerWindow(Window window)
    {
        var hWnd = (nint)window.AppWindow.Id.Value;
        var exStyle = GetWindowLongA(hWnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_LAYERED) is 0)
            _ = SetWindowLongA(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
    }

}


public static class NativeWindowHelper
{
    private const int WM_NCLBUTTONDBLCLK = 0x00A3; // Non-client left button double-click
    private const int WM_SYSCOMMAND = 0x0112; // System command message
    private const int SC_MAXIMIZE = 0xF030; // Maximize command
    private const int WM_SIZE = 0x0005; // Resize message
    private const int SIZE_MAXIMIZED = 2; // Maximized size
    private const int WM_DPICHANGED = 0x02E0; // DPI change message
    private const int GWLP_WNDPROC = -4;

    // Static field to hold the delegate, preventing it from being garbage-collected
    private static WndProcDelegate _currentWndProcDelegate;

    // Delegate for the new window procedure
    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    public static void ForceDisableMaximize(Window window, int? targetDipWidth = null, int? targetDipHeight = null)
    {
        var hwnd = WindowNative.GetWindowHandle(window);

        if (hwnd == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("Invalid window handle. Cannot hook window procedure.");
            return;
        }

        // Store the original WndProc and assign the new one
        IntPtr originalWndProc = GetWindowLongPtr(hwnd, GWLP_WNDPROC);
        if (originalWndProc == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("Failed to retrieve the original WndProc.");
            return;
        }

        _currentWndProcDelegate = (wndHwnd, msg, wParam, lParam) =>
        {
            // Suppress double-click maximize
            if (msg == WM_NCLBUTTONDBLCLK)
            {
                System.Diagnostics.Debug.WriteLine("Double-click maximize suppressed.");
                return IntPtr.Zero;
            }

            // Suppress system maximize command (e.g., via keyboard shortcuts or title bar menu)
            if (msg == WM_SYSCOMMAND && wParam.ToInt32() == SC_MAXIMIZE)
            {
                System.Diagnostics.Debug.WriteLine("Maximize via system command suppressed.");
                return IntPtr.Zero;
            }

            if (msg == WM_DPICHANGED && targetDipWidth.HasValue && targetDipHeight.HasValue)
            {
                int newDpiX = wParam.ToInt32() & 0xFFFF;
                double newScale = newDpiX / 96.0;
                int newPixelWidth = (int)Math.Round(targetDipWidth.Value * newScale);
                int newPixelHeight = (int)Math.Round(targetDipHeight.Value * newScale);

                var result = CallWindowProc(originalWndProc, wndHwnd, msg, wParam, lParam);

                var rect = Marshal.PtrToStructure<Common.Win32.RECT>(lParam);
                window.AppWindow.Move(new Windows.Graphics.PointInt32 { X = rect.Left, Y = rect.Top });
                window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = newPixelWidth, Height = newPixelHeight });

                return result;
            }

            try
            {
                if (wndHwnd != IntPtr.Zero && originalWndProc != IntPtr.Zero)
                {
                    return CallWindowProc(originalWndProc, wndHwnd, msg, wParam, lParam);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Invalid parameters in WndProc call.");
                    return IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions to avoid crashing
                System.Diagnostics.Debug.WriteLine($"Error in WndProc: {ex.Message}");
                return IntPtr.Zero;
            }
        };

        try
        {
            // Hook the new WndProc
            IntPtr result = SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_currentWndProcDelegate));
            if (result == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set new WndProc. Error: {Marshal.GetLastWin32Error()}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error hooking window procedure: {ex.Message}");
            return;
        }

        // Prevent garbage collection of the delegate (redundant but safe)
        GC.KeepAlive(_currentWndProcDelegate);
    }

    // Win32 API declarations
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);
    }
}
