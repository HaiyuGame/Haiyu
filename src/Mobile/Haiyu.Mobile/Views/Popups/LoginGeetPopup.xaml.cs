using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Haiyu.Mobile.ViewModels.Popups;

namespace Haiyu.Mobile.Views.Popups;

public partial class LoginGeetPopup : ContentView
{
	public LoginGeetPopup(LoginGeetViewModel loginGeetViewModel)
	{
		InitializeComponent();
        this.BindingContext = loginGeetViewModel;
	}


	
}
