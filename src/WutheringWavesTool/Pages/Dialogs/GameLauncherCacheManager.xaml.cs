using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class GameLauncherCacheManager : ContentDialog, IDialog
    {
        public GameLauncherCacheManager(
        GameLauncherCacheViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

        public GameLauncherCacheViewModel ViewModel { get; }

        public void SetData(object data)
        {
            if (data is GameLauncherCacheArgs args)
            {
                ViewModel.SetData(args);
            }
        }

    }
}
