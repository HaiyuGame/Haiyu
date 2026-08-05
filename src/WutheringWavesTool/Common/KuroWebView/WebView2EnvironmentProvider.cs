using Waves.Settings;

namespace Haiyu.Common.KuroWebView;

/// <summary>
/// Shared WebView2 environment.
/// DEBUG (or HAIYU_WEBVIEW_CDP_PORT) enables Chromium remote debugging so external
/// CDP tools can attach — Haiyu itself does not speak CDP.
///
/// Attach from outside:
///   http://127.0.0.1:{port}/json/list
///   Edge: edge://inspect  →  Configure  →  localhost:{port}
///   Chrome: chrome://inspect
/// Env: HAIYU_WEBVIEW_CDP_PORT=9223  |  0 to disable
/// </summary>
public static class WebView2EnvironmentProvider
{
    public const int DefaultCdpPort = 9223;

    private static readonly Lazy<
        Task<Microsoft.Web.WebView2.Core.CoreWebView2Environment>
    > EnvironmentTask = new(CreateEnvironmentAsync);

    /// <summary>Remote-debugging port, or 0 when disabled.</summary>
    public static int CdpPort { get; private set; }
    private static string _selectedRuntimeMode = "Evergreen";

    public static string GetSelectedRuntimeVersion()
    {
        var fixedFolder = ResolveBrowserExecutableFolder(_selectedRuntimeMode);
        if (fixedFolder is not null)
        {
            var executable = Path.Combine(fixedFolder, "msedgewebview2.exe");
            return FileVersionInfo.GetVersionInfo(executable).FileVersion
                ?? Path.GetFileName(fixedFolder);
        }

        return CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "未安装";
    }

    public static IReadOnlyList<string> GetFixedRuntimeFolders()
    {
        var roots = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Waves",
                "WebView2FixedRuntime"
            )
        };

        var result = new List<string>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var executable in Directory.EnumerateFiles(root, "msedgewebview2.exe", SearchOption.AllDirectories))
            {
                var folder = Path.GetDirectoryName(executable);
                if (folder is not null && !result.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(folder);
                }
            }
        }

        return result;
    }

    public static async Task EnsureInitializedAsync(WebView2 webView)
    {
        ArgumentNullException.ThrowIfNull(webView);

        var environment = await EnvironmentTask.Value;
        await webView.EnsureCoreWebView2Async(environment);
    }

    private static async Task<Microsoft.Web.WebView2.Core.CoreWebView2Environment> CreateEnvironmentAsync()
    {
        Directory.CreateDirectory(AppSettings.WebCacheFolder);
        CdpPort = ResolveCdpPort();
        var args = new List<string>();
        if (CdpPort > 0)
        {
            args.Add($"--remote-debugging-port={CdpPort}");
            args.Add("--remote-allow-origins=*");
        }
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = string.Join(' ', args)
        };

        var settings = Instance.Host.Services.GetRequiredService<AppSettings>();
        _selectedRuntimeMode = await settings.GetWebViewRuntimeModeAsync() ?? "Evergreen";
        var browserExecutableFolder = ResolveBrowserExecutableFolder(_selectedRuntimeMode);
        System.Diagnostics.Debug.WriteLine(
            $"[WebView2] runtime={(browserExecutableFolder is null ? "Evergreen" : browserExecutableFolder)}"
        );

        return await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateWithOptionsAsync(
            browserExecutableFolder: browserExecutableFolder,
            userDataFolder: AppSettings.WebCacheFolder,
            options: options
        );
    }

    private static string? ResolveBrowserExecutableFolder(string mode)
    {
        var configured = Environment.GetEnvironmentVariable("HAIYU_WEBVIEW2_FIXED_RUNTIME");
        if (IsRuntimeFolder(configured))
        {
            return configured;
        }

        if (mode.Equals("Evergreen", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (IsRuntimeFolder(mode))
        {
            return mode;
        }

        var packaged = Path.Combine(AppContext.BaseDirectory, "WebView2FixedRuntime");
        if (IsRuntimeFolder(packaged))
        {
            return packaged;
        }

        return null;
    }

    private static bool IsRuntimeFolder(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && File.Exists(Path.Combine(path, "msedgewebview2.exe"));

    private static int ResolveCdpPort()
    {
        var raw = Environment.GetEnvironmentVariable("HAIYU_WEBVIEW_CDP_PORT");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (
                raw.Equals("0", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("false", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("off", StringComparison.OrdinalIgnoreCase)
            )
            {
                return 0;
            }

            return int.TryParse(raw, out var port) && port > 0 && port < 65536
                ? port
                : DefaultCdpPort;
        }

        // Default off even in DEBUG — last crash had CDP attached; enable explicitly when needed.
        return 0;
    }
}
