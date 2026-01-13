using System.Security.Principal;
using Haiyu.Plugin.Extensions;
using Waves.Core.Settings;
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

    void GetAllVersion()
    {
        WebViewVersion = CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "未安装";
        this.WindowsAppSdkVersion =
            $"1.8.251106002";
        this.RunType = RuntimeFeature.IsDynamicCodeCompiled ? "JIT" : "AOT";
        this.FrameworkType = RuntimeInformation.FrameworkDescription;
    }

    [RelayCommand]
    void SetRpcToken()
    {
        if (string.IsNullOrWhiteSpace(this.RpcToken))
        {
            TipShow.ShowMessage("密钥不能为空", Symbol.Clear);
            return;
        }
        AppSettings.RpcToken = Md5Helper.ComputeMd532(RpcToken);
        TipShow.ShowMessage("密钥已经更新", Symbol.Accept);
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
        if (
            Directory.Exists(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Haiyu.exe.WebView2")
            )
        )
        {
            await Task.Run(() =>
            {
                Directory.Delete(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Haiyu.exe.WebView2"),
                    true
                );
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
                "Haiyu"
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
                CreateUwpShortcut(saveDialog.Path, $"shell:AppsFolder\\{haiyu.Id.FamilyName}!App");
                await TipShow.ShowMessageAsync("桌面图标创建成功", Symbol.Accept);
            }
        }
        catch (Exception ex)
        {
            await TipShow.ShowMessageAsync($"桌面图标创建异常:{ex.Message}", Symbol.Clear);
        }
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
                throw new Exception($"PowerShell执行失败: {error}");

            return output.Trim();
        }
    }
}
