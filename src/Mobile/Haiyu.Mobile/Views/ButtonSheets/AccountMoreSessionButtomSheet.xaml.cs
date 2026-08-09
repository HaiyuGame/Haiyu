using Haiyu.Mobile.ViewModels.ButtonSheet;
using Haiyu.Mobile.ViewModels.ItemsViewModles;
using Plugin.Maui.BottomSheet.Navigation;
using static Android.App.DownloadManager;

namespace Haiyu.Mobile.Views.ButtonSheets;

public partial class AccountMoreSessionButtomSheet : ContentPage
{
	public AccountMoreSessionButtomSheet(AccountMoreSessionViewModel viewModel)
	{
		InitializeComponent();
        this.BindingContext = viewModel;
	}

}
