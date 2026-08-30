using System.Runtime.Intrinsics.Arm;
using Haiyu.ViewModel.ToolkitsViewModel;
using Windows.Win32.Foundation;

namespace Haiyu.Pages.Toolkits;

public sealed partial class MonitorToolPage : Page, IWindowPage
{
    private bool _disposed;

    public MonitorToolPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<MonitorToolViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public MonitorToolViewModel? ViewModel { get; private set; }

    public void SetData(object value) { }

    public void SetWindow(Window window)
    {
        if (ViewModel is null)
            return;
        var workArea = WindowExtension.GetWorkarea();
        var dpi = WindowExtension.GetScaleAdjustment(window);
        double height = 50;
        int leftMargin = 200;
        int rightMargin = 200;
        double width = workArea.Value.Right - workArea.Value.Left - leftMargin - rightMargin;
        int left = workArea.Value.Left + leftMargin;
        int top = workArea.Value.Top;
        nint rawHwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        HWND hwnd = new(rawHwnd);
        WindowExtension.SetWindowTopMost(hwnd, true);
        window.SetWindowSize(width / dpi, height / dpi);
        window.AppWindow.Move(new Windows.Graphics.PointInt32
        {
            X = left,
            Y = top
        });
        this.ViewModel.Window = window;
        this.ViewModel.Window.AppWindow.Closing += CloseWindow;
    }

    private void CloseWindow(AppWindow sender, AppWindowClosingEventArgs args)
    {
        this.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            this.DataContext = null;
            this.ViewModel?.Dispose();
        }
        finally
        {
            this.ViewModel = null;
        }
    }
}
