using CommunityToolkit.WinUI;
using Haiyu.Services.DialogServices;
using Microsoft.UI.Dispatching;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.Services;
using Waves.Core.Settings;
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
        AppSettings appSettings
    )
    {
        KuroClient = wavesClient;
        WallpaperService = wallpaperService;
        DialogManager = dialogManager;
        LoggerService = loggerService;
        AppSettings = appSettings;
        WallpaperService.WallpaperPletteChanged += WallpaperService_WallpaperPletteChanged;
    }

    private ContentDialog _dialog;

    public T App { get; private set; }

    public IKuroClient KuroClient { get; }
    public IWallpaperService WallpaperService { get; }
    public IDialogManager DialogManager { get; }
    public LoggerService LoggerService { get; }
    public AppSettings AppSettings { get; }

    public async Task LauncherAsync(T app)
    {
        try
        {
            var xboxConfig = Instance.Host.Services.GetRequiredService<XBoxConfig>();
            if (xboxConfig.IsEnable == true)
            {
                await Instance.Host.Services.GetRequiredService<XBoxService>().StartAsync();
            }
            await Instance.Host.Services.GetRequiredService<IKuroClient>().InitAsync();
            await Instance.Host.Services!.GetRequiredKeyedService<IGameContext>(nameof(WavesMainGameContext))
                .InitAsync();
            await Instance.Host.Services!.GetRequiredKeyedService<IGameContext>(nameof(WavesBiliBiliGameContext))
                .InitAsync();
            await Instance.Host.Services!.GetRequiredKeyedService<IGameContext>(nameof(WavesGlobalGameContext))
                .InitAsync();
            await Instance.Host.Services!.GetRequiredKeyedService<IGameContext>(nameof(PunishMainGameContext))
                .InitAsync();
            await Instance.Host.Services!.GetRequiredKeyedService<IGameContext>(nameof(PunishBiliBiliGameContext))
                .InitAsync();
            await Instance.Host.Services!.GetRequiredKeyedService<IGameContext>(nameof(PunishGlobalGameContext))
                .InitAsync();
            await Instance.Host.Services!.GetRequiredKeyedService<IGameContext>(nameof(PunishTwGameContext))
                .InitAsync();
            this.App = app;
            var win = new MainWindow();
            
            try
            {
                var scale = TitleBar.GetScaleAdjustment(win);
                //if (string.IsNullOrWhiteSpace(AppSettings.AutoOOBE))
                //{
                //    int targetDipWidth = 800;
                //    int targetDipHeight = 500;
                //    var pixelWidth = (int)Math.Round(targetDipWidth * scale);
                //    var pixelHeight = (int)Math.Round(targetDipHeight * scale);
                //    win.AppWindow.Resize(
                //        new Windows.Graphics.SizeInt32 { Width = pixelWidth, Height = pixelHeight }
                //    );
                //    var page = Instance.Host.Services!.GetRequiredService<OOBEPage>();
                //    page.titlebar.Window = win;
                //    win.Content = page;
                //}
                //else
                //{
                //    int targetDipWidth = 1150;
                //    int targetDipHeight = 650;
                //    var pixelWidth = (int)Math.Round(targetDipWidth * scale);
                //    var pixelHeight = (int)Math.Round(targetDipHeight * scale);
                //    win.AppWindow.Resize(
                //        new Windows.Graphics.SizeInt32 { Width = pixelWidth, Height = pixelHeight }
                //    );
                //    var page = Instance.Host.Services!.GetRequiredService<ShellPage>();
                //    page.titlebar.Window = win;
                //    win.Content = page;
                //}
                int targetDipWidth = 1150;
                int targetDipHeight = 650;
                var pixelWidth = (int)Math.Round(targetDipWidth * scale);
                var pixelHeight = (int)Math.Round(targetDipHeight * scale);
                win.AppWindow.Resize(
                    new Windows.Graphics.SizeInt32 { Width = pixelWidth, Height = pixelHeight }
                );
                var page = Instance.Host.Services!.GetRequiredService<ShellPage>();
                page.titlebar.Window = win;
                win.Content = page;
            }
            catch(Exception ex)
            {
                win.MaxWidth = 1100;
                win.MaxHeight = 700;
            }

            win.IsResizable = false;
            win.IsMaximizable = false;
            this.App.MainWindow = win;
            this.App.MainWindow.Activate();
            (win.AppWindow.Presenter as OverlappedPresenter)!.SetBorderAndTitleBar(true, false);
            this.App.MainWindow.AppWindow.Closing += AppWindow_Closing;
        }
        catch (Exception ex)
        {
            LoggerService.WriteError(ex.Message);
            WindowExtension.MessageBox(IntPtr.Zero,"出现故障性错误，请检查网络连接和日志！关闭当前消息自动打开日志文件夹","Haiyu",0);
            WindowExtension.ShellExecute(IntPtr.Zero, "open", AppSettings.BassFolder+"\\appLogs", null, null, WindowExtension.SW_SHOWNORMAL);
            Process.GetCurrentProcess().Kill();
        }

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

    private void WallpaperService_WallpaperPletteChanged(object sender, PletteArgs color)
    {
        if (color.Background == null || color.Forground == null || color.Shadow == null)
            return;
        this.StressColor = new SolidColorBrush(color.Background.Value);
        this.StressShadowColor = color.Shadow.Value;
        this.StessForground = new SolidColorBrush(color.Forground.Value);
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
        var close = AppSettings.CloseWindow;
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
}
