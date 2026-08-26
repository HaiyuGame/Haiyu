namespace Haiyu.WindowModels;

public sealed partial class GetGeetWindow : WindowModelBase
{
    public GeetType Type { get; }

    public GetGeetWindow(nint value, GeetType type, WindowsOption? windowsOption = null)
        : base(value, windowsOption)
    {
        this.InitializeComponent();
        this.titleBar.Window = this;
        Type = type;
        this.webView2.NavigationCompleted += WebView2_NavigationCompleted;
        this.webView2.Loaded += WebView2_Loaded;

        this.grid.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    protected override void OnDetaching()
    {
        this.webView2.NavigationCompleted -= WebView2_NavigationCompleted;
        try
        {
            this.webView2?.Close();
        }
        catch
        {
        }
        if (titleBar is not null)
            titleBar.Window = null;
    }

    private async void WebView2_Loaded(object sender, RoutedEventArgs e)
    {
        this.webView2.Loaded -= WebView2_Loaded;
        await global::Haiyu.Common.KuroWebView.WebView2EnvironmentProvider.EnsureInitializedAsync(
            this.webView2
        );

        this.webView2.Source = Type switch
        {
            GeetType.Login => new(AppDomain.CurrentDomain.BaseDirectory + "Assets\\geet.html"),
            _ => null,
        };
    }

    private void WebView2_NavigationCompleted(
        WebView2 sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args
    )
    {
        sender.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        sender.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
    }

    private void CoreWebView2_WebMessageReceived(
        Microsoft.Web.WebView2.Core.CoreWebView2 sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args
    )
    {
        try
        {
            WeakReferenceMessenger.Default.Send<GeeSuccessMessanger>(
                new(args.TryGetWebMessageAsString(), Type)
            );
            this.webView2.Close();
            this.Close();
        }
        catch (Exception)
        {
            return;
        }
    }
}
