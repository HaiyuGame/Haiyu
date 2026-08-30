using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Microsoft.UI.Composition;

namespace Haiyu.Common.Bases;


public sealed partial class TransparentBackdrop : CompositionBrushBackdrop
{
    private const uint WmEraseBkgnd = 0x0014,
        WmDwmCompositionChanged = 0x031E;
    private readonly nint _hwnd;
    private WindowMessageMonitor? _monitor;
    private nint _backgroundBrush;

    public TransparentBackdrop(nint hwnd) =>
        _hwnd = hwnd != 0 ? hwnd : throw new ArgumentException("HWND 为空");

    protected override void OnTargetConnected(
        Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop target,
        XamlRoot xamlRoot
    )
    {
        _monitor = new(_hwnd);
        _monitor.MessageReceived += OnMessage;
        _monitor.Attach();
        ConfigureDwm(_hwnd);
        base.OnTargetConnected(target, xamlRoot);
        nint hdc = GetDC(_hwnd);
        try
        {
            ClearBackground(_hwnd, hdc);
        }
        finally
        {
            if (hdc != 0)
                ReleaseDC(_hwnd, hdc);
        }
    }

    protected override void OnTargetDisconnected(
        Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop target
    )
    {
        if (_monitor is not null)
        {
            _monitor.MessageReceived -= OnMessage;
            _monitor.Dispose();
            _monitor = null;
        }
        if (_backgroundBrush != 0)
        {
            DeleteObject(_backgroundBrush);
            _backgroundBrush = 0;
        }
        base.OnTargetDisconnected(target);
    }

    private void OnMessage(object? sender, WindowMessageEventArgs e)
    {
        if (e.MessageId == WmEraseBkgnd && ClearBackground(e.Hwnd, (nint) e.WParam))
        {
            e.Result = 1;
            e.Handled = true;
        }
        else if (e.MessageId == WmDwmCompositionChanged)
        {
            ConfigureDwm(e.Hwnd);
            e.Handled = true;
        }
    }

    private static void ConfigureDwm(nint hwnd)
    {
        Margins margins = new();
        Marshal.ThrowExceptionForHR(DwmExtendFrameIntoClientArea(hwnd, ref margins));
        nint region = CreateRectRgn(-2, -2, -1, -1);
        if (region == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            DwmBlurBehind blur = new()
            {
                Flags = 3,
                Enable = true,
                Region = region,
            };
            Marshal.ThrowExceptionForHR(DwmEnableBlurBehindWindow(hwnd, ref blur));
        }
        finally
        {
            DeleteObject(region);
        }
    }

    private bool ClearBackground(nint hwnd, nint hdc)
    {
        if (hdc == 0 || !GetClientRect(hwnd, out Rect rect))
            return false;
        _backgroundBrush = _backgroundBrush == 0 ? CreateSolidBrush(0) : _backgroundBrush;
        return _backgroundBrush != 0 && FillRect(hdc, ref rect, _backgroundBrush) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left,
            Right,
            Top,
            Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left,
            Top,
            Right,
            Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmBlurBehind
    {
        public uint Flags;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Enable;
        public nint Region;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Transition;
    }

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hwnd, nint hdc);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern int FillRect(nint hdc, ref Rect rect, nint brush);

    [DllImport("gdi32.dll")]
    private static extern nint CreateSolidBrush(uint color);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint handle);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmEnableBlurBehindWindow(nint hwnd, ref DwmBlurBehind blur);

    protected override Windows.UI.Composition.CompositionBrush CreateBrush(
        Windows.UI.Composition.Compositor compositor
    ) => compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
}
