using ABI.System;
using Haiyu.Common.KuroWebView;

namespace Haiyu.WindowModels;

public sealed partial class KuroDataCenterWindow : Window
{
    KuroCommunityWebViewHostInitializer hostInitializer;

    public KuroDataCenterWindow(WebSessionContext context, WindowsOption? windowsOption = null)
    {
        InitializeComponent();
        this.ApplyWindowsOption(windowsOption);
        this.titleBar.Window = this;
        this.AppWindow.Closing += AppWindow_Closing;
        Context = context;
    }

    public WebSessionContext Context { get; }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (webView2 != null)
        {
            webView2.Close();
        }
        this.AppWindow.Closing -= AppWindow_Closing;
    }

    private async void grid_Loaded(object sender, RoutedEventArgs e)
    {
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
