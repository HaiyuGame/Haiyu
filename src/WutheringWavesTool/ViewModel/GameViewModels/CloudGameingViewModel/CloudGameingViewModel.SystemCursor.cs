using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel.GameViewModels;

partial class CloudGameingViewModel
{
    #region Win32

    private const int CURSOR_SHOWING = 0x00000001;
    private const uint SPI_SETCURSORS = 0x0057;
    private const uint WM_SETCURSOR = 0x0020;
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint HTCLIENT = 1;
    private delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr lParam);
    private static readonly UIntPtr WebViewCursorSubclassId = new(0x48415955);
    private static readonly uint[] SystemCursorIds =
    [
        32512, // OCR_NORMAL
        32513, // OCR_IBEAM
        32514, // OCR_WAIT
        32515, // OCR_CROSS
        32516, // OCR_UP
        32642, // OCR_SIZENWSE
        32643, // OCR_SIZENESW
        32644, // OCR_SIZEWE
        32645, // OCR_SIZENS
        32646, // OCR_SIZEALL
        32648, // OCR_NO
        32649, // OCR_HAND
        32650, // OCR_APPSTARTING
        32651, // OCR_HELP
        32671, // OCR_PIN
        32672, // OCR_PERSON
    ];

    private delegate IntPtr SUBCLASSPROC(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData
    );

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(out CURSORINFO cursorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemCursor(IntPtr cursor, uint cursorId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateCursor(
        IntPtr instance,
        int xHotSpot,
        int yHotSpot,
        int width,
        int height,
        byte[] andPlane,
        byte[] xorPlane
    );

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        IntPtr data,
        uint flags
    );

    [DllImport("user32.dll")]
    private static extern int ShowCursor(bool show);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(IntPtr parentWindow, IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr hWnd,
        StringBuilder lpClassName,
        int nMaxCount
    );

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(
        IntPtr hWndParent,
        EnumWindowsProc lpEnumFunc,
        IntPtr lParam
    );

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData
    );

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        UIntPtr uIdSubclass
    );

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }
    #endregion

    private bool _cursorHidden;
    private bool _cloudCursorHiddenRequested;
    private bool _windowIsActive = true;
    private bool _systemCursorSchemeOverridden;
    private bool _webViewCursorSubclassInstalled;
    private nint _webViewWindowHandle;
    private DispatcherTimer _cursorTimer;
    private DispatcherTimer _hotkeyTimer;
    private bool _f11WasDown;
    private SUBCLASSPROC _webViewCursorSubclassProc;

    private void HideSystemCursor()
    {
        if (!_windowIsActive)
        {
            return;
        }

        if (_cursorHidden && _systemCursorSchemeOverridden)
        {
            return;
        }

        _cursorHidden = true;

        TryInstallWebViewCursorSubclass();
        OverrideSystemCursorsWithTransparent();
        EnsureSystemCursorHidden();

        var hitWindow = GetWebViewWindowUnderCursor();
        if (_webViewCursorSubclassInstalled && hitWindow != IntPtr.Zero)
        {
            _ = SetCursor(IntPtr.Zero);
        }

        if (_cursorTimer is null)
        {
            _cursorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _cursorTimer.Tick += (_, _) =>
            {
                if (_cursorHidden && IsSystemCursorVisible())
                {
                    EnsureSystemCursorHidden();
                }
            };
        }

        _cursorTimer.Start();
    }

    public void ShowSystemCursor()
    {
        _cursorHidden = false;
        _cursorTimer?.Stop();
        RestoreSystemCursors();
        while (ShowCursor(true) < 0) { }

        var hitWindow = GetWebViewWindowUnderCursor();
        if (hitWindow != IntPtr.Zero)
        {
            var setCursorLParam = new IntPtr(
                unchecked((int)((WM_MOUSEMOVE << 16) | HTCLIENT))
            );
            _ = SendMessage(hitWindow, WM_SETCURSOR, hitWindow, setCursorLParam);
        }
    }

    private void ApplyCloudCursorVisibility(bool visible)
    {
        _cloudCursorHiddenRequested = !visible;

        if (_cloudCursorHiddenRequested && _windowIsActive)
        {
            HideSystemCursor();
        }
        else
        {
            ShowSystemCursor();
        }
    }

    private void ApplyWindowActivationState(bool isActive)
    {
        _windowIsActive = isActive;

        if (_windowIsActive && _cloudCursorHiddenRequested)
        {
            HideSystemCursor();
        }
        else
        {
            ShowSystemCursor();
        }
    }

    private static bool IsSystemCursorVisible()
    {
        var cursorInfo = new CURSORINFO
        {
            cbSize = Marshal.SizeOf<CURSORINFO>()
        };

        return GetCursorInfo(out cursorInfo)
            && (cursorInfo.flags & CURSOR_SHOWING) == CURSOR_SHOWING;
    }

    private static void EnsureSystemCursorHidden()
    {
        while (ShowCursor(false) >= 0) { }
    }

    private void OverrideSystemCursorsWithTransparent()
    {
        if (_systemCursorSchemeOverridden)
        {
            return;
        }

        foreach (var cursorId in SystemCursorIds)
        {
            var transparentCursor = CreateTransparentCursorHandle();
            if (transparentCursor != IntPtr.Zero)
            {
                _ = SetSystemCursor(transparentCursor, cursorId);
            }
        }

        _systemCursorSchemeOverridden = true;
    }

    private void RestoreSystemCursors()
    {
        if (!_systemCursorSchemeOverridden)
        {
            return;
        }

        _ = SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        _systemCursorSchemeOverridden = false;
    }

    private static IntPtr CreateTransparentCursorHandle()
    {
        byte[] andMask = [0xFF, 0xFF, 0xFF, 0xFF];
        byte[] xorMask = [0x00, 0x00, 0x00, 0x00];
        return CreateCursor(IntPtr.Zero, 0, 0, 1, 1, andMask, xorMask);
    }

    private void StartHotkeyTimer()
    {
        if (_hotkeyTimer is not null)
        {
            return;
        }

        _hotkeyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _hotkeyTimer.Tick += async (_, _) =>
        {
            var f11Down = (GetAsyncKeyState(0x7A) & 0x8000) != 0;
            if (f11Down && !_f11WasDown)
            {
                _f11WasDown = true;
                await ToggleFullScreenAsync();
            }
            else if (!f11Down)
            {
                _f11WasDown = false;
            }
        };
        _hotkeyTimer.Start();
    }

    private async Task ToggleFullScreenAsync()
    {
        if(this.Window.AppWindow == null)
        {
            return;
        }
        if(this.Window.AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped)
        {
            this.Window.SetWindowPresenter(AppWindowPresenterKind.FullScreen);
            this.Window.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
            this.TitleBarVisiblity = Visibility.Collapsed;
        }
        else
        {
            this.Window.SetWindowPresenter(AppWindowPresenterKind.Overlapped);
            this.Window.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            this.TitleBarVisiblity = Visibility.Visible;
        }
        await SyncBridgeResolutionAsync();
    }

    private void TryInstallWebViewCursorSubclass()
    {
        if (_webViewCursorSubclassInstalled)
        {
            return;
        }

        if (WindowHandle == IntPtr.Zero)
        {
            WindowHandle = Window.GetWindowHandle();
            if (WindowHandle == IntPtr.Zero)
            {
                return;
            }
        }

        _webViewWindowHandle = FindWebViewChildWindow(WindowHandle);
        if (_webViewWindowHandle == IntPtr.Zero)
        {
            return;
        }

        _webViewCursorSubclassProc ??= WebViewCursorSubclassProc;
        _webViewCursorSubclassInstalled = SetWindowSubclass(
            WindowHandle,
            _webViewCursorSubclassProc,
            WebViewCursorSubclassId,
            UIntPtr.Zero
        );

        if (
            _webViewCursorSubclassInstalled
            && _cursorHidden
            && GetWebViewWindowUnderCursor() != IntPtr.Zero
        )
        {
            _ = SetCursor(IntPtr.Zero);
        }
    }

    private void ReleaseWebViewCursorSubclass()
    {
        if (
            _webViewCursorSubclassInstalled
            && WindowHandle != IntPtr.Zero
            && _webViewCursorSubclassProc is not null
        )
        {
            _ = RemoveWindowSubclass(
                WindowHandle,
                _webViewCursorSubclassProc,
                WebViewCursorSubclassId
            );
        }

        _webViewCursorSubclassInstalled = false;
        _webViewWindowHandle = IntPtr.Zero;
    }

    private IntPtr WebViewCursorSubclassProc(
            IntPtr windowHandle,
            uint message,
            IntPtr wParam,
            IntPtr lParam,
            UIntPtr subclassId,
            UIntPtr referenceData
        )
    {
        if (
            _cursorHidden
            && message == WM_SETCURSOR
            && IsWebViewHitWindow(wParam)
        )
        {
            SetCursor(IntPtr.Zero);
            return new IntPtr(1);
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private IntPtr FindWebViewChildWindow(IntPtr parentWindowHandle)
    {
        IntPtr result = IntPtr.Zero;

        EnumChildWindows(
            parentWindowHandle,
            (childHandle, _) =>
            {
                if (IsWebViewWindowClass(childHandle))
                {
                    result = childHandle;
                    return false;
                }

                return true;
            },
            IntPtr.Zero
        );

        return result;
    }

    private static bool IsWebViewWindowClass(IntPtr windowHandle)
    {
        var classNameBuilder = new StringBuilder(256);
        _ = GetClassName(windowHandle, classNameBuilder, classNameBuilder.Capacity);
        var className = classNameBuilder.ToString();

        return className.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal)
            || className.Contains("WebView", StringComparison.OrdinalIgnoreCase);
    }

    private IntPtr GetWebViewWindowUnderCursor()
    {
        if (
            _webViewWindowHandle == IntPtr.Zero
            || !GetCursorPos(out var cursorPosition)
        )
        {
            return IntPtr.Zero;
        }

        var hitWindow = WindowFromPoint(cursorPosition);
        return IsWebViewHitWindow(hitWindow) ? hitWindow : IntPtr.Zero;
    }

    private bool IsWebViewHitWindow(IntPtr hitWindow)
    {
        return hitWindow != IntPtr.Zero
            && _webViewWindowHandle != IntPtr.Zero
            && (
                hitWindow == _webViewWindowHandle
                || IsChild(_webViewWindowHandle, hitWindow)
            );
    }


}
