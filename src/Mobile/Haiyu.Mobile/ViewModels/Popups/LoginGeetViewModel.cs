using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using Haiyu.Mobile.Common;

namespace Haiyu.Mobile.ViewModels.Popups;

public partial class LoginGeetViewModel : ViewModelBase
{
    private readonly IPopupService _popupService;
    private int _isClosing;

    public LoginGeetViewModel(IPopupService popupService)
    {
        _popupService = popupService;
    }

    public WebView? WebView { get; private set; }

    [RelayCommand]
    public async Task Loaded(Microsoft.Maui.Controls.WebView webView)
    {
        this.WebView = webView;

        WebView.Navigating -= WebView_Navigating;
        WebView.Navigating += WebView_Navigating;
        await LoadGeetestPageAsync();
    }

    private async Task LoadGeetestPageAsync()
    {
        const string packagePath = "web/geet.html";

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(packagePath);
            using var reader = new StreamReader(stream);
            var html = await reader.ReadToEndAsync();
            WebView!.Source = new HtmlWebViewSource
            {
                Html = html,
                BaseUrl = "file:///android_asset/web/"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load geet.html failed: {ex}");
            WebView!.Source = new HtmlWebViewSource
            {
                Html = $"<html><body style='font-family:sans-serif;padding:16px'>" +
                       $"<h3>无法加载极验页面</h3>" +
                       $"<p>请确认 Resources/Raw/web/geet.html 已打包。</p>" +
                       $"<pre>{System.Net.WebUtility.HtmlEncode(ex.Message)}</pre>" +
                       $"</body></html>"
            };
        }
    }

    private void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        const string scheme = "haiyu://message/";
        if (!e.Url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;

        if (Interlocked.Exchange(ref _isClosing, 1) == 1)
            return;

        if (WebView is not null)
            WebView.Navigating -= WebView_Navigating;

        var json = Uri.UnescapeDataString(e.Url[scheme.Length..]);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Yield();
                var page = Shell.Current?.CurrentPage ?? Shell.Current;
                if (page is null)
                    return;
                await _popupService.ClosePopupAsync(page, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Close geet popup failed: {ex}");
                Interlocked.Exchange(ref _isClosing, 0);
            }
        });
    }
}
