using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Project.WPFSetup.Common;
using Project.WPFSetup.Common.Setups;
using Project.WPFSetup.Resources;
using Project.WPFSetup.Services;
using System.Diagnostics;
using System.Windows;

namespace Project.WPFSetup.ViewModels.UserViewModels;

public partial class UpdateViewModel : ObservableObject
{
    public PackageService PackageService { get; }
    public SetupProperty SetupProperty { get; }

    [ObservableProperty]
    public partial string CurrentVersion { get; set; }

    [ObservableProperty]
    public partial string CurrentInstallPath { get; set; }

    [ObservableProperty]
    public partial Visibility InstallingVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility UpdateVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string UpdateBthString { get; set; }

    [ObservableProperty]
    public partial string UpdateTipString { get; set; }

    [ObservableProperty]
    public partial Visibility UpdatedVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial InstallProgressArgs InstallProgressArgs { get; set; }

    [ObservableProperty]
    public partial string SetupString { get; set; }

    public UpdateViewModel(PackageService packageService)
    {
        PackageService = packageService;
        this.SetupProperty = SetupPropertyFactory.CreateInstall();
        var currentVersion = PackageService.GetInstallVersion(SetupProperty);
        var curentLocation = PackageService.GetInstallLocation(SetupProperty);
        if (currentVersion.Item1)
        {
            this.CurrentVersion = currentVersion.Item2!;
        }
        else
        {
            this.CurrentVersion = "NAN";
        }
        if (curentLocation.Item1)
        {
            this.CurrentInstallPath = curentLocation.Item2!;
            InstallingVisibility = Visibility.Collapsed;
            UpdateVisibility = Visibility.Visible;
            UpdatedVisibility = Visibility.Collapsed;
        }
        else
        {
            MessageBox.Show(
                $"无法获取路径！请删除注册表：计算机\\HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{SetupProperty.ProductId}"
            );
            Environment.Exit(0);
        }
        if (Version.Parse(CurrentVersion) > Version.Parse(Resource1.Version))
        {
            UpdateBthString = "下一步";
            UpdateTipString = "检测到安装包低于当前安装版本，是否继续？";
        }
        else
        {
            UpdateBthString = "升级";
            UpdateTipString = "全新安装包准备完毕";
        }
    }

    [RelayCommand]
    void Close()
    {
        Environment.Exit(0);
    }

    [RelayCommand]
    async Task Update()
    {
        this.SetupProperty.InstallPath = this.CurrentInstallPath;
        InstallingVisibility = Visibility.Visible;
        UpdateVisibility = Visibility.Collapsed;
        UpdatedVisibility = Visibility.Collapsed;
        SetupProperty.Setups.Add(new RisgrayKeyWriterSetup());
        SetupProperty.Setups.Insert(0, new DeleteInstallFolderSetup());
        IProgress<InstallProgressArgs> installProgress = new Progress<InstallProgressArgs>();
        (installProgress as Progress<InstallProgressArgs>)!.ProgressChanged += (s, e) =>
        {
            this.InstallProgressArgs = e;
            this.SetupString = e.GetCurrentSetupString();
        };
        var result = await PackageService.InvokeSetup(this.SetupProperty, installProgress);
        if (result.Item1)
        {
            InstallingVisibility = Visibility.Collapsed;
            UpdateVisibility = Visibility.Collapsed;
            UpdatedVisibility = Visibility.Visible;
        }
        else
        {
            MessageBox.Show($"安装失败！{result.Item2}");
            Environment.Exit(0);
        }
    }

    [RelayCommand]
    async Task Loaded()
    {
        var progress = Process.GetProcesses();
        var current = progress.Where(x => x.ProcessName.Contains("Haiyu")).FirstOrDefault();
        if (current != null)
        {
            var result = MessageBox.Show("Haiyu正在运行，是否关闭？", "警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.OK);
            if (result == MessageBoxResult.OK)
            {
                current.Kill();
            }
            else
            {
                MessageBox.Show("Haiyu正在运行，无法卸载");
                Environment.Exit(0);
            }
        }
    }
}
