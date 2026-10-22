using GrayExtension;
using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Waves.Core.Models.Options;
using Waves.Core.Services.GameResourceProvider;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class SelectDownloadGameViewModelV2 : DialogViewModelBase
{
    public SelectDownloadGameViewModelV2(
        DialogSession dialogSession,
        IWindowManager windowManager,
        GrayOpenService grayOpenService
    )
        : base(dialogSession)
    {
        Pickers = windowManager.Shell.PickersService;
        GrayOpenService = grayOpenService;
    }

    private GameResourceParameter? Parameter;
    public IGameContextV2 GameContext { get; private set; } = null!;
    public IPickersService Pickers { get; }
    public GameResourceSummary? Launcher { get; private set; }

    [ObservableProperty]
    public partial string DiskSpaceText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool IsBusy { get; set; }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            var result = await TryInvokeAsync(async () =>
            {
                await operation();
                return true;
            });
            if (result.Code == -2)
                TipMessage = result.Message ?? LanguageService.GetString("GameInstall_ReadFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [ObservableProperty]
    public partial string FolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TipMessage { get; set; } =
        LanguageService.GetString("GameDownload_CheckDiskSpace");

    private bool CanStartDownload() => CanDownload && !IsBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool CanDownload { get; set; }

    [ObservableProperty]
    public partial double MaxValue { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<LayerData> BarValues { get; set; } = new();

    [ObservableProperty]
    public partial Visibility DiskVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility BundleVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial ObservableCollection<BunleResourceSize> Bundles { get; set; } = new();

    [ObservableProperty]
    public partial BunleResourceSize? SelectedBundle { get; set; }
    public GrayOpenService GrayOpenService { get; }

    partial void OnFolderPathChanged(string value)
    {
        CanDownload = false;
        DiskVisibility = Visibility.Collapsed;
    }

    partial void OnSelectedBundleChanged(BunleResourceSize? value) => CanDownload = true;

    internal void SetData(object data)
    {
        if (data is GameFolderDialogRequest request)
        {
            Parameter = request.Parameter;
            data = request.ContextType;
        }
        if (data is Type type)
        {
            var name = type.Name;
            this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(name);
        }
    }

    [RelayCommand]
    private Task Cancel() =>
        CloseAsync(
            new SelectDownloadFolderResult
            {
                InstallFolder = FolderPath,
                Result = ContentDialogResult.None,
            }
        );

    [RelayCommand(CanExecute = nameof(CanStartDownload))]
    private Task Download() =>
        RunBusyAsync(async () =>
        {
            await RefreshDiskCoreAsync();
            if (!CanDownload || Launcher is null || IsDisposed)
                return;
            await CloseAsync(
                new SelectDownloadFolderResult
                {
                    InstallFolder = FolderPath,
                    Result = ContentDialogResult.Primary,
                    Summary = Launcher,
                    Parameter = Parameter,
                }
            );
        });

    [RelayCommand]
    private async Task SelectFolder()
    {
        var folderPath = await Pickers.GetFolderPicker();
        if (folderPath == null)
            return;
        if (!Directory.Exists(folderPath.Path))
        {
            return;
        }
        FolderPath = folderPath.Path;
        await RefreshDiskAsync();
    }

    [RelayCommand]
    Task RefreshDiskAsync() => RunBusyAsync(RefreshDiskCoreAsync);

    private async Task RefreshDiskCoreAsync()
    {
        CanDownload = false;
        DiskVisibility = Visibility.Collapsed;
        if (
            SelectedBundle is BunleResourceSize size
            && Bundles is ObservableCollection<BunleResourceSize> bunles
        )
        {
            this.Parameter = new GameResourceParameter()
            {
                BundleName = size.BunleName,
                TargetPackVersions = bunles.ToDictionary(x => x.BunleName, x => x.Version),
            };
        }
        string? rootPath = IsInstallDirectory(FolderPath) ? Path.GetPathRoot(FolderPath) : null;
        DriveInfo? selectedDrive = DriveInfo
            .GetDrives()
            .FirstOrDefault(drive =>
                drive.Name.Equals(rootPath, StringComparison.OrdinalIgnoreCase)
            );
        var isVild = !IsInstallDirectory(FolderPath);
        if (isVild)
        {
            TipMessage = LanguageService.GetStringByText("请选择一个文件夹，而并非一个磁盘");
            CanDownload = false;
            return;
        }
        if (selectedDrive == null || !selectedDrive.IsReady)
        {
            TipMessage = LanguageService.GetString("GameDownload_SelectValidFolder");
            return;
        }
        double totalSizeMB = (double)selectedDrive.TotalSize / (1024 * 1024 * 1024);
        double freeSpaceMB = (double)selectedDrive.TotalFreeSpace / (1024 * 1024 * 1024);
        double usedSpaceMB = totalSizeMB - freeSpaceMB;
        DiskSpaceText = string.Format(
            LanguageService.GetString("GameInstall_ShowAvailableSpace"),
            freeSpaceMB,
            totalSizeMB
        );
        MaxValue = totalSizeMB;
        var checkedPath = FolderPath;
        var checkedBundle = SelectedBundle;
        Launcher = await this.GameContext.GetResourceSummaryAsync(Parameter, CTS.Token);
        if (IsDisposed || checkedPath != FolderPath || checkedBundle != SelectedBundle)
            return;
        if (Launcher == null)
        {
            TipMessage = LanguageService.GetStringByText("数据拉取失败");
            return;
        }
        DiskVisibility = Visibility.Visible;
        var updateSize = usedSpaceMB + Launcher.Install.DownloadSize / 1024d / 1024 / 1024;
        BarValues = new ObservableCollection<LayerData>()
        {
            new LayerData()
            {
                Label = LanguageService.GetStringByText("总容量"),
                Color = new SolidColorBrush(Colors.LightGreen),
                Value = totalSizeMB,
            },
            new LayerData()
            {
                Label = LanguageService.GetStringByText("已用容量"),
                Color = new SolidColorBrush(Colors.Purple),
                Value = usedSpaceMB,
            },
            new LayerData()
            {
                Label = LanguageService.GetStringByText("下载后增量"),
                Color = new SolidColorBrush(Colors.Red),
                Value = updateSize,
            },
        };
        if (selectedDrive.TotalFreeSpace < Launcher.Install.RequiredSpace)
        {
            TipMessage = LanguageService.GetStringByText("空间不足，请清理一些文件进行下载");
            CanDownload = false;
            return;
        }
        else
        {
            TipMessage = LanguageService.FormatByText(
                LanguageService.GetStringByText("本次更新大小约为{0}GB"),
                Math.Round(Launcher.Install.DownloadSize / 1024d / 1024 / 1024, 2)
            );
            CanDownload = true;
        }
    }

    private static bool IsInstallDirectory(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Path.IsPathFullyQualified(folderPath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            var rootPath = Path.GetPathRoot(fullPath);
            return !string.IsNullOrEmpty(rootPath)
                && !string.Equals(
                    Path.TrimEndingDirectorySeparator(fullPath),
                    Path.TrimEndingDirectorySeparator(rootPath),
                    StringComparison.OrdinalIgnoreCase
                )
                && Directory.Exists(fullPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    [RelayCommand]
    private Task Loaded() => RunBusyAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var bunleGray = this.GrayOpenService.GetOpen((uint)GrayConfigId.WavesGameBunleOpen, false);
        if (
            !this.GameContext.IsBunle
            || !bunleGray
            || this.GameContext.GameResourceProvider is not BundleGameResourceProvider p
        )
        {
            BundleVisibility = Visibility.Collapsed;
            return;
        }
        BundleVisibility = Visibility.Visible;
        var bunleNames = await p.GetBunlesAsync();
        Bundles = bunleNames;
        this.Parameter = await p.GetLocalGameReosurceParamter();
        SelectedBundle =
            bunleNames.FirstOrDefault(x => x.BunleName == Parameter.BundleName)
            ?? bunleNames.FirstOrDefault();
    }
}
