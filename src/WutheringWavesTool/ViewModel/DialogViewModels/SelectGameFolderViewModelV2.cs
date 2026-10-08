using Waves.Core.Models.Options;
using System.Text.RegularExpressions;
using Haiyu.Common.Contracts;
using Microsoft.UI.Xaml.Shapes;

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
    public partial string ExePath { get; set; }

    [ObservableProperty]
    public partial string TipMessage { get; set; } =
        LanguageService.GetStringByText("选择目标程序，以查看驱动器详情");

    [ObservableProperty]
    public partial bool IsVerify { get; set; }

    public bool GetIsVerify() => IsVerify;

    [ObservableProperty]
    public partial ObservableCollection<LayerData> BarValues { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> Versions { get; set; } = new();

    [ObservableProperty]
    public partial string SelectedVersion { get; set; }

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
    async Task RefreshDiskAsync()
    {
        if (!File.Exists(ExePath))
            return;
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
        if (Launcher == null)
        {
            TipMessage = LanguageService.FormatByText(
                LanguageService.GetStringByText("游戏数据拉取失败")
            );
            return;
        }

        var totalSpaceGB = BytesToGigabytes(driveInfo.TotalSize);
        var freeSpaceGB = BytesToGigabytes(driveInfo.TotalFreeSpace);
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
        IsVerify = true;
    }

    [RelayCommand]
    async Task StartVerify()
    {
        this.Result = ContentDialogResult.Primary;
        await this.Close();
    }

    [RelayCommand]
    async Task Loaded()
    {
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

    [RelayCommand]
    async Task SelectVersion()
    {
        var folder = System.IO.Path.GetDirectoryName(this.ExePath);
        if (
            string.IsNullOrWhiteSpace(this.SelectedVersion)
            || string.IsNullOrWhiteSpace(this.ExePath)
            || string.IsNullOrWhiteSpace(folder)
        )
            return;
        await this.GameContext.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LocalGameVersion,
            SelectedVersion
        );
        await this.GameContext.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder,
            folder
        );
        await this.GameContext.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.LocalGameUpdateing,
            "False"
        );
        await this.GameContext.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.GameLauncherBassProgram,
            ExePath
        );
        this.GameContext.GameEventPublisher.Publish(
            new GameContextOutputArgs()
            {
                Type = Waves.Core.Models.Enums.GameContextActionType.None,
            }
        );
        await this.Close();
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
