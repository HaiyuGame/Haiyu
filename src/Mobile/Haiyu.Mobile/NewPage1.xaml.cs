using CommunityToolkit.Maui.Views;

namespace Haiyu.Mobile;

public partial class NewPage1 : Popup
{
	public NewPage1()
	{
		InitializeComponent();
		this.Loaded += NewPage1_Loaded;
	}

	private async void NewPage1_Loaded(object? sender, EventArgs e)
	{
		webView.Navigating += WebView_Navigating;
		await LoadGeetestPageAsync();
	}

	private async Task LoadGeetestPageAsync()
	{
		// MauiAsset: Resources/Raw/web/** → app package as "web/..."
		const string packagePath = "web/geet.html";

		try
		{
			// Verify the file was packaged (throws FileNotFoundException if missing)
			await using var stream = await FileSystem.OpenAppPackageFileAsync(packagePath);
			using var reader = new StreamReader(stream);
			var html = await reader.ReadToEndAsync();

			// BaseUrl must point at the folder containing geet.html so ./Js/*.js resolve.
			// On Android this maps to file:///android_asset/web/
			webView.Source = new HtmlWebViewSource
			{
				Html = html,
#if ANDROID
				BaseUrl = "file:///android_asset/web/"
#else
				BaseUrl = "web/"
#endif
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Load geet.html failed: {ex}");
			webView.Source = new HtmlWebViewSource
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
		var json = Uri.UnescapeDataString(e.Url[scheme.Length..]);
		System.Diagnostics.Debug.WriteLine($"Geetest message: {json}");
	}
}
