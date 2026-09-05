using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Waves.Settings;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class CloseDialog : ContentDialog, IResultDialog<CloseWindowResult>
    {
        public CloseDialog(DialogSession dialogSession)
        {
            this.InitializeComponent();
            this.RequestedTheme = Instance
                .Host.Services.GetRequiredService<IThemeService>()
                .CurrentTheme;
            this.AppSettings = Instance.Host.Services.GetRequiredService<AppSettings>();
            DialogSession = dialogSession;
        }

        private bool isExit = false,
            isMin = false;

        public AppSettings AppSettings { get; }
        public DialogSession DialogSession { get; }

        private async void Min_Win(object sender, RoutedEventArgs e)
        {
            if (isClose.IsChecked == true)
            {
                await AppSettings.SetCloseWindowAsync("False");
            }
            this.isExit = false;
            this.isMin = true;

            DialogSession.Close(
                new CloseWindowResult() { IsExit = this.isExit, IsMinTaskBar = this.isMin }
            );
        }

        private async void Close_Win(object sender, RoutedEventArgs e)
        {
            if (isClose.IsChecked == true)
            {
                await AppSettings.SetCloseWindowAsync("True");
            }
            this.isExit = true;
            this.isMin = false;
            DialogSession.Close(
                new CloseWindowResult() { IsExit = this.isExit, IsMinTaskBar = this.isMin }
            );
        }

        public void SetData(object data) { }
    }
}
