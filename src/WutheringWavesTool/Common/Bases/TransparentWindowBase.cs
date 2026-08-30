using System;
using System.Collections.Generic;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Haiyu.Common.Bases;

public class TransparentWindowBase : Window
{
    private const int WsExLayered = 0x00080000;
    private const int BorderlessStyleMask =
        0x00C00000 | 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
    private const uint DwmWindowCornerPreference = 33;
    private const uint DwmBorderColor = 34;
    private const uint DwmDoNotRound = 1;
    private const uint DwmColorNone = 0xFFFFFFFE;

    public TransparentWindowBase()
    {
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        nint rawHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        HWND hwnd = new(rawHwnd);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        SystemBackdrop = new TransparentBackdrop(rawHwnd);

        int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | WsExLayered);
        PInvoke.SetLayeredWindowAttributes(
            hwnd,
            new COLORREF(0),
            255,
            LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA
        );

        int style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style & ~BorderlessStyleMask);

        uint cornerPreference = DwmDoNotRound;
        _ = DwmSetWindowAttribute(
            rawHwnd,
            DwmWindowCornerPreference,
            ref cornerPreference,
            sizeof(uint)
        );
        uint borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(rawHwnd, DwmBorderColor, ref borderColor, sizeof(uint));
        RefreshWindowFrame();
    }

    private void RefreshWindowFrame()
    {
        HWND hwnd = new(WinRT.Interop.WindowNative.GetWindowHandle(this));
        PInvoke.SetWindowPos(
            hwnd,
            HWND.Null,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED
                | SET_WINDOW_POS_FLAGS.SWP_NOMOVE
                | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
                | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
                | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW
        );
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        uint attribute,
        ref uint value,
        uint valueSize
    );
}
