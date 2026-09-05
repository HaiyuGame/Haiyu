using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class LocalGameTokenDialog : ContentDialog,IDialog
{
    public LocalGameTokenDialog(
        LocalGameTokenViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
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
