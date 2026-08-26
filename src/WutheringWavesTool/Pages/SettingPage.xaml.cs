namespace Haiyu.Pages;

public sealed partial class SettingPage : Page, IPage
{
    public SettingPage()
    {
        this.InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<SettingViewModel>();
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
        await ViewModel.DialogManager.ShowMessageDialog(new ShowDialogOption()
        {
            Context = LanguageService.GetStringByText(
                "Mirror酱是一个第三方应用分发平台，Haiyu通过此平台避开Github的访问问题以提供国内高速下载。\n"
                + "此下载方式为付费下载，可在Mirror官网了解整个分发服务原理，定价与解释权由Mirror官方提供。"
            ),
            CloseText  = LanguageService.GetStringByText("确定"),
             ShowPrimaryButton = false,
        });
    }
}
