namespace Haiyu.Pages.Dialogs;

public sealed partial class LocalGameTokenDialog : ContentDialog,IDialog
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

}
