using Haiyu.ViewModel.Communitys;

namespace Haiyu.Pages.Communitys;

public sealed partial class GamerSignPage : Page, IWindowPage
{
    private bool _disposed;

    public GamerSignPage()
    {
        this.InitializeComponent();
        this.ViewModel = Instance.Host.Services!.GetRequiredService<GamerSignViewModel>();

        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public GamerSignViewModel? ViewModel { get; private set; }

    public void SetData(object value)
    {
        if (value is GameRoilDataItem item && ViewModel is not null)
        {
            this.ViewModel.SignRoil = item;
        }
    }

    public void SetWindow(Window window)
    {
        this.titlebar.Window = window;
        this.titlebar.IsExtendsContentIntoTitleBar = true;
        this.titlebar.UpDate();
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
            this.titlebar.Window = null;
            this.ViewModel = null;
        }
    }
}
