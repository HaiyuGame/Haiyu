namespace Haiyu.Pages.Dialogs;

public sealed partial class LocalGameTokenDialog : Page, IWindowPage
{
    public LocalGameTokenDialog()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<LocalGameTokenViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }


    public LocalGameTokenViewModel ViewModel { get; }

    public void Dispose()
    {
    }

    public async void SetData(object value)
    {
        if(value is string contextName)
        {
            await this.ViewModel.RefreshContextName(contextName);
        }
    }

    public void SetWindow(Window window)
    {
        this.ViewModel.Initialization(window);
        title.Window = this.ViewModel.Window;
        title.Window.Closed += Window_Closed;
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        this.ViewModel?.Dispose();
    }
}
