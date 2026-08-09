
using Haiyu.ViewModel.GameViewModels;
using Waves.Core.Models.CloudGame;

namespace Haiyu.WindowModels;


public sealed partial class CloudGameWindows : Window
{
    private bool _isClosing;
    public CloudGameingViewModel ViewModel { get; private set; }
    public CloudGameSettingViewModel CloudSettingModel { get; }

    public CloudGameWindows(BrowserSessionLaunchOptions option)
    {
        InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.ViewModel = Instance.Host.Services.GetRequiredService<CloudGameingViewModel>();
        this.CloudSettingModel = Instance.Host.Services.GetRequiredService<CloudGameSettingViewModel>();
        ViewModel.SetWebView(this._browser, this, option);
        this.AppWindow.Closing += CloudGameWindows_Closing;
        this.grid.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        this._browser.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    private void CloudGameWindows_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        // Dispose cancels KeepAlive CTS + ViewModelBase.CTS → expected OperationCanceledException
        // on in-flight HTTP/timer. That is normal shutdown noise, not a gameplay crash.
        try
        {
            ViewModel?.ShowSystemCursor();
            ViewModel?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CloudGameWindows] Dispose on close: {ex}");
        }
        finally
        {
            this.ViewModel = null;
        }
    }

    private void TitleBar_PaneToggleRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        this.view.IsPaneOpen = !this.view.IsPaneOpen;
    }

}
