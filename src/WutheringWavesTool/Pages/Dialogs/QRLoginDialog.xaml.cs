using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class QRLoginDialog : ContentDialog, IResultDialog<QRScanResult>
    {
        public QRLoginDialog(
        QrLoginViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

        public QrLoginViewModel? ViewModel { get; }

        public QRScanResult? GetResult()
        {
            return ViewModel?.Result;
        }

        public void SetData(object data)
        {
        }
    }
}
