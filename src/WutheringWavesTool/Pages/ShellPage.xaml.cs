using System.Collections.Specialized;
using Haiyu.Common.WindowContext;
using Haiyu.Pages.GamePages;
using Microsoft.UI.Xaml.Hosting;

namespace Haiyu.Pages;

public sealed partial class ShellPage : Page, IDisposable
{
    private bool _disposed;

    public ShellPage(ShellViewModel viewModel)
    {
        this.InitializeComponent();
        this.ViewModel = viewModel;
        this.Loaded += ShellPage_Loaded;
        this.ViewModel.HomeNavigationService.Navigated += HomeNavigationService_Navigated;
        this.ViewModel.HomeNavigationService.RegisterView(this.frame);
        this.ViewModel.HomeNavigationViewService.Register(this.navigationView);
        this.ViewModel.WindowManager.Shell.TipShow.Owner = this.panel;
        if(this.ViewModel.WindowManager.Shell is ShellWindowContext shell) 
        {
            shell.MainTitle = this.titlebar;
        }
        this.ViewModel.AppContext.WallpaperService.RegisterMediaHost(mediaControl);
    }

    private void HomeNavigationService_Navigated(object sender, NavigationEventArgs e)
    {
        if (
            e.SourcePageType == typeof(PunishV2GamePage)
            || e.SourcePageType == typeof(WavesV2GamePage)
            ||e.SourcePageType == typeof(WavesCloudGamePage)
        )
        {
            To0.Start();
            this.titlebar.UpDate();
        }
        else
        {
            To8.Start();
            this.titlebar.UpDate();
            this.ViewModel.WallpaperService.PauseVideo();
        }
        ViewModel.SetSelectItem(e.SourcePageType);
        this.ViewModel.HomeNavigationService.ClearHistory();
        GC.Collect();
    }

    public ShellViewModel ViewModel { get; }

    private void ShellPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        this.ViewModel.WindowManager.Shell.DialogManager.RegisterRoot(this.XamlRoot);
    }

    private void ComboBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        this.titlebar.UpDate();
    }

    private void notify_LeftDoubleClick(object sender, EventArgs args)
    {
        this.ViewModel.ShowWindowCommand.Execute(null);
    }

    private void OpenMessagePane(object sender, RoutedEventArgs e)
    {
        view.IsPaneOpen = !view.IsPaneOpen;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Loaded -= ShellPage_Loaded;
        ViewModel.HomeNavigationService.Navigated -= HomeNavigationService_Navigated;
        ViewModel.HomeNavigationService.UnRegisterView();
        ViewModel.HomeNavigationViewService.UnRegister();
        ViewModel.AppContext.WallpaperService.UnregisterMediaHost(mediaControl);
        Bindings.StopTracking();
    }

    private async void Page_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {

        if (e.Key != Windows.System.VirtualKey.F12)
            return;

        e.Handled = true;
        await ViewModel.OpenDevShellDialogCommand.ExecuteAsync(e);
    }
}
