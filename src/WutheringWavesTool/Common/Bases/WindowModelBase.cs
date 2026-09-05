namespace Haiyu.Common.Bases;

public partial class WindowModelBase : Window
{
    private readonly nint _ownerHwnd;
    private bool _detached;

    public AppWindow AppWindowApp;

    public WindowsOption? WindowsOption { get; }

    OverlappedPresenter? Overlapped => this.AppWindow.Presenter as OverlappedPresenter;

    public WinUIEx.WindowManager Manager => WinUIEx.WindowManager.Get(this);

    public WindowModelBase(nint value, WindowsOption? windowsOption = null)
    {
        _ownerHwnd = value;
        WindowsOption = windowsOption;
        this.SystemBackdrop = new DesktopAcrylicBackdrop();
        if (Overlapped != null)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId1 = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindowApp = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId1);
            WindowExtension.SetWindowLong(hWnd, WindowExtension.GWL_HWNDPARENT, _ownerHwnd);
            Microsoft.UI.Windowing.OverlappedPresenter presenter = OverlappedPresenter.CreateForDialog();
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.IsModal = true;
            this.AppWindow.SetPresenter(presenter);
        }

        this.Closed += OnWindowClosed;
        this.ApplyWindowsOption(windowsOption);
    }

    private void OnWindowClosed(object sender, WindowEventArgs e)
    {
        Closed -= OnWindowClosed;
        DetachFromVisualTree();
        try
        {
            var windowId = Win32Interop.GetWindowIdFromWindow(_ownerHwnd);
            var parentAppWindow = AppWindow.GetFromWindowId(windowId);
            parentAppWindow.Show();
            WindowExtension.SwitchToThisWindow(_ownerHwnd, true);
        }
        catch
        {
        }
    }

    protected virtual void OnDetaching() { }

    public void DetachFromVisualTree()
    {
        if (_detached)
            return;
        _detached = true;
        try
        {
            OnDetaching();
            if (Content is IDisposable disposable)
                disposable.Dispose();
        }
        finally
        {
            Content = null;
        }
    }
}
