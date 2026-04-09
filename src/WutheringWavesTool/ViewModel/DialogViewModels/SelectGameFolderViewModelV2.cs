using System.Text.RegularExpressions;
using Haiyu.Services.DialogServices;
using Microsoft.UI.Xaml.Shapes;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class SelectGameFolderViewModelV2 : DialogViewModelBase
{
    public SelectGameFolderViewModelV2(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IPickersService pickersService
    )
        : base(dialogManager)
    {
        PickersService = pickersService;
    }

    public IGameContextV2 GameContext { get; private set; }

    [ObservableProperty]
    public partial string ExePath { get; set; }

    [ObservableProperty]
    public partial string TipMessage { get; set; } = "选择目标程序，以查看驱动器详情";

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
    public GameLauncherSource? Launcher { get; internal set; }

    [RelayCommand]
    async Task SelectGameProgram()
    {
        var exe = await PickersService.GetFileOpenPicker([".exe"]);
        if (exe == null)
            return;
        if (System.IO.Path.GetFileName(exe.Path) != GameContext.Config.GameExeName)
        {
            TipMessage = "无效地址";
            return;
        }
        this.ExePath = exe.Path;
        var folderPath = System.IO.Path.GetDirectoryName(ExePath);
        var directoryInfo = new DirectoryInfo(folderPath);
        var folderSizeBytes = await CalculateFolderSizeAsync(directoryInfo);
        var folderSizeGB = BytesToGigabytes(folderSizeBytes);
        var rootPath = System.IO.Path.GetPathRoot(ExePath);
        var driveInfo = GetDriveInfo(rootPath);

        if (driveInfo == null)
        {
            TipMessage = $"无法找到对应驱动器: {rootPath}";
            return;
        }

        Launcher = await this.GameContext.GetGameLauncherSourceAsync(null, this.CTS.Token);
        if (Launcher == null)
        {
            TipMessage = $"游戏数据拉取失败";
            return;
        }

        var totalSpaceGB = BytesToGigabytes(driveInfo.TotalSize);
        var freeSpaceGB = BytesToGigabytes(driveInfo.TotalFreeSpace);
        this.MaxValue = totalSpaceGB;
        this.BarValues = new ObservableCollection<LayerData>([
            new LayerData()
            {
                Label = "总容量",
                Color = new SolidColorBrush(Colors.LightGreen),
                Value = totalSpaceGB,
            },
            new LayerData()
            {
                Label = "当前游戏文件夹容量",
                Color = new SolidColorBrush(Colors.Purple),
                Value = totalSpaceGB - freeSpaceGB,
            },
            new LayerData()
            {
                Label = "占用容量",
                Color = new SolidColorBrush(Colors.MediumPurple),
                Value = totalSpaceGB - freeSpaceGB - folderSizeGB,
            },
        ]);
        IsVerify = true;
    }

    [RelayCommand]
    void StartVerify()
    {
        this.Result = ContentDialogResult.Primary;
        this.DialogManager.CloseDialog();
    }

    [RelayCommand]
    async Task Loaded()
    {
        Launcher = await this.GameContext.GetGameLauncherSourceAsync(token: this.CTS.Token);
        if (Launcher == null)
        {
            return;
        }
        var configs = Launcher.ResourceDefault.Config.PatchConfig;
        var versions = new List<string>();
        foreach (var item in configs)
        {
            string pattern = @"\d+(?:\.\d+)+";

            // 提取所有匹配结果
            MatchCollection matches = Regex.Matches(item.IndexFile, pattern);
            var result = matches.Select(x => x.Value).Distinct().ToList();
            if (result.Count > 1)
                versions.Add(result[1]);
        }
        Versions = versions.Reverse<string>().ToObservableCollection();
        Versions.Insert(0, Launcher.ResourceDefault.Version);
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
        this.GameContext.GameEventPublisher.Publish(new GameContextOutputArgs()
        {
            Type = Waves.Core.Models.Enums.GameContextActionType.None
        });
        this.Close();
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

    internal void SetData(Type type)
    {
        var name = type.Name;
        this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(name);
    }
}
