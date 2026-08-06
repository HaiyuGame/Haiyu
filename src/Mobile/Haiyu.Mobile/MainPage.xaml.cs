using CommunityToolkit.Maui.Extensions;
using ZXing.Net.Maui;

namespace Haiyu.Mobile
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();

            //barcodeReader.Options = new BarcodeReaderOptions
            //{
            //    Formats = BarcodeFormat.QrCode,
            //    AutoRotate = true,
            //    Multiple = false
            //};
        }


        private void BarcodeReader_BarcodesDetected(object sender, ZXing.Net.Maui.BarcodeDetectionEventArgs e)
        {
            //var first = e.Results.FirstOrDefault();

            //if (first == null)
            //    return;


            //MainThread.BeginInvokeOnMainThread(() =>
            //{
            //    DisplayAlert(
            //        "扫码结果",
            //        first.Value,
            //        "OK");
            //});
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            NewPage1 page1 = new NewPage1();
            await this.ShowPopupAsync(page1);
        }
    }
}
