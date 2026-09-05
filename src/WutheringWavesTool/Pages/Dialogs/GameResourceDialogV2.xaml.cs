using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class GameResourceDialogV2 : ContentDialog, IDialog
{
    public GameResourceDialogV2(GameResourceViewModelV2 viewModel)
    {
        this.InitializeComponent();
        ViewModel = viewModel;
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public GameResourceViewModelV2 ViewModel { get; }

    public void SetData(object data)
    {
        if (data is string str)
        {
            this.ViewModel.SetData(str);
        }
    }

}
