using Haiyu.Mobile.ViewModels;

namespace Haiyu.Mobile.Views;

public partial class UserManagerPage : ContentPage
{
	public UserManagerPage(UserManagerViewModel viewModel)
	{
		InitializeComponent();
        this.BindingContext = viewModel;
	}

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        if (this.BindingContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
