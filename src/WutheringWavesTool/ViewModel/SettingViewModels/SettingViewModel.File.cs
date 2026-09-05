using System.Security.Principal;
using Haiyu.Common.KuroWebView;
using Haiyu.Models.Wrapper;
using Haiyu.Plugin.Extensions;
using Microsoft.WindowsAppSDK;
using Waves.Core.Common;
using Waves.Settings;
using Windows.Management.Deployment;

namespace Haiyu.ViewModel;

partial class SettingViewModel
{
    [ObservableProperty]
    public partial string WebViewVersion { get; set; }

    [ObservableProperty]
    public partial string WindowsAppSdkVersion { get; set; }

    [ObservableProperty]
    public partial string RunType { get; set; }

    [ObservableProperty]
    public partial string FrameworkType { get; set; }

    [ObservableProperty]
    public partial string RpcToken { get; set; }

    [ObservableProperty]
    public partial WebViewRuntimeWrapper? WebViewRuntimeItem { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<WebViewRuntimeWrapper> WebViewRuntimeOptions { get; set; } =
    [];

    private bool _webViewRuntimeLoaded;

    async Task GetAllVersionAsync()
    {
        WindowsAppSdkVersion = Microsoft.WindowsAppSDK.Runtime.Version.DotQuadString;
        RunType = RuntimeFeature.IsDynamicCodeCompiled ? "JIT" : "AOT";
        FrameworkType = RuntimeInformation.FrameworkDescription;
        await LoadWebViewRuntimeModeAsync();
    }

    private async Task LoadWebViewRuntimeModeAsync()
    {
        var mode = await AppSettings.GetWebViewRuntimeModeAsync(CTS.Token) ?? "Evergreen";
        var evergreen = CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "未安装";

        var options = new ObservableCollection<WebViewRuntimeWrapper>
        {
            new() { DisplayName = $"System（{evergreen}）", RuntimePath = "Evergreen" },
        };

        foreach (var item in WebView2EnvironmentProvider.GetFixedRuntimeFolders())
        {
            options.Add(item);
        }
        WebViewRuntimeOptions = options;
        WebViewRuntimeItem =
            WebViewRuntimeOptions.FirstOrDefault(x =>
                x.RuntimePath.Equals(mode, StringComparison.OrdinalIgnoreCase)
            ) ?? WebViewRuntimeOptions.FirstOrDefault();

        WebViewVersion =
            WebViewRuntimeItem?.DisplayName
            ?? WebView2EnvironmentProvider.GetSelectedRuntimeVersion();
        _webViewRuntimeLoaded = true;
    }

    async partial void OnWebViewRuntimeItemChanged(WebViewRuntimeWrapper? value)
    {
        if (!_webViewRuntimeLoaded || value is null)
        {
            return;
        }

        await AppSettings.SetWebViewRuntimeModeAsync(value.RuntimePath);
        WebViewVersion = value.DisplayName;
    }

    [RelayCommand]
    async Task SetRpcToken()
    {
        if (string.IsNullOrWhiteSpace(this.RpcToken))
        {
            await WindowManager.Shell.TipShow.ShowMessageAsync(LanguageService.GetStringByText("密钥不能为空"), Symbol.Clear);
            return;
        }
        await RpcSettings.SetAuthTokenAsync(Md5Helper.ComputeMd532(RpcToken));
        await WindowManager.Shell.TipShow.ShowMessageAsync(LanguageService.GetStringByText("密钥已经更新"), Symbol.Accept);
    }

    [RelayCommand]
    void OpenConfigFolder()
    {
        WindowExtension.ShellExecute(
            IntPtr.Zero,
            "open",
            AppSettings.BassFolder,
            null,
            null,
            WindowExtension.SW_SHOWNORMAL
        );
    }

    [RelayCommand]
    async Task DeleteWebCacheCommand()
    {
        if (Directory.Exists(AppSettings.WebCacheFolder))
        {
            await Task.Run(() =>
            {
                Directory.Delete(AppSettings.WebCacheFolder, true);
            });
        }
    }

    [RelayCommand]
    void OpenCaptureFolder()
    {
        WindowExtension.ShellExecute(
            IntPtr.Zero,
            "open",
            AppSettings.ScreenCaptures,
            null,
            null,
            WindowExtension.SW_SHOWNORMAL
        );
    }

    [RelayCommand]
    async Task CreateLink()
    {
        try
        {
            var saveDialog = await PickersService.GetFileSavePicker(
                new List<string>() { ".lnk" },
                "Haiyu",
                WindowManager.Shell.GetWindow().GetWindowHandle()
            );
            if (saveDialog != null)
            {
                if (File.Exists(saveDialog.Path))
                {
                    File.Delete(saveDialog.Path);
                }
                PackageManager packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser(
                    WindowsIdentity.GetCurrent().User!.Value
                );
                var haiyu = packages.Where(x => x.DisplayName.Contains("Haiyu")).FirstOrDefault();
                if (haiyu == null)
                {
                    await WindowManager.Shell.TipShow.ShowMessageAsync(
                        LanguageService.GetStringByText("当前应用程序为独立模式，无法创建桌面图标"),
                        Symbol.Accept
                    );
                    return;
                }
                CreateUwpShortcut(saveDialog.Path, $"shell:AppsFolder\\{haiyu.Id.FamilyName}!App");
                await WindowManager.Shell.TipShow.ShowMessageAsync(
                    LanguageService.GetStringByText("桌面图标创建成功"),
                    Symbol.Accept
                );
            }
        }
        catch (Exception ex)
        {
            await WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.FormatByText(
                    LanguageService.GetStringByText("桌面图标创建异常:{0}"),
                    ex.Message
                ),
                Symbol.Clear
            );
        }
    }

    [RelayCommand]
    async Task OpenWebViewCabDialog()
    {
        await this.WindowManager.Shell.DialogManager.ShowWebViewCabManangerAsync();
        _webViewRuntimeLoaded = false;
        await LoadWebViewRuntimeModeAsync();
    }

    public static string CreateUwpShortcut(string filePath, string target)
    {
        string psScript =
            $@"
                $target = '{target}'
                $shortcutPath = '{filePath}'
                $shell = New-Object -ComObject WScript.Shell
                $shortcut = $shell.CreateShortcut($shortcutPath)
                $shortcut.TargetPath = $target
                $shortcut.Description = 'Haiyu'
                $shortcut.Save()
                Write-Output $shortcutPath";
        using (Process process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // 3. 检查执行结果
            if (process.ExitCode != 0)
                throw new Exception(
                    LanguageService.FormatByText(
                        LanguageService.GetStringByText("PowerShell执行失败: {0}"),
                        error
                    )
                );

            return output.Trim();
        }
    }
}
