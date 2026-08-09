using Haiyu.Mobile.ViewModels;

namespace Haiyu.Mobile.Views;

public partial class AddKuroUserPage : ContentPage
{
	public AddKuroUserPage(AddKuroUserViewModel addKuroUserViewModel)
	{
		InitializeComponent();
        this.BindingContext = addKuroUserViewModel;
	}
}
