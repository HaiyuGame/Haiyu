using Haiyu.Models.Dialogs;
using Haiyu.Services.DialogServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class CloseDialog : ContentDialog,
            IResultDialog<CloseWindowResult>
    {
        public CloseDialog()
        {
            this.InitializeComponent();
            this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
            this.AppSettings = Instance.Host.Services.GetRequiredService<AppSettings>();
        }

        private bool isExit = false, isMin = false;

        public AppSettings AppSettings { get; }

        public CloseWindowResult GetResult()
        {
            return new CloseWindowResult() { IsExit = this.isExit, IsMinTaskBar = this.isMin };
        }

        private void Min_Win(object sender, RoutedEventArgs e)
        {
            if (isClose.IsChecked == true)
            {
                AppSettings.CloseWindow = "False";
            }
            this.isExit = false;
            this.isMin = true;
            Instance.Host.Services.GetRequiredKeyedService<IDialogManager>(nameof(MainDialogService)).CloseDialog();
        }

        private void Close_Win(object sender, RoutedEventArgs e)
        {
            if (isClose.IsChecked == true)
            {
                AppSettings.CloseWindow = "True";
            }
            this.isExit = true;
            this.isMin = false;
            Instance.Host.Services.GetRequiredKeyedService<IDialogManager>(nameof(MainDialogService)).CloseDialog();
        }

        public void SetData(object data)
        {
        }
    }
}
