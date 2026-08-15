using Haiyu.Mobile.ViewModels.Popups;

namespace Haiyu.Mobile.Views.Popups;

public partial class AddKuroTokenPopup : ContentView
{
	public AddKuroTokenPopup(AddKuroTokenViewModel viewModel)
	{
		InitializeComponent();
        this.BindingContext = viewModel;
        
	}
}
