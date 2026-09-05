using Haiyu.Common.Contracts;
using Haiyu.Common.WindowContext;
using Haiyu.Models.Options;

namespace Haiyu.Services;

public sealed class WindowManager : IWindowManager
{


    public WindowManager(AppSettings appSettings)
    {
        AppSettings = appSettings;
    }
    public readonly Dictionary<string, WindowContext> _windowContext =new();

    public ShellWindowContext Shell
    {
        get
        {
            var context =  _windowContext.GetValueOrDefault("Shell");
            if(context is ShellWindowContext shellC)
            {
                return shellC;
            }
            throw new ArgumentException("Shell window context not found.");
        }
    }

    public AppSettings AppSettings { get; }

    public async Task CreateShellWindowAsync()
    {
        WindowEx winEx = new WindowEx();

        winEx.Title = "Haiyu";
        winEx.AppWindow.SetIcon(AppDomain.CurrentDomain.BaseDirectory + "Assets/appLogo.ico");
        NativeWindowHelper.ForceDisableMaximize(winEx, targetDipWidth: 1150, targetDipHeight: 650);
        winEx.SystemBackdrop = new MicaBackdrop();
        (winEx.AppWindow.Presenter as OverlappedPresenter)!.SetBorderAndTitleBar(true, false);
        var shell = new ShellWindowContext(Instance.Host.Services.CreateAsyncScope(), IWindowManager.ShellKey);
        shell.SetWindow(winEx);
        shell.GetWindow().AppWindow.Closing += AppWindow_Closing;
        this._windowContext.Add(shell.Key, shell);
        #region Config
        var mainSizeConfig = await this.AppSettings.GetMainWindowSettingsAsync();
        if (mainSizeConfig == null)
        {
            mainSizeConfig = MainWindowSetting.Default;
        }
        #endregion
        #region Page
        if (await AppSettings.GetAutoOOBEAsync() == true)
        {
            var page = Instance.Host.Services.GetRequiredService<OOBEPage>();
            page.titlebar.Window = this.Shell.GetWindow();
            this.Shell.GetWindow().Content = page;
            this.Shell.GetWindow().ApplyWindowsOption(WindowsOption.OOBEWindowOption);
        }
        else
        {
            var page = Instance.Host.Services!.GetRequiredService<ShellPage>();
            page.titlebar.Window = this.Shell.GetWindow();
            this.Shell.GetWindow().Content = page;
            var defaultOption = WindowsOption.DefaultWindowsOption;
            var widthRate =
                double.IsFinite(mainSizeConfig.WidthRate) && mainSizeConfig.WidthRate > 0
                    ? mainSizeConfig.WidthRate
                    : MainWindowSetting.Default.WidthRate;
            var heightRate =
                double.IsFinite(mainSizeConfig.HeightRate) && mainSizeConfig.HeightRate > 0
                    ? mainSizeConfig.HeightRate
                    : MainWindowSetting.Default.HeightRate;

            this.Shell.GetWindow().ApplyWindowsOption(
                defaultOption with
                {
                    Width = defaultOption.Width * widthRate,
                    Height = defaultOption.Height * heightRate,
                    IsResizable = mainSizeConfig.IsResize,
                }
            );
        }
        #endregion

        
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
    }

    public Task CreateWindow<T>(WindowManagerOption managerOption) where T : IWindowPage
    {
        return null;
    }
    public Task<IEnumerable<WindowContext>> GetWindowContextsAsync()
        => Task.FromResult(_windowContext.Values.AsEnumerable());

    public WindowContext? GetWindowContext(string key)
    {
        return _windowContext.GetValueOrDefault(key);
    }
}
