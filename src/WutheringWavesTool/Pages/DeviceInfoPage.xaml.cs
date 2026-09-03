using Haiyu.Common.Contracts;

namespace Haiyu.Pages;

public sealed partial class DeviceInfoPage : Page, IWindowPage
{
    private bool _disposed;

    public DeviceInfoPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.GetService<DeviceInfoViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public DeviceInfoViewModel? ViewModel { get; private set; }

    public void SetData(object value)
    {
    }

    public void SetWindow(Window window)
    {
        this.ViewModel?.Initialization(window);
        title.Window = window;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            this.Bindings.StopTracking();
            this.ViewModel?.Dispose();
        }
        finally
        {
            title.Window = null;
            this.ViewModel = null;
        }
    }
}
