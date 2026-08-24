using CommunityToolkit.WinUI;
using Haiyu.Plugin.Contracts;
using Haiyu.Services.DialogServices;
using Microsoft.UI.Dispatching;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.GameContext.ContextsV2;
using Waves.Core.GameContext.ContextsV2.Punish;
using Waves.Core.GameContext.ContextsV2.Waves;
using Waves.Core.Services;
using Waves.Settings;
using Windows.UI.StartScreen;
using TitleBar = Haiyu.Controls.TitleBar;

namespace Haiyu.Services;

public class AppContext<T> : IAppContext<T>
    where T : ClientApplication
{
    public AppContext(
        IKuroClient wavesClient,
        IWallpaperService wallpaperService,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        [FromKeyedServices("AppLog")] LoggerService loggerService,
        AppSettings appSettings,
        IAppActivation appActivation
    )
    {
        KuroClient = wavesClient;
        WallpaperService = wallpaperService;
        DialogManager = dialogManager;
        LoggerService = loggerService;
        AppSettings = appSettings;
        AppActivation = appActivation;
    }

    private ContentDialog _dialog;

    public T App { get; private set; }

    public IKuroClient KuroClient { get; }
    public IWallpaperService WallpaperService { get; }
    public IDialogManager DialogManager { get; }
    public LoggerService LoggerService { get; }
    public AppSettings AppSettings { get; }
    public IAppActivation AppActivation { get; }

    public async Task LauncherAsync(T app)
    {
        try
        {
            var mainSizeConfig = await this.AppSettings.GetMainWindowSettingsAsync();
            if(mainSizeConfig == null)
            {
                mainSizeConfig = MainWindowSetting.Default;
            }
            var xboxConfig = Instance.Host.Services.GetRequiredService<XBoxConfig>();
            if ((await xboxConfig.GetIsEnableAsync()) == true)
            {
                await Instance.Host.Services.GetRequiredService<XBoxService>().StartAsync();
            }
            this.App = app;
            var win = new MainWindow();
            #region Mirror
            if (
                Instance.Host.Services.GetRequiredKeyedService<IUpdateService>("Mirror")
                is IMirrorUpdateService mirror
            )
            {
                mirror.SetMirrorKey(await AppSettings.GetMirrorKeyAsync());
            }
            #endregion
            try
            {
                if (await AppSettings.GetAutoOOBEAsync() == true)
                {
                    var page = Instance.Host.Services.GetRequiredService<OOBEPage>();
                    page.titlebar.Window = win;
                    win.Content = page;
                    win.ApplyWindowsOption(MainWindow.OOBEWindowOption);
                }
                else
                {
                    var page = Instance.Host.Services!.GetRequiredService<ShellPage>();
                    page.titlebar.Window = win;
                    win.Content = page;
                    var defaultOption = MainWindow.DefaultWindowsOption;
                    var widthRate =
                        double.IsFinite(mainSizeConfig.WidthRate)
                        && mainSizeConfig.WidthRate > 0
                            ? mainSizeConfig.WidthRate
                            : MainWindowSetting.Default.WidthRate;
                    var heightRate =
                        double.IsFinite(mainSizeConfig.HeightRate)
                        && mainSizeConfig.HeightRate > 0
                            ? mainSizeConfig.HeightRate
                            : MainWindowSetting.Default.HeightRate;

                    win.ApplyWindowsOption(
                        defaultOption with
                        {
                            Width = defaultOption.Width * widthRate,
                            Height = defaultOption.Height * heightRate,
                            IsResizable = mainSizeConfig.IsResize,
                        }
                    );
                }
            }
            catch (Exception ex) { }
            this.App.MainWindow = win;
            this.App.MainWindow.Activate();
            (win.AppWindow.Presenter as OverlappedPresenter)!.SetBorderAndTitleBar(true, false);
            this.App.MainWindow.AppWindow.Closing += AppWindow_Closing;
            await InitGameCoreAsync();
            await CreateJumpListAsync();
        }
        catch (Exception ex)
        {
            LoggerService.WriteError(ex.Message);
            WindowExtension.MessageBox(
                IntPtr.Zero,
                LanguageService.GetStringByText("出现故障性错误，请检查网络连接和日志！关闭当前消息自动打开日志文件夹"),
                "Haiyu",
                0
            );
            WindowExtension.ShellExecute(
                IntPtr.Zero,
                "open",
                AppSettings.BassFolder + "\\appLogs",
                null,
                null,
                WindowExtension.SW_SHOWNORMAL
            );
            Process.GetCurrentProcess().Kill();
        }
    }

    private async Task InitGameCoreAsync()
    {
        foreach (var item in GameContextFactory.GetAllLocalContextName())
        {
            var context = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(item);
            await context.InitAsync();
        }
        foreach (var item in GameContextFactory.GetAllCloudContextName())
        {
            var context = Instance.Host.Services.GetRequiredKeyedService<IKuroCloudGameContext>(
                item
            );
            await context.InitAsync();
        }
        var wavesCloudService = Instance.Host.Services.GetRequiredService<IWavesCloudGameService>();
        await wavesCloudService.InitAsync();
        await KuroClient.InitAsync();
    }

    private async Task CreateJumpListAsync()
    {
        var jumpList = await JumpList.LoadCurrentAsync();
        #region 鸣潮
        jumpList.Items.Clear();
        foreach (var item in GameContextFactory.GetAllLocalContextName())
        {
            var context = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(item);
            var jumpItem = await AppActivation.CreateJumpListsAndInitCoreAsync(context);
            if (jumpItem != null)
            {
                jumpList.Items.Add(jumpItem);
            }
        }
        #endregion
        await jumpList.SaveAsync();
    }

    private void AppWindow_Closing(
        Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args
    )
    {
        args.Cancel = true;
        Process.GetCurrentProcess().Kill();
    }

    public async Task TryInvokeAsync(Func<Task> action)
    {
        await SafeInvokeAsync(
                this.App.MainWindow.DispatcherQueue,
                action,
                priority: Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal
            )
            .ConfigureAwait(false);
    }

    async Task SafeInvokeAsync(
        DispatcherQueue dispatcher,
        Func<Task> action,
        DispatcherQueuePriority priority = DispatcherQueuePriority.Normal
    )
    {
        try
        {
            if (dispatcher.HasThreadAccess)
            {
                await action().ConfigureAwait(false);
            }
            else
            {
                await dispatcher.EnqueueAsync(action, priority).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UI操作失败: {ex.Message}");
        }
    }

    public Controls.TitleBar MainTitle { get; private set; }
    public SolidColorBrush StressColor { get; private set; } = new(Colors.DodgerBlue);
    public Color StressShadowColor { get; private set; } = Colors.AliceBlue;
    public SolidColorBrush StessForground { get; private set; } = new(Colors.Black);

    public void SetTitleControl(Controls.TitleBar titleBar)
    {
        this.MainTitle = titleBar;
    }

    public void TryInvoke(Action action)
    {
        this.App.MainWindow.DispatcherQueue.TryEnqueue(() => action.Invoke());
    }

    public void Minimise()
    {
        this.App.MainWindow.Minimize();
    }

    public async Task CloseAsync()
    {
        var close = await AppSettings.GetCloseWindowAsync();
        if (close == "True")
        {
            Environment.Exit(0);
        }
        else if (close == "False")
        {
            this.App.MainWindow.Hide();
        }
        else
        {
            var result = await DialogManager.ShowCloseWindowResult();
            if (result.IsExit)
            {
                Environment.Exit(0);
            }
            else
            {
                this.App.MainWindow.Hide();
            }
        }
    }

    public void MinToTaskbar()
    {
        this.App.MainWindow.Hide();
    }

    public async Task UpdateAppAsync(bool isApply = false, CancellationToken token = default)
    {
        try
        {
            if (DesktopBridge.IsRunningAsMsix())
            {
                return;
            }
            IUpdateService? service = null;
            if ((await AppSettings.GetUpdateTypeAsync()) == "Github")
            {
                service =
                    Instance.Host.Services.GetKeyedService<Haiyu.Plugin.Contracts.IUpdateService>(
                        "GitHub"
                    );
            }
            else
            {
                service =
                    Instance.Host.Services.GetKeyedService<Haiyu.Plugin.Contracts.IUpdateService>(
                        "Mirror"
                    );
            }
            if (service == null)
                return;
            if (await service.CheckProgramUpdateAsync(Haiyu.App.AppVersion, token))
            {
                var info = await service.GetLasterProgramInfoAsync(token);
                if (info != null)
                {
                    if (!isApply && info.Version == await AppSettings.GetSkipAppVersionAsync())
                    {
                        return;
                    }
                    info.IsApply = isApply;
                    await this.DialogManager.ShowUpdateDialog(info);
                }
                else
                {
                    Instance
                        .Host.Services.GetRequiredService<SystemEventPublisher>()
                        .Publish(
                            new SystemMessagerModel() { Message = LanguageService.GetStringByText("获取更新信息失败"), Delay = 5 }
                        );
                }
            }
            else
            {
                Instance
                    .Host.Services.GetRequiredService<SystemEventPublisher>()
                    .Publish(new SystemMessagerModel() { Message = LanguageService.GetStringByText("当前已是最新版本"), Delay = 5 });
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
