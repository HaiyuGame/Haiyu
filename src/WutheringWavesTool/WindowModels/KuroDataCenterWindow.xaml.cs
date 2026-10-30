using ABI.System;
using Haiyu.Common.Contracts;
using Haiyu.Common.KuroWebView;

namespace Haiyu.WindowModels;

public sealed partial class KuroDataCenterWindow : Window, IWindowInitializable
{
    KuroCommunityWebViewHostInitializer hostInitializer;

    public KuroDataCenterWindow(WindowSession session)
    {
        InitializeComponent();
        this.titleBar.Window = this;
        grid.RequestedTheme = Instance
            .Host.Services.GetRequiredService<IThemeService>()
            .CurrentTheme;
        Session = session;
    }

    public WebSessionContext Context { get; private set; } = null!;
    public WindowSession Session { get; }

    public void Dispose()
    {
        this.Bindings.StopTracking();
        webView2?.Close();
    }

    public void Initialize()
    {
        Context = Session.GetParameter<WebSessionContext>();
    }

    private async void grid_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.Content is FrameworkElement element)
        {
            element.RequestedTheme = Instance
                .Host.Services.GetRequiredService<IThemeService>()
                .CurrentTheme;
        }
        hostInitializer = new KuroCommunityWebViewHostInitializer();
        await hostInitializer.InitializeAsync(webView2, Context);
        this.webView2.CoreWebView2.Navigate(Context.GetPageUrl());
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
    }

    private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem item)
        {
            return;
        }
        if (this.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = item.IsChecked;
        }
    }

    private void ToggleMenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem item)
        {
            return;
        }
        if (this.AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }
        if (item.IsChecked)
        {
            titleBar.Visibility = Visibility.Collapsed;
            Grid.SetRow(content, 0);
            Grid.SetRowSpan(content, 2);
            presenter.SetBorderAndTitleBar(true, false);
        }
        else
        {
            titleBar.Visibility = Visibility.Visible;
            Grid.SetRow(content, 1);
            Grid.SetRowSpan(content, 1);
            presenter.SetBorderAndTitleBar(true, true);
        }
    }
}
