using Haiyu.Mobile.ViewModels;

namespace Haiyu.Mobile.Views;

public partial class AddKuroUserPage : ContentPage
{
	public AddKuroUserPage(AddKuroUserViewModel addKuroUserViewModel)
	{
		InitializeComponent();
        this.BindingContext = addKuroUserViewModel;
	}


    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        if(this.BindingContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
