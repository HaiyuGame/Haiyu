using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using Project.WPFSetup.Common;
using Project.WPFSetup.Common.Setups;
using Project.WPFSetup.Resources;
using Project.WPFSetup.Services;

namespace Project.WPFSetup.ViewModels.UserViewModels;

public sealed partial class InstallViewModel : ObservableRecipient
{
    public InstallViewModel(PackageService packageService)
    {
        PackageService = packageService;
        this.SetupProperty = SetupPropertyFactory.CreateInstall();
        this.SelectInstallVisibility = Visibility.Visible;
        InstallingVisibility = Visibility.Collapsed;
        InstalledVisibility = Visibility.Collapsed;
        this.InstallFolder = Path.Combine(Environment.GetFolderPath(Environment.Is64BitProcess? Environment.SpecialFolder.ProgramFiles: Environment.SpecialFolder.ProgramFilesX86),"Haiyu");    }

    [ObservableProperty]
    public partial string InstallFolder { get; set; }

    partial void OnInstallFolderChanged(string value)
    {
        SetupProperty.InstallPath = value;
    }

    [ObservableProperty]
    public partial Visibility SelectInstallVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility InstallingVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility InstalledVisibility { get; set; } = Visibility.Collapsed;

    #region InstallProperty
    [ObservableProperty]
    public partial InstallProgressArgs InstallProgressArgs { get; set; }

    [ObservableProperty]
    public partial string SetupString { get; set; }

    #endregion

    [ObservableProperty]
    public partial bool CreateStartMenuCheck { get; set; }

    [ObservableProperty]
    public partial bool CreateDesktopCheck { get; set; }

    public PackageService PackageService { get; }
    public SetupProperty SetupProperty { get; }

    [RelayCommand]
    void OpenFolder()
    {
        CommonOpenFileDialog dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            var isVild =  Path.GetPathRoot(dialog.FileName) == dialog.FileName;
            if (isVild)
            {
                InstallFolder = dialog.FileName+"\\Haiyu";
            }
            else
            {
                InstallFolder = dialog.FileName;
            }
        }
    }

    [RelayCommand]
    async Task InstallAsync()
    {
        if (string.IsNullOrWhiteSpace(this.SetupProperty.InstallPath))
        {
            MessageBox.Show("请选择安装目录！");
            return;
        }
        var isVild = Path.GetPathRoot(SetupProperty.InstallPath) == "C:\\";
        if (isVild)
        {
            var resultMessage = MessageBox.Show("注意！你选择了C盘进行安装，Haiyu不会自动使用管理员权限启动，有可能会导致库街区无法登陆以及云工具无法正常使用的错误！\r\n继续安装请点击确定", "警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (resultMessage == MessageBoxResult.Cancel)
            {
                return;
            }
        }
        var files = Directory.GetFiles(this.SetupProperty.InstallPath,"*",searchOption: SearchOption.AllDirectories);
        if (files.Length>0)
        {
            MessageBox.Show("必须选择一个空文件夹作为安装目录！", "警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            return;
        }
        this.SelectInstallVisibility = Visibility.Collapsed;
        InstalledVisibility = Visibility.Collapsed;
        InstallingVisibility = Visibility.Visible;
        SetupProperty.UninstallString = SetupProperty.GetUninstallPath();
        if (CreateStartMenuCheck)
        {
            SetupProperty.Setups.Add(new StartMenuLinkSetup());
            SetupProperty.Setups.Add(new CreateUninstallSetup());
        }
        if (CreateDesktopCheck)
        {
            SetupProperty.Setups.Add(new DesktopLinkSetup());
        }
        SetupProperty.Setups.Add(new RisgrayKeyWriterSetup());
        IProgress<InstallProgressArgs> installProgress = new Progress<InstallProgressArgs>();
        (installProgress as Progress<InstallProgressArgs>)!.ProgressChanged += (s, e) =>
        {
            this.InstallProgressArgs = e;
            this.SetupString = e.GetCurrentSetupString();
        };
        var result = await PackageService.InvokeSetup(this.SetupProperty, installProgress);
        if (result.Item1)
        {
            this.SelectInstallVisibility = Visibility.Collapsed;
            InstalledVisibility = Visibility.Visible;
            InstallingVisibility = Visibility.Collapsed;
        }
        else
        {
            MessageBox.Show($"安装失败！{result.Item2}");
            Environment.Exit(0);
        }
    }

    [RelayCommand]
    void Close()
    {
        Environment.Exit(0);
    }

    [RelayCommand]
    void StartProcessAndClose()
    {
        var path = $"{this.SetupProperty.InstallPath}\\{Resource1.ProgramExe}";
        Process.Start(path);
        Environment.Exit(0);
    }
}
