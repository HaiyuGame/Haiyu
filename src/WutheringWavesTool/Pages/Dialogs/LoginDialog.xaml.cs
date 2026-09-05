using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class LoginDialog : ContentDialog, IDialog
{
    public LoginDialog(LoginGameViewModel viewModel)
    {
        this.InitializeComponent();
        this.ViewModel = viewModel;

        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public LoginGameViewModel ViewModel { get; }

    public void SetData(object data) { }

    private void SelectorBar_SelectionChanged(
        SelectorBar sender,
        SelectorBarSelectionChangedEventArgs args
    )
    {
        this.ViewModel.SwitchView(sender.SelectedItem.Tag.ToString());
    }
}
