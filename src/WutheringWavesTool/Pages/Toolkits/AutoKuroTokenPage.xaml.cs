using Haiyu.ViewModel.ToolkitsViewModel;

namespace Haiyu.Pages.Toolkits;

public sealed partial class AutoKuroTokenPage : Page, IWindowPage
{
    private bool _disposed;

    public AutoKuroTokenPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<AutoKuroTokenViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public AutoKuroTokenViewModel? ViewModel { get; private set; }

    public void SetData(object value) { }

    public void SetWindow(Window window)
    {
        if (ViewModel is null)
            return;
        this.ViewModel.Window = window;
        this.ViewModel.Window.ExtendsContentIntoTitleBar = true;
        this.titleBar.Window = window;
        this.ViewModel.Window.ApplyWindowsOption(
            new()
            {
                Height = 580,
                Width = 1000,
                MaxHeight = 580,
                MaxWidth =1000,
                MinHeight = 580,
                MinWidth=1000,
                IsMaximizable = false,
                IsMinimizable = false,
                IsResizable = false,
            }
        );
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
