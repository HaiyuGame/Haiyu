
using Haiyu.ViewModel.GameViewModels;
using Waves.Core.Models.CloudGame;

namespace Haiyu.WindowModels;


public sealed partial class CloudGameWindows : Window
{
    private const int InitialWindowWidth = 1920;
    private const int InitialWindowHeight = 1080;

    public CloudGameingViewModel ViewModel { get; private set; }
    public CloudGameSettingViewModel CloudSettingModel { get; }

    public CloudGameWindows(BrowserSessionLaunchOptions option)
    {
        InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        SetInitialWindowBounds();
        this.ViewModel = Instance.Host.Services.GetRequiredService<CloudGameingViewModel>();
        this.CloudSettingModel = Instance.Host.Services.GetRequiredService<CloudGameSettingViewModel>();
        ViewModel.SetWebView(this._browser, this, option);
        this.AppWindow.Closing += CloudGameWindows_Closing;
        this.grid.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        this._browser.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    private void SetInitialWindowBounds()
    {
        var displayArea = DisplayArea.GetFromWindowId(
            this.AppWindow.Id,
            DisplayAreaFallback.Primary
        );
        var workArea = displayArea.WorkArea;
        var x = workArea.X + Math.Max(0, (workArea.Width - InitialWindowWidth) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - InitialWindowHeight) / 2);

        this.AppWindow.Resize(
            new Windows.Graphics.SizeInt32
            {
                Width = InitialWindowWidth,
                Height = InitialWindowHeight,
            }
        );
        this.AppWindow.Move(new Windows.Graphics.PointInt32 { X = x, Y = y });
    }

    
    private async void CloudGameWindows_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
       
        ViewModel.ShowSystemCursor();
        this.ViewModel.Dispose();
        this.ViewModel = null;
        Close();
        GC.Collect();
    }

    private void TitleBar_PaneToggleRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        this.view.IsPaneOpen = !this.view.IsPaneOpen;
    }
}
