using Haiyu.Models.Dialogs;
using Haiyu.Plugin.Common.LegacyMessageBox;
using Haiyu.Services.DialogServices;
using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Helpers;
using Waves.Core.Models.Enums;
using Windows.System;

namespace Haiyu.ViewModel.DialogViewModels;


public sealed partial class UpdateGameViewModelV2 : DialogViewModelBase
{
    public UpdateGameViewModelV2(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IPickersService pickersService,
        IAppContext<App> app
    )
        : base(dialogManager)
    {
        this.App = app;
        PickersService = pickersService;
    }

    public IGameContextV2 GameContext { get; private set; }
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
        var result = await PickersService.GetFolderPicker();
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
            WindowExtension.MessageBox(0, "不能选择磁盘根目录作为补丁下载目录！", "警告", 0);
            EnableContinue = false;
            return;
        }
        double totalSizeGB = ByteConversion.BytesToGigabytes(driveInfo.TotalSize, 2);
        double freeSpaceGB = ByteConversion.BytesToGigabytes(driveInfo.TotalFreeSpace, 2);
        if (freeSpaceGB < PatcherFileSize)
        {
            WindowExtension.MessageBox(0, "选择磁盘容量不足！", "警告", 0);
            EnableContinue = false;
            return;
        }
        EnableContinue = true;
    }

    [RelayCommand]
    async Task Loaded()
    {
        string? localVersion = "";
        IntPtr ownedHwnd = Win32Interop.GetWindowFromWindowId(App.App.MainWindow.AppWindow.Id);
        var launcher = await this.GameContext.GetGameLauncherSourceAsync(null, this.CTS.Token);
        #region 前置判断
        if (this.InvokeType == UpdateGameType.UpdateGame)
        {
            if (launcher == null || launcher.ResourceDefault == null)
            {
                WindowExtension.MessageBox(0, "游戏资源拉取失败！", "错误", 0);
                this.Close();
                return;
            }
        }
        else
        {
            if (launcher == null || launcher.Predownload == null)
            {
                WindowExtension.MessageBox(0, "预下载资源拉取失败！", "错误", 0);
                this.Close();
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
            WindowExtension.MessageBox(0, "本地游戏版本获取失败，请重启启动器后重新尝试", "错误", 0);
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
        var cdnUrl =
           launcher
               .ResourceDefault.CdnList.Where(x => x.P != 0)
               .OrderBy(x => x.P)
               .FirstOrDefault()
           ?? null;
        if(cdnUrl == null || patche == null)
        {
            WindowExtension.MessageBox(
                0,
                "网络请求失败，请稍后重新尝试",
                "警告",
                0
            );
            return;
        }
        var preious = await GameContext.GetPatchGameResourceAsync(cdnUrl.Url+patche.IndexFile);
        if(preious != null && preious.ApplyTypes == null && preious.Resource != null)
        {
            LegacyMessageBox.ShowInformation(ownedHwnd, "警告：本地版本过于老旧，无法更新\r\n解决方案：建议进行 修复游戏 或 卸载之后重新下载\r\n原因说明：检索库洛服务器中不包含热补丁文件，无法进行增量更新","警告");
            return;
        }
        PatcherFileSize = ByteConversion.BytesToGigabytes(patche.Size, 2);
        string? driveLetter = Path.GetPathRoot(_localPath);
        DriveInfo? driveInfo = DriveInfo
            .GetDrives()
            .FirstOrDefault(d => d.Name.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));
        if (driveInfo == null || !driveInfo.IsReady)
        {
            LegacyMessageBox.ShowInformation(ownedHwnd, "警告：本地版本过于老旧，无法更新\r\n解决方案：建议进行 修复游戏 或 卸载之后重新下载\r\n原因说明：检索库洛服务器中不包含热补丁文件，无法进行增量更新", "警告");
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
                new PieData() { Name = "总容量", Values = [totalSizeGB] },
                new PieData() { Name = "已用容量", Values = [usedSpaceGB] },
                new PieData() { Name = "更新占用容量", Values = [PatcherFileSize] },
            };
        }
        FreeDiskSpace = freeSpaceGB;
        if (FreeDiskSpace < PatcherFileSize)
        {
            this.Logger.WriteError("磁盘空间不足");
            WindowExtension.MessageBox(
                0,
                "磁盘空间不足！可以选择其他盘作为补丁文件下载路径",
                "警告",
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
    void Invoke()
    {
        this.IsOk = true;
        this.Close();
    }

    internal void SetData(IGameContextV2 context, UpdateGameType item2)
    {
        this.GameContext = context;
        this.InvokeType = item2;
        if (this.InvokeType == UpdateGameType.UpdateGame)
        {
            this.InvokeName = "更新游戏";
        }
        else
        {
            this.InvokeName = "预下载游戏";
        }
    }
}