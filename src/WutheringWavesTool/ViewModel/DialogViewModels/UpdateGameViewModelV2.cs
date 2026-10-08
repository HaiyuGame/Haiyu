using Waves.Core.Models.Options;
using System;
using System.Collections.Generic;
using System.Text;
using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Haiyu.Plugin.Common.LegacyMessageBox;
using Waves.Core.Helpers;
using Waves.Core.Models.Enums;
using Windows.System;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class UpdateGameViewModelV2 : DialogViewModelBase
{
    public UpdateGameViewModelV2(
        DialogSession dialogSession,
        IAppContext<App> app,
        IWindowManager windowManager
    )
        : base(dialogSession)
    {
        this.App = app;
        _windowManager = windowManager;
        PickersService = this.AppContext.WindowManager.Shell.PickersService;
    }

    public IGameContextV2 GameContext { get; private set; }
    public GameResourceParameter? Parameter { get; private set; }
    public UpdateGameType InvokeType { get; private set; }
    public IAppContext<App> App { get; }

    [ObservableProperty]
    public partial string NewVersion { get; set; }

    [ObservableProperty]
    public partial string LocalVersion { get; set; }

    [ObservableProperty]
    public partial double NewFileSize { get; set; }

    [ObservableProperty]
    public partial double LocalFileSize { get; set; }

    [ObservableProperty]
    public partial double PatcherFileSize { get; set; }

    [ObservableProperty]
    public partial double FreeDiskSpace { get; set; }

    [ObservableProperty]
    public partial bool EnableContinue { get; set; } = false;

    [ObservableProperty]
    public partial string DiffSavePath { get; set; }

    private string? _localPath;
    private long _downloadBytes;
    private bool _resourceReady;
    private readonly IWindowManager _windowManager;

    [ObservableProperty]
    public partial string InvokeName { get; set; }

    /// <summary>
    /// 磁盘更新示意图
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<object> DiskPipePoint { get; set; }
    public IPickersService PickersService { get; }

    [RelayCommand]
    async Task SelectDiffPath()
    {
        var result = await PickersService.GetFolderPicker();
        if (result == null)
            return;

        DiffSavePath = InstallOption.BuildCacheFolder(result.Path, InvokeType != UpdateGameType.UpdateGame);
        var rootDir = Path.GetPathRoot(result.Path);
        DriveInfo? driveInfo = DriveInfo
            .GetDrives()
            .FirstOrDefault(d => d.Name.Equals(rootDir, StringComparison.OrdinalIgnoreCase));
        if (driveInfo == null || !driveInfo.IsReady)
        {
            EnableContinue = false;
            return;
        }
        if (rootDir == result.Path)
        {
            WindowExtension.MessageBox(
                0,
                LanguageService.GetStringByText("不能选择磁盘根目录作为补丁下载目录！"),
                LanguageService.GetStringByText("警告"),
                0
            );
            EnableContinue = false;
            return;
        }
        double totalSizeGB = ByteConversion.BytesToGigabytes(driveInfo.TotalSize, 2);
        double freeSpaceGB = ByteConversion.BytesToGigabytes(driveInfo.TotalFreeSpace, 2);
        if (driveInfo.TotalFreeSpace < _downloadBytes)
        {
            WindowExtension.MessageBox(
                0,
                LanguageService.GetStringByText("选择磁盘容量不足！"),
                LanguageService.GetStringByText("警告"),
                0
            );
            EnableContinue = false;
            return;
        }
        FreeDiskSpace = freeSpaceGB;
        EnableContinue = _resourceReady;
    }

    [RelayCommand]
    async Task Loaded()
    {
        if (!IsAlive) return;
        var token = LifetimeToken;
        EnableContinue = false;
        _resourceReady = false;
        _downloadBytes = 0;
        try
        {
            await LoadDetailsAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _resourceReady = false;
            EnableContinue = false;
            if (IsAlive) ShowLoadError($"加载资源信息失败：{ex.Message}");
        }
    }

    private void ShowLoadError(string message)
    {
        Logger.WriteError($"{GameContext?.ContextName} / {InvokeType}: {message}");
        var hwnd = Win32Interop.GetWindowFromWindowId(App.WindowManager.Shell.GetWindow().AppWindow.Id);
        LegacyMessageBox.ShowInformation(hwnd, message, LanguageService.GetStringByText("提示"));
    }

    private async Task LoadDetailsAsync(CancellationToken token)
    {
        string? localVersion = "";
        var summary = await GameContext.GetResourceSummaryAsync(Parameter, token);
        token.ThrowIfCancellationRequested();
        var resourceSize = InvokeType == UpdateGameType.UpdateGame ? summary.Update : summary.Predownload;
        LocalVersion = summary.LocalVersion;
        NewVersion = InvokeType == UpdateGameType.UpdateGame ? summary.OfficialVersion : summary.PredownloadVersion ?? "";
        if (resourceSize.Availability != global::Waves.Core.Models.GameResourceAvailability.Ready)
        {
            var reason = resourceSize.Availability switch
            {
                global::Waves.Core.Models.GameResourceAvailability.AlreadyCurrent => "当前已是目标版本。",
                global::Waves.Core.Models.GameResourceAvailability.MissingPatch => "当前安装版本没有匹配的增量补丁，任务已跳过。",
                global::Waves.Core.Models.GameResourceAvailability.NoPredownload => "当前没有预下载资源。",
                _ => "资源状态尚未就绪。"
            };
            ShowLoadError($"{reason}\r\n本地版本：{LocalVersion}，目标版本：{NewVersion}\r\n可使用刷新按钮重新查询。资源状态：{resourceSize.Availability}");
            return;
        }
        _localPath = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder,
            token
        );
        localVersion = summary.LocalVersion;
        if (localVersion == null)
        {
            WindowExtension.MessageBox(
                0,
                LanguageService.GetStringByText("本地游戏版本获取失败，请重启启动器后重新尝试"),
                LanguageService.GetStringByText("错误"),
                0
            );
            return;
        }
        if (string.IsNullOrWhiteSpace(_localPath) || !Directory.Exists(_localPath))
        {
            ShowLoadError("游戏目录不存在，请检查已选择的游戏目录。");
            return;
        }
        LocalVersion = localVersion;
        NewVersion = InvokeType == UpdateGameType.UpdateGame ? summary.OfficialVersion : summary.PredownloadVersion!;
        NewFileSize = ByteConversion.BytesToGigabytes(resourceSize.TargetSize, 2);
        var localSize = await FolderSizeCalculator.CalculateFolderSizeAsync(
            _localPath!,
            token
        );
        token.ThrowIfCancellationRequested();
        LocalFileSize = ByteConversion.BytesToGigabytes(localSize, 2);
        _downloadBytes = resourceSize.DownloadSize;
        _resourceReady = true;
        if (string.IsNullOrWhiteSpace(DiffSavePath))
            DiffSavePath = InstallOption.BuildCacheFolder(_localPath!, InvokeType != UpdateGameType.UpdateGame);
        PatcherFileSize = ByteConversion.BytesToGigabytes(_downloadBytes, 2);
        string? driveLetter = Path.GetPathRoot(DiffSavePath);
        DriveInfo? driveInfo = DriveInfo
            .GetDrives()
            .FirstOrDefault(d => d.Name.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));
        if (driveInfo == null || !driveInfo.IsReady)
        {
            ShowLoadError("缓存目录所在磁盘尚未就绪，请选择其他目录。");
            return;
        }
        double totalSizeGB = ByteConversion.BytesToGigabytes(driveInfo.TotalSize, 2);
        double freeSpaceGB = ByteConversion.BytesToGigabytes(driveInfo.TotalFreeSpace, 2);
        double usedSpaceGB = totalSizeGB - freeSpaceGB;
        if (this.DiskPipePoint != null)
        {
            (DiskPipePoint[0] as PieData).Values = [totalSizeGB];
            (DiskPipePoint[1] as PieData).Values = [usedSpaceGB];
            (DiskPipePoint[2] as PieData).Values = [PatcherFileSize];
        }
        else
        {
            this.DiskPipePoint = new ObservableCollection<object>()
            {
                new PieData()
                {
                    Name = LanguageService.GetStringByText("总容量"),
                    Values = [totalSizeGB],
                },
                new PieData()
                {
                    Name = LanguageService.GetStringByText("已用容量"),
                    Values = [usedSpaceGB],
                },
                new PieData()
                {
                    Name = LanguageService.GetStringByText("更新占用容量"),
                    Values = [PatcherFileSize],
                },
            };
        }
        FreeDiskSpace = freeSpaceGB;
        if (driveInfo.TotalFreeSpace < _downloadBytes)
        {
            this.Logger.WriteError("磁盘空间不足");
            WindowExtension.MessageBox(
                0,
                LanguageService.GetStringByText("磁盘空间不足！可以选择其他盘作为补丁文件下载路径"),
                LanguageService.GetStringByText("警告"),
                0
            );
            EnableContinue = false;
        }
        else
        {
            EnableContinue = _resourceReady;
        }
    }

    [RelayCommand]
    async Task Invoke()
    {
        if (!EnableContinue || !_resourceReady || string.IsNullOrWhiteSpace(DiffSavePath)) return;
        this.Result = new UpdateGameResult() { IsOk = true, DiffSavePath = DiffSavePath, Parameter = Parameter };
        await this.Close();
    }

    internal void SetData(IGameContextV2 context, UpdateGameType item2, GameResourceParameter? parameter = null)
    {
        this.GameContext = context;
        Parameter = parameter;
        this.InvokeType = item2;
        if (this.InvokeType == UpdateGameType.UpdateGame)
        {
            this.InvokeName = LanguageService.GetStringByText("更新游戏");
        }
        else
        {
            this.InvokeName = LanguageService.GetStringByText("预下载游戏");
        }
    }
}
