using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Project.WPFSetup.Common;
using Project.WPFSetup.Common.Setups;
using Project.WPFSetup.Services;

namespace Project.WPFSetup.ViewModels.UserViewModels;

public sealed partial class UninstallViewModel : ObservableRecipient
{
    public PackageService PackageService { get; }
    public SetupProperty SetupProperty { get; }

    [ObservableProperty]
    public partial Visibility UninsatllVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility UninstallingVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility UninstadVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial InstallProgressArgs InstallProgressArgs { get; set; }

    [ObservableProperty]
    public partial string SetupString { get; set; }

    public UninstallViewModel(PackageService packageService)
    {
        PackageService = packageService;
        this.SetupProperty = SetupPropertyFactory.CreateInstall();
        var path = packageService.GetInstallLocation(SetupProperty);
        if (path.Item1)
        {
            SetupProperty.InstallPath = path.Item2;
            UninsatllVisibility = Visibility.Visible;
            UninstallingVisibility = Visibility.Collapsed;
            UninstadVisibility = Visibility.Collapsed;
        }
        else
        {
            MessageBox.Show(
                $"文件损坏，请进入：计算机\\HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{SetupProperty.ProductId}，随后删除此注册表信息"
            );
            Environment.Exit(0);
        }
    }

    [RelayCommand]
    async Task Uninstall()
    {
        UninsatllVisibility = Visibility.Collapsed;
        UninstallingVisibility = Visibility.Visible;
        UninstadVisibility = Visibility.Collapsed;
        SetupProperty.Setups.Clear();
        SetupProperty.Setups.Add(new DeleteInstallFolderSetup());
        SetupProperty.Setups.Add(new RemoveRegisterSetup());
        SetupProperty.Setups.Add(new RemoveLinkSetup());
        IProgress<InstallProgressArgs> installProgress = new Progress<InstallProgressArgs>();
        (installProgress as Progress<InstallProgressArgs>)!.ProgressChanged += (s, e) =>
        {
            this.InstallProgressArgs = e;
            this.SetupString = e.GetCurrentSetupString();
        };
        var result = await this.PackageService.InvokeSetup(this.SetupProperty, installProgress);
        if (result.Item1 == true)
        {
            this.UninsatllVisibility = Visibility.Collapsed;
            this.UninstallingVisibility = Visibility.Collapsed;
            this.UninstadVisibility = Visibility.Visible;
            MessageBox.Show("卸载完成，可能会出现多余的文件夹内容需要手动删除");
            return;
        }
    }

    [RelayCommand]
    async Task Loaded()
    {
        var progress = Process.GetProcesses();
        var current = progress.Where(x=>x.ProcessName.Contains("Haiyu")).FirstOrDefault();
        if(current != null)
        {
            var result = MessageBox.Show("Haiyu正在运行，是否关闭？","警告",MessageBoxButton.OKCancel, MessageBoxImage.Warning,MessageBoxResult.OK);
            if(result == MessageBoxResult.OK) 
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

    [RelayCommand]
    void Help()
    {
        Process.Start("explorer.exe", $"{SetupProperty.HelpLink}");
    }

    [RelayCommand]
    void Close()
    {
        Environment.Exit(0);
    }
}
