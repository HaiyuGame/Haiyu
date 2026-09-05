using System;
using System.Collections.Generic;
using System.Text;
using Haiyu.Models.Dialogs;
using Waves.Core.Common;
using Waves.Core.Helpers;
using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class UpdateGameViewModel : DialogViewModelBase
{
    public UpdateGameViewModel(
        DialogSession dialogSession,
        IPickersService pickersService,
        IWindowManager windowManager
    )
        : base(dialogSession)
    {
        PickersService = pickersService;
        _windowManager = windowManager;
    }

    public IGameContextV2 GameContext { get; private set; }
    public UpdateGameType InvokeType { get; private set; }

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
    private readonly IWindowManager _windowManager;

    [ObservableProperty]
    public partial string InvokeName { get; set; }

    /// <summary>
    /// 磁盘更新示意图
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<object> DiskPipePoint { get; set; }
    public IPickersService PickersService { get; }
    public bool IsOk { get; private set; }

    public UpdateGameResult? GameResult()
    {
        return new UpdateGameResult() { DiffSavePath = DiffSavePath, IsOk = this.IsOk };
    }

    [RelayCommand]
    async Task SelectDiffPath()
    {
        var result = await PickersService.GetFolderPicker(
            _windowManager.Shell.GetWindow().GetWindowHandle()
        );
        if (result == null)
            return;

        DiffSavePath = result.Path;
        var rootDir = Path.GetPathRoot(result.Path);
        DriveInfo? driveInfo = DriveInfo
            .GetDrives()
            .FirstOrDefault(d => d.Name.Equals(rootDir, StringComparison.OrdinalIgnoreCase));
        if (driveInfo == null || !driveInfo.IsReady)
        {
            EnableContinue = false;
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
        if (freeSpaceGB < PatcherFileSize)
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
        EnableContinue = true;
    }

    [RelayCommand]
    async Task Loaded()
    {
        string? localVersion = "";
        var launcher = await this.GameContext.GetGameLauncherSourceAsync(null, this.CTS.Token);
        #region 前置判断
        if (this.InvokeType == UpdateGameType.UpdateGame)
        {
            if (launcher == null || launcher.ResourceDefault == null)
            {
                WindowExtension.MessageBox(
                    0,
                    LanguageService.GetStringByText("游戏资源拉取失败！"),
                    LanguageService.GetStringByText("错误"),
                    0
                );
                await this.Close();
                return;
            }
        }
        else
        {
            if (launcher == null || launcher.Predownload == null)
            {
                WindowExtension.MessageBox(
                    0,
                    LanguageService.GetStringByText("预下载资源拉取失败！"),
                    LanguageService.GetStringByText("错误"),
                    0
                );
                await this.Close();
                return;
            }
        }
        #endregion
        _localPath = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder,
            this.CTS.Token
        );
        localVersion = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion,
            this.CTS.Token
        );
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
        LocalVersion = localVersion;
        NewVersion =
            this.InvokeType == UpdateGameType.UpdateGame
                ? launcher.ResourceDefault.Version
                : launcher.Predownload.Version;
        NewFileSize = ByteConversion.BytesToGigabytes(
            this.InvokeType == UpdateGameType.UpdateGame
                ? launcher.ResourceDefault.Config.UnCompressSize
                : launcher.Predownload.Config.UnCompressSize,
            2
        );
        var localSize = await FolderSizeCalculator.CalculateFolderSizeAsync(
            _localPath!,
            this.CTS.Token
        );
        LocalFileSize = ByteConversion.BytesToGigabytes(localSize, 2);
        var patche =
            this.InvokeType == UpdateGameType.UpdateGame
                ? launcher
                    .ResourceDefault.Config.PatchConfig.Where(x => x.Version == localVersion)
                    .FirstOrDefault()
                : launcher
                    .Predownload.Config.PatchConfig.Where(x => x.Version == localVersion)
                    .FirstOrDefault();
        if (patche == null)
        {
            WindowExtension.MessageBox(
                IntPtr.Zero,
                LanguageService.GetStringByText(
                    "请联系开发者处理此问题：本地版本过于等于预下载版本，流程被打乱，无法进行预下载"
                ),
                "Haiyu",
                0
            );
            return;
        }
        PatcherFileSize = ByteConversion.BytesToGigabytes(patche.Size, 2);
        string? driveLetter = Path.GetPathRoot(_localPath);
        DriveInfo? driveInfo = DriveInfo
            .GetDrives()
            .FirstOrDefault(d => d.Name.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));
        if (driveInfo == null || !driveInfo.IsReady)
        {
            Console.WriteLine($"磁盘 {driveLetter} 不可用或未就绪");
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
        if (FreeDiskSpace < PatcherFileSize)
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
            this.DiffSavePath = Path.Combine(_localPath!, "Diff");
            EnableContinue = true;
        }
    }

    [RelayCommand]
    async Task Invoke()
    {
        this.IsOk = true;
        await this.Close();
    }

    internal void SetData(IGameContextV2 context, UpdateGameType item2)
    {
        this.GameContext = context;
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
