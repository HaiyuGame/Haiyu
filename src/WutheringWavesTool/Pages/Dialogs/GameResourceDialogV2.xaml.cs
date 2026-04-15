namespace Haiyu.Pages.Dialogs;

public sealed partial class GameResourceDialogV2 : ContentDialog
{
    public GameResourceDialogV2(GameResourceViewModelV2 viewModel)
    {
        this.InitializeComponent();
        ViewModel = viewModel;
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public GameResourceViewModelV2 ViewModel { get; }

    internal void SetData(string contextName)
    {
        this.ViewModel.SetData(contextName);
    }
}
