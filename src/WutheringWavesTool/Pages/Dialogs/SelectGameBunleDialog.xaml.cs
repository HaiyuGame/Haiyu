using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;

namespace Haiyu.Pages.Dialogs;

public sealed partial class SelectGameBunleDialog : ContentDialog, IDialog
{
    public SelectGameBunleDialog(SelectGameBunleViewModel viewModel)
    {
        this.InitializeComponent();
        this.ViewModel = viewModel;
        this.RequestedTheme = Instance
            .Host.Services.GetRequiredService<IThemeService>()
            .CurrentTheme;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
                e.Handled = true;
        };
    }
    public SelectGameBunleViewModel ViewModel { get; }


    public void SetData(object data) 
    {
        if(data is SelectGameBunleRequest request)
        {
            this.ViewModel.SwitchCore(request.ContextName);
        }
    }

}
