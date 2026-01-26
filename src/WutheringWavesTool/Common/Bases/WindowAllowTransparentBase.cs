using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Haiyu.Common.Bases;

public partial class WindowAllowTransparentBase : Window
{
    public AppWindow AppWindowApp;

    OverlappedPresenter? Overlapped => this.AppWindow.Presenter as OverlappedPresenter;

    public WindowManager Manager => WindowManager.Get(this);

    private Windows.Win32.Foundation.HWND _handle; // 优化：私有字段加下划线命名规范
    private WINDOW_EX_STYLE WinExStyle
    {
        get => (WINDOW_EX_STYLE)PInvoke.GetWindowLong(_handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        set => _ = PInvoke.SetWindowLong(_handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)value);
    }

    // 新增：置顶相关Win32常量（SetWindowPos需要）
    private static readonly HWND HWND_TOPMOST = new(-1);    // 置顶标记
    private static readonly HWND HWND_NOTOPMOST = new(-2);  // 取消置顶标记

    public WindowAllowTransparentBase()
    {
        this.SystemBackdrop = new DesktopAcrylicBackdrop();

        if (Overlapped != null)
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId1 = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindowApp = AppWindow.GetFromWindowId(windowId1);

            #region [Transparency + TopMost]
            var hwnd = WindowNative.GetWindowHandle(this);
            _handle = new Windows.Win32.Foundation.HWND(hwnd);

            WinExStyle |= WINDOW_EX_STYLE.WS_EX_LAYERED;
            WinExStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
            SystemBackdrop = new TransparentTintBackdrop();

            SetWindowTopMost(true);
            #endregion
        }
    }

    public void SetWindowTopMost(bool isTopMost)
    {
        if (_handle.IsNull)
        {
            throw new InvalidOperationException("窗口句柄未初始化，无法设置置顶状态");
        }

        bool result = PInvoke.SetWindowPos(
            hWnd: _handle,
            hWndInsertAfter: isTopMost ? HWND_TOPMOST : HWND_NOTOPMOST,
            X: 0,
            Y: 0,
            cx: 0,
            cy: 0,
            uFlags: SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
        );
        if (!result)
        {
            throw new System.ComponentModel.Win32Exception();
        }
    }
}