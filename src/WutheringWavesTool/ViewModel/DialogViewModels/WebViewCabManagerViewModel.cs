using Haiyu.Common.KuroWebView;
using Haiyu.Services.DialogServices;

namespace Haiyu.ViewModel.DialogViewModels;

/// <summary>
/// WebView 环境管理
/// </summary>
public partial class WebViewCabManagerViewModel : DialogViewModelBase
{
    public WebViewCabManagerViewModel(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IPickersService pickersService,
        IAppContext<App> appContext,
        ITipShow tipShow
    )
        : base(dialogManager)
    {
        PickersService = pickersService;
        AppContext = appContext;
        TipShow = tipShow;
        Runtimes = [];
        RegisterMessager();
    }

    public IPickersService PickersService { get; }
    public IAppContext<App> AppContext { get; }
    public ITipShow TipShow { get; }

    [ObservableProperty]
    public partial ObservableCollection<WebViewRuntimeWrapper> Runtimes { get; set; }

    [ObservableProperty]
    public partial string CurrentVersion { get; set; } = "—";

    [ObservableProperty]
    public partial double ZipArchiveProgress { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectCabFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplySelectCommand))]
    public partial bool IsImporting { get; set; }

    [ObservableProperty]
    public partial bool HasRuntimes { get; set; }

    private bool CanOperate() => !IsImporting;

    private void RegisterMessager()
    {
        WeakReferenceMessenger.Default.Register<DeleteWebViewRuntimeMessager>(
            this,
            DeleteRuntimeMethod
        );
    }

    private async void DeleteRuntimeMethod(object recipient, DeleteWebViewRuntimeMessager message)
    {
        if (IsImporting)
            return;

        await DeleteRuntimeAsync(message.Runtime);
    }

    [RelayCommand]
    private async Task LoadedAsync()
    {
        await LoadLocalWebViewCabEnvironmentAsync();
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task RefreshAsync()
    {
        await LoadLocalWebViewCabEnvironmentAsync();
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    public async Task SelectCabFilesAsync()
    {
        var cabFile = await PickersService.GetFileOpenPicker([".zip"]);
        if (cabFile is null || !File.Exists(cabFile.Path))
            return;

        var extractRoot = Path.Combine(AppSettings.WebViewFixRuntime, Guid.NewGuid().ToString("N"));
        IsImporting = true;
        ZipArchiveProgress = 0;

        try
        {
            IProgress<double> progress = new Progress<double>(value =>
            {
                AppContext.TryInvoke(() =>
                {
                    ZipArchiveProgress = Math.Clamp(value * 100d, 0d, 100d);
                });
            });

            await ZipArchiveHelper.UnZipFileAsync(
                cabFile.Path,
                extractRoot,
                progress,
                CTS.Token
            );

            var runtimeFolder = FindRuntimeFolder(extractRoot);
            if (runtimeFolder is null)
            {
                TryDeleteDirectory(extractRoot);
                await TipShow.ShowMessageAsync(
                    LanguageService.GetStringByText(
                        "未找到有效的 WebView2 固定运行时（缺少 msedgewebview2.exe）"
                    ),
                    Symbol.Clear
                );
                return;
            }

            await TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("WebView 固定运行时导入成功"),
                Symbol.Accept
            );
            await LoadLocalWebViewCabEnvironmentAsync();
        }
        catch (OperationCanceledException)
        {
            TryDeleteDirectory(extractRoot);
        }
        catch (Exception ex)
        {
            TryDeleteDirectory(extractRoot);
            Logger.WriteError($"导入 WebView 固定运行时失败: {ex}");
            await TipShow.ShowMessageAsync(
                LanguageService.FormatByText("导入失败: {0}", ex.Message),
                Symbol.Clear
            );
        }
        finally
        {
            IsImporting = false;
            ZipArchiveProgress = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ApplySelectAsync()
    {
        var selected = Runtimes.FirstOrDefault(x => x.IsSelect);
        if (selected is null)
        {
            await TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("请先选择一个 WebView 运行时"),
                Symbol.Clear
            );
            return;
        }

        foreach (var item in Runtimes)
        {
            if (!ReferenceEquals(item, selected))
                item.IsSelect = false;
        }

        await AppSettings.SetWebViewRuntimeModeAsync(selected.RuntimePath);
        CurrentVersion = selected.DisplayName;
        await TipShow.ShowMessageAsync(
            LanguageService.GetStringByText("已应用 WebView 运行时，重启相关页面后生效"),
            Symbol.Accept
        );
        await Close();
    }

    private async Task DeleteRuntimeAsync(WebViewRuntimeWrapper runtime)
    {
        if (runtime.IsEvergreen)
            return;

        if (!IsUnderFixRuntimeRoot(runtime.RuntimePath))
        {
            await TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("只能删除本工具导入的固定运行时"),
                Symbol.Clear
            );
            return;
        }

        try
        {
            var mode = await AppSettings.GetWebViewRuntimeModeAsync(CTS.Token) ?? "Evergreen";
            var wasSelected =
                mode.Equals(runtime.RuntimePath, StringComparison.OrdinalIgnoreCase)
                || runtime.IsSelect;

            var deleteRoot = ResolveImportRoot(runtime.RuntimePath);
            await Task.Run(
                () =>
                {
                    if (Directory.Exists(deleteRoot))
                        Directory.Delete(deleteRoot, true);
                },
                CTS.Token
            );

            if (wasSelected)
            {
                await AppSettings.SetWebViewRuntimeModeAsync("Evergreen");
            }

            await LoadLocalWebViewCabEnvironmentAsync();
            await TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("已删除 WebView 固定运行时"),
                Symbol.Accept
            );
        }
        catch (Exception ex)
        {
            Logger.WriteError($"删除 WebView 固定运行时失败: {ex}");
            await TipShow.ShowMessageAsync(
                LanguageService.FormatByText("删除失败: {0}", ex.Message),
                Symbol.Clear
            );
        }
    }

    public async Task LoadLocalWebViewCabEnvironmentAsync()
    {
        var selectedMode = await AppSettings.GetWebViewRuntimeModeAsync(CTS.Token) ?? "Evergreen";
        var evergreenVersion =
            CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "未安装";

        var items = new ObservableCollection<WebViewRuntimeWrapper>
        {
            new()
            {
                DisplayName = $"系统 WebView2（{evergreenVersion}）",
                RuntimePath = "Evergreen",
                IsSelect = selectedMode.Equals("Evergreen", StringComparison.OrdinalIgnoreCase),
            },
        };

        foreach (var item in WebView2EnvironmentProvider.GetFixedRuntimeFolders())
        {
            item.IsSelect = selectedMode.Equals(
                item.RuntimePath,
                StringComparison.OrdinalIgnoreCase
            );
            items.Add(item);
        }

        if (!items.Any(x => x.IsSelect))
        {
            items[0].IsSelect = true;
        }

        Runtimes = items;
        HasRuntimes = items.Count > 0;
        CurrentVersion =
            items.FirstOrDefault(x => x.IsSelect)?.DisplayName
            ?? WebView2EnvironmentProvider.GetSelectedRuntimeVersion();
    }

    private static string? FindRuntimeFolder(string extractRoot)
    {
        if (!Directory.Exists(extractRoot))
            return null;

        if (File.Exists(Path.Combine(extractRoot, "msedgewebview2.exe")))
            return extractRoot;

        foreach (
            var executable in Directory.EnumerateFiles(
                extractRoot,
                "msedgewebview2.exe",
                SearchOption.AllDirectories
            )
        )
        {
            return Path.GetDirectoryName(executable);
        }

        return null;
    }

    private static bool IsUnderFixRuntimeRoot(string runtimePath)
    {
        if (string.IsNullOrWhiteSpace(runtimePath))
            return false;

        var root = Path.GetFullPath(AppSettings.WebViewFixRuntime)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(runtimePath);
        return full.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            ) || full.Equals(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveImportRoot(string runtimePath)
    {
        var root = Path.GetFullPath(AppSettings.WebViewFixRuntime);
        var full = Path.GetFullPath(runtimePath);
        var relative = Path.GetRelativePath(root, full);
        var firstSegment = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        );
        return firstSegment.Length == 0 ? full : Path.Combine(root, firstSegment[0]);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // ignore cleanup failures
        }
    }


    public override void AfterClose()
    {
        Runtimes.Clear();
        base.AfterClose();
    }
}
