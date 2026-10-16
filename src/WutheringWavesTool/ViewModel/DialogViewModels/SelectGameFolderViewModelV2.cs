using System.Text.RegularExpressions;
using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Waves.Core.Models.Options;
using Waves.Core.Services.GameResourceProvider;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class SelectGameFolderViewModelV2 : DialogViewModelBase
{
    public SelectGameFolderViewModelV2(
        DialogSession dialogSession,
        IWindowManager windowManager
    )
        : base(dialogSession)
    {
        PickersService = this.AppContext.WindowManager.Shell.PickersService;
        WindowManager = windowManager;
    }

    public GameResourceParameter? Parameter { get; private set; }
    public IGameContextV2 GameContext { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartVerifyCommand), nameof(SelectVersionCommand))]
    public partial bool HasBundleSelection { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<BunleResourceSize> Bundles { get; set; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartVerifyCommand), nameof(SelectVersionCommand))]
    public partial BunleResourceSize? SelectedBundle { get; set; }

    partial void OnSelectedBundleChanged(BunleResourceSize? value)
    {
        if (value is null)
            return;
        Parameter = new GameResourceParameter
        {
            BundleName = value.BunleName,
            TargetPackVersions = Bundles.ToDictionary(x => x.BunleName, x => x.Version),
        };
    }

    [ObservableProperty]
    public partial string DiskSpaceText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartVerifyCommand), nameof(SelectVersionCommand))]
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
    public partial string ExePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TipMessage { get; set; } =
        LanguageService.GetStringByText("选择目标程序，以查看驱动器详情");

    private bool CanImport() => IsVerify && !IsBusy
        && !string.IsNullOrWhiteSpace(SelectedVersion)
        && (!HasBundleSelection || SelectedBundle is not null);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartVerifyCommand), nameof(SelectVersionCommand))]
    public partial bool IsVerify { get; set; }

    partial void OnExePathChanged(string value) => IsVerify = false;

    public bool GetIsVerify() => IsVerify;

    [ObservableProperty]
    public partial ObservableCollection<LayerData> BarValues { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<string> Versions { get; set; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartVerifyCommand), nameof(SelectVersionCommand))]
    public partial string SelectedVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double MaxValue { get; set; }
    public IPickersService PickersService { get; }
    public IWindowManager WindowManager { get; }
    public GameResourceSummary? Launcher { get; internal set; }

    [RelayCommand]
    async Task SelectGameProgram()
    {
        var exe = await PickersService.GetFileOpenPicker(
            [".exe"]
        );
        if (exe == null)
            return;
        if (System.IO.Path.GetFileName(exe.Path) != GameContext.Config.GameExeName)
        {
            TipMessage = LanguageService.GetStringByText("无效地址");
            return;
        }
        this.ExePath = exe.Path;
        await RefreshDiskAsync();
    }

    [RelayCommand]
    Task RefreshDiskAsync() => RunBusyAsync(RefreshDiskCoreAsync);

    private async Task RefreshDiskCoreAsync()
    {
        IsVerify = false;
        if (!File.Exists(ExePath)
            || !string.Equals(Path.GetFileName(ExePath), GameContext.Config.GameExeName, StringComparison.OrdinalIgnoreCase))
        {
            TipMessage = LanguageService.GetString("GameImport_SelectValidExecutable");
            return;
        }
        var checkedPath = ExePath;
        var folderPath = System.IO.Path.GetDirectoryName(ExePath);
        var directoryInfo = new DirectoryInfo(folderPath);
        var folderSizeBytes = await CalculateFolderSizeAsync(directoryInfo);
        var folderSizeGB = BytesToGigabytes(folderSizeBytes);
        var rootPath = System.IO.Path.GetPathRoot(ExePath);
        var driveInfo = GetDriveInfo(rootPath);

        if (driveInfo == null)
        {
            TipMessage = LanguageService.FormatByText(
                LanguageService.GetStringByText("无法找到对应驱动器: {0}"),
                rootPath
            );
            return;
        }

        Launcher = await this.GameContext.GetResourceSummaryAsync(Parameter, this.CTS.Token);
        if (IsDisposed || checkedPath != ExePath)
            return;
        if (Launcher == null)
        {
            TipMessage = LanguageService.FormatByText(
                LanguageService.GetStringByText("游戏数据拉取失败")
            );
            return;
        }

        var totalSpaceGB = BytesToGigabytes(driveInfo.TotalSize);
        var freeSpaceGB = BytesToGigabytes(driveInfo.TotalFreeSpace);
        DiskSpaceText = string.Format(LanguageService.GetString("GameInstall_ShowAvailableSpace"), freeSpaceGB, totalSpaceGB);
        this.MaxValue = totalSpaceGB;
        this.BarValues = new ObservableCollection<LayerData>([
            new LayerData()
            {
                Label = LanguageService.GetStringByText("总容量"),
                Color = new SolidColorBrush(Colors.LightGreen),
                Value = totalSpaceGB,
            },
            new LayerData()
            {
                Label = LanguageService.GetStringByText("当前游戏文件夹容量"),
                Color = new SolidColorBrush(Colors.Purple),
                Value = totalSpaceGB - freeSpaceGB,
            },
            new LayerData()
            {
                Label = LanguageService.GetStringByText("占用容量"),
                Color = new SolidColorBrush(Colors.MediumPurple),
                Value = totalSpaceGB - freeSpaceGB - folderSizeGB,
            },
        ]);
        TipMessage = LanguageService.GetString("GameImport_GameDetected");
        IsVerify = true;
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    Task StartVerify() => RunBusyAsync(async () =>
    {
        if (!IsVerify)
            return;
        var bundleConfig = await GetBundleConfigAsync();
        if (bundleConfig.Count > 0
            && !await GameContext.GameLocalConfig.SaveConfigsAsync(bundleConfig, CTS.Token))
        {
            TipMessage = LanguageService.GetString("GameImport_SaveTextureFailed");
            return;
        }
        await CloseAsync(new SelectDownloadFolderResult
        {
            InstallFolder = Path.GetDirectoryName(ExePath)!,
            Result = ContentDialogResult.Primary,
            Summary = Launcher,
            Parameter = Parameter,
        });
    });

    [RelayCommand]
    Task Loaded() => RunBusyAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        HasBundleSelection = GameContext.IsBunle
            && GameContext.GameResourceProvider is BundleGameResourceProvider;
        if (HasBundleSelection && GameContext.GameResourceProvider is BundleGameResourceProvider provider)
        {
            Bundles = await provider.GetBunlesAsync();
            var bundleName = Parameter?.BundleName
                ?? await GameContext.GameLocalConfig.GetConfigAsync(GameLocalSettingName.BunleName, CTS.Token)
                ?? GameContext.Config.BunleDefaultBundleName;
            SelectedBundle = Bundles.FirstOrDefault(x => string.Equals(x.BunleName, bundleName, StringComparison.OrdinalIgnoreCase))
                ?? Bundles.FirstOrDefault();
            if (SelectedBundle is null)
            {
                TipMessage = LanguageService.GetString("GameImport_LoadTextureFailed");
                return;
            }
        }
        Launcher = await this.GameContext.GetResourceSummaryAsync(Parameter, this.CTS.Token);
        if (Launcher == null)
        {
            return;
        }
        Versions = Launcher.HistoricalVersions.Reverse().ToObservableCollection();
        Versions.Insert(0, Launcher.OfficialVersion);
        SelectedVersion = Versions[0];
    }

    partial void OnIsVerifyChanged(bool value)
    {
        this.StartVerifyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    Task SelectVersion() => RunBusyAsync(SelectVersionCoreAsync);

    private async Task SelectVersionCoreAsync()
    {
        if (!IsVerify)
            return;
        var folder = System.IO.Path.GetDirectoryName(this.ExePath);
        if (
            string.IsNullOrWhiteSpace(this.SelectedVersion)
            || string.IsNullOrWhiteSpace(this.ExePath)
            || string.IsNullOrWhiteSpace(folder)
        )
            return;
        var config = await GetBundleConfigAsync();
        config[GameLocalSettingName.LocalGameVersion] = SelectedVersion;
        config[GameLocalSettingName.GameLauncherBassFolder] = folder;
        config[GameLocalSettingName.LocalGameUpdateing] = "False";
        config[GameLocalSettingName.GameLauncherBassProgram] = ExePath;
        if (!await GameContext.GameLocalConfig.SaveConfigsAsync(config, CTS.Token))
        {
            TipMessage = LanguageService.GetString("GameImport_SaveConfigFailed");
            return;
        }
        this.GameContext.GameEventPublisher.Publish(
            new GameContextOutputArgs()
            {
                Type = Waves.Core.Models.Enums.GameContextActionType.None,
            }
        );
        await this.Close();
    }

    private async Task<Dictionary<string, string>> GetBundleConfigAsync()
    {
        var config = new Dictionary<string, string>();
        if (!HasBundleSelection)
            return config;
        var bundle = SelectedBundle
            ?? throw new InvalidOperationException(LanguageService.GetString("GameImport_RequireTexture"));
        if (string.IsNullOrWhiteSpace(bundle.Version))
            throw new InvalidOperationException(LanguageService.GetString("GameImport_ReloadTextureVersion"));

        var arguments = await GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.StartGameArguments, CTS.Token) ?? string.Empty;
        arguments = Regex.Replace(arguments, @"(?<!\S)-krqlv=(?:""[^""]*""|\S+)", "", RegexOptions.IgnoreCase).Trim();
        var quality = string.IsNullOrWhiteSpace(bundle.ResourceCommand)
            ? bundle.BunleName.ToLowerInvariant()
            : bundle.ResourceCommand.ToLowerInvariant();
        config[GameLocalSettingName.BunleName] = bundle.BunleName;
        config[GameLocalSettingName.GetBunlePackVersionKey(bundle.BunleName)] = bundle.Version;
        config[GameLocalSettingName.BunlePackVersion_Common] = SelectedVersion;
        config[GameLocalSettingName.StartGameArguments] = string.IsNullOrWhiteSpace(arguments)
            ? $"-krqlv={quality}"
            : $"{arguments} -krqlv={quality}";
        return config;
    }

    private async Task<long> CalculateFolderSizeAsync(DirectoryInfo directory)
    {
        long totalSize = 0;
        var files = GetAccessibleFiles(directory);
        await Parallel.ForEachAsync(
            files,
            async (file, ct) =>
            {
                try
                {
                    Interlocked.Add(ref totalSize, file.Length);
                }
                catch (FileNotFoundException) { }
                await Task.CompletedTask;
            }
        );
        var subdirs = GetAccessibleDirectories(directory);
        await Parallel.ForEachAsync(
            subdirs,
            async (subdir, ct) =>
            {
                var size = await CalculateFolderSizeAsync(subdir);
                Interlocked.Add(ref totalSize, size);
            }
        );

        return totalSize;
    }

    private FileInfo[] GetAccessibleFiles(DirectoryInfo dir)
    {
        try
        {
            return dir.GetFiles();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<FileInfo>();
        }
    }

    private DirectoryInfo[] GetAccessibleDirectories(DirectoryInfo dir)
    {
        try
        {
            return dir.GetDirectories();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<DirectoryInfo>();
        }
    }

    private DriveInfo? GetDriveInfo(string rootPath)
    {
        return DriveInfo
            .GetDrives()
            .FirstOrDefault(d => d.Name.Equals(rootPath, StringComparison.OrdinalIgnoreCase));
    }

    private double BytesToGigabytes(long bytes) => bytes / 1024d / 1024 / 1024;

    internal void SetData(Type type, GameResourceParameter? parameter = null)
    {
        Parameter = parameter;
        var name = type.Name;
        this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(name);
    }
}
