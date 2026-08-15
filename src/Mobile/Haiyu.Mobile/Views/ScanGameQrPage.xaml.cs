using Haiyu.Mobile.ViewModels;
using ZXing.Net.Maui;

namespace Haiyu.Mobile.Views;

public partial class ScanGameQrPage : ContentPage
{
	public ScanGameQrPage(ScanGameQrViewModel viewModel)
	{
		InitializeComponent();
        this.BindingContext = viewModel;
	}

    private void camera_BarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (BindingContext is ScanGameQrViewModel vm)
        {
            vm.HandleBarcodesDetected(e);
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        if (this.BindingContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
