using Haiyu.Common.Contracts;

namespace Haiyu.Pages;

public sealed partial class WavesAnalysisRecordPage : Page,IWindowPage
{
    private bool _disposed;

    public WavesAnalysisRecordViewModel? ViewModel { get; private set; }

    public WavesAnalysisRecordPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<WavesAnalysisRecordViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public void SetWindow(Window window)
    {
        this.ViewModel?.Initialization(window);
        this.titleBar.Window = window;
    }

    public void SetData(object value)
    {
        if(value is CloudGameLoginSession session)
        {
            this.ViewModel?.SetSessionAsync(session);
        }
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
            this.titleBar.Window = null;
            this.ViewModel = null;
        }
    }
}
