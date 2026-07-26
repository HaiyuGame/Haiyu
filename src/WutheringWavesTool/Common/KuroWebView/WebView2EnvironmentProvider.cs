using Waves.Core.Settings;

namespace Haiyu.Common.KuroWebView;

public static class WebView2EnvironmentProvider
{
    private static readonly Lazy<
        Task<Microsoft.Web.WebView2.Core.CoreWebView2Environment>
    > EnvironmentTask = new(CreateEnvironmentAsync);

    public static async Task EnsureInitializedAsync(WebView2 webView)
    {
        ArgumentNullException.ThrowIfNull(webView);

        var environment = await EnvironmentTask.Value;
        await webView.EnsureCoreWebView2Async(environment);
    }

    private static async Task<Microsoft.Web.WebView2.Core.CoreWebView2Environment> CreateEnvironmentAsync()
    {
        Directory.CreateDirectory(AppSettings.WebCacheFolder);
        return await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateWithOptionsAsync(
            null,
            AppSettings.WebCacheFolder,
            null
        );
    }
}
