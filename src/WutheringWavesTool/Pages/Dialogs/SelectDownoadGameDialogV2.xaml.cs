using Haiyu.Models.Dialogs;
using Haiyu.Services.DialogServices;

namespace Haiyu.Pages.Dialogs;

public sealed partial class SelectDownoadGameDialogV2
    : ContentDialog,
        IResultDialog<SelectDownloadFolderResult>
{
    public SelectDownoadGameDialogV2()
    {
        InitializeComponent();
        this.DialogManager = Instance.Host.Services.GetRequiredKeyedService<IDialogManager>(
            nameof(MainDialogService)
        );
        this.Pickers = Instance.Host.Services.GetRequiredService<IPickersService>();
        this.RequestedTheme = Instance
            .Host.Services.GetRequiredService<IThemeService>()
            .CurrentTheme;
    }

    SelectDownloadFolderResult downloadResult = null;
    ContentDialogResult clickBth = ContentDialogResult.None;
    public IGameContextV2 GameContext { get; private set; }
    public IDialogManager DialogManager { get; }
    public IPickersService Pickers { get; }
    public GameLauncherSource Launcher { get; private set; }

    public SelectDownloadFolderResult GetResult()
    {
        return this.downloadResult;
    }

    public void SetData(object data)
    {
        if (data is Type type)
        {
            var name = type.Name;
            this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(name);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        downloadResult = new SelectDownloadFolderResult()
        {
            InstallFolder = this.folderPath.Text,
            Result = clickBth,
        };
        this.DialogManager.CloseDialog();
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDiskAsync();
        if (!downloadBth.IsEnabled)
        {
            return;
        }

        var launcher = await this.GameContext.GetGameLauncherSourceAsync();
        if (launcher == null)
        {
            return;
        }
        this.clickBth = ContentDialogResult.Primary;
        downloadResult = new SelectDownloadFolderResult()
        {
            InstallFolder = this.folderPath.Text,
            Result = clickBth,
            Launcher = launcher,
        };
        this.DialogManager.CloseDialog();
    }

    private async void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = await Pickers.GetFolderPicker();
        if (folderPath == null)
            return;
        if (!Directory.Exists(folderPath.Path))
        {
            return;
        }
        this.folderPath.Text = folderPath.Path;
        await RefreshDiskAsync();
    }

    async Task RefreshDiskAsync()
    {
        string? rootPath = Path.GetPathRoot(this.folderPath.Text);
        DriveInfo? selectedDrive = DriveInfo
            .GetDrives()
            .FirstOrDefault(drive =>
                drive.Name.Equals(rootPath, StringComparison.OrdinalIgnoreCase)
            );
        var isVild = !IsInstallDirectory(folderPath.Text);
        if (isVild)
        {
            layeredGrid.Visibility = Visibility.Visible;
            layerText.Visibility = Visibility.Collapsed;
            TipMessage.Text = LanguageService.GetStringByText("请选择一个文件夹，而并非一个磁盘");
            download.Fill = new SolidColorBrush(Colors.Red);
            downloadBth.IsEnabled = false;
            return;
        }
        if (selectedDrive == null)
            return;
        double totalSizeMB = (double) selectedDrive.TotalSize / (1024 * 1024 * 1024);
        double freeSpaceMB = (double) selectedDrive.TotalFreeSpace / (1024 * 1024 * 1024);
        double usedSpaceMB = totalSizeMB - freeSpaceMB;
        layered.MaxValue = totalSizeMB;
        layeredGrid.Visibility = Visibility.Visible;
        layerText.Visibility = Visibility.Collapsed;
        Launcher = await this.GameContext.GetGameLauncherSourceAsync();
        if (Launcher == null)
        {
            TipMessage.Text = LanguageService.GetStringByText("数据拉取失败");
            return;
        }
        var updateSize = usedSpaceMB + Launcher.ResourceDefault.Config.Size / 1024 / 1024 / 1024;
        this.layered.Values = new ObservableCollection<LayerData>()
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
        if (updateSize > totalSizeMB)
        {
            TipMessage.Text = LanguageService.GetStringByText("空间不足，请清理一些文件进行下载");
            download.Fill = new SolidColorBrush(Colors.Red);
            downloadBth.IsEnabled = false;
            return;
        }
        else
        {
            TipMessage.Text =
                LanguageService.FormatByText(LanguageService.GetStringByText("本次更新大小约为{0}GB"), Launcher.ResourceDefault.Config.Size / 1024 / 1024 / 1024);
            downloadBth.IsEnabled = true;
            download.Fill = new SolidColorBrush(Colors.Green);
        }
    }

    private async void RefreshDisk_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDiskAsync();
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
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException
        )
        {
            return false;
        }
    }
}
