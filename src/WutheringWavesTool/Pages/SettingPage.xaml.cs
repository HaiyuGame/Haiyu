using Haiyu.Common.Contracts;

namespace Haiyu.Pages;

public sealed partial class SettingPage : Page, IPage
{
    public SettingPage()
    {
        this.InitializeComponent();
        this.ViewModel = Instance.GetService<SettingViewModel>()!;
    }

    public Type PageType => typeof(SettingPage);

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        try
        {
            this.Bindings.StopTracking();
            this.ViewModel?.Dispose();
        }
        finally
        {
            this.ViewModel = null;
            base.OnNavigatedFrom(e);
        }
    }

    public SettingViewModel? ViewModel { get; private set; }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;
        await ViewModel.WindowManager.Shell.DialogManager.ShowMessageDialog(new ShowDialogOption()
        {
            Context = LanguageService.GetString("Settings_ExplainMirror"),
            CloseText  = LanguageService.GetStringByText("确定"),
             ShowPrimaryButton = false,
        });
    }
}
