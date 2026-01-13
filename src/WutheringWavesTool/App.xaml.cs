using System.Globalization;
using Haiyu.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.AppLifecycle;
using Waves.Core.Services;
using Waves.Core.Settings;
using Windows.Globalization;

namespace Haiyu;

public partial class App : ClientApplication
{
    [DllImport("shcore.dll", SetLastError = true)]
    private static extern int SetProcessDpiAwareness(int dpiAwareness);

    private const int PROCESS_PER_MONITOR_DPI_AWARE = 2;
    private AppInstance mainInstance;

    public static string AppVersion => "1.2.18-preview2";

    public AppSettings AppSettings { get; private set; }

    public App()
    {
        this.InitializeComponent();
        mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey(
            "Haiyu_Main"
        );
        mainInstance.Activated += MainInstance_Activated;
    }

    private void MainInstance_Activated(object sender, AppActivationArguments e)
    {
        if (e.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.File) { }
    }

    void CreateFolder()
    {
        Directory.CreateDirectory(AppSettings.BassFolder);
        Directory.CreateDirectory(AppSettings.RecordFolder);
        Directory.CreateDirectory(AppSettings.ColorGameFolder);
        Directory.CreateDirectory(AppSettings.WrallpaperFolder);
        Directory.CreateDirectory(AppSettings.ScreenCaptures);
        Directory.CreateDirectory(AppSettings.LocalUserFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(AppSettings.LogPath));
        Directory.CreateDirectory(AppSettings.CloudFolderPath);
    }

    private void App_UnhandledException(
        object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs e
    )
    {
        try
        {
            Instance.Host.Services.GetService<ITipShow>().ShowMessage(e.Message, Symbol.Clear);
            Instance.Host.Services.GetKeyedService<LoggerService>("AppLog").WriteError(e.Message);
        }
        catch (Exception ex)
        {
            Instance.Host.Services.GetKeyedService<LoggerService>("AppLog").WriteError(ex.Message);
        }
        finally
        {
            e.Handled = true;
        }
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Instance.InitService();
        Task.Run(async () => await Instance.Host.RunAsync());
        this.AppSettings = Instance.Host.Services.GetRequiredService<AppSettings>();
        this.UnhandledException += App_UnhandledException;
        CreateFolder();
        if (AppSettings.WallpaperType == null)
        {
            AppSettings.WallpaperType = "Video";
        }
        SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE);
        GameContextFactory.GameBassPath = AppSettings.BassFolder;

        Instance.Host.Services.GetKeyedService<LoggerService>("AppLog").WriteInfo("启动程序中……");

        var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey(
            "Haiyu_Main"
        );
        if (!mainInstance.IsCurrent)
        {
            mainInstance.Activated -= MainInstance_Activated;
            var activatedEventArgs = Microsoft
                .Windows.AppLifecycle.AppInstance.GetCurrent()
                .GetActivatedEventArgs();
            await mainInstance.RedirectActivationToAsync(activatedEventArgs);
            Process.GetCurrentProcess().Kill();
            return;
        }
        await LanguageService.InitAsync();
        await Instance.Host.Services.GetRequiredService<IAppContext<App>>().LauncherAsync(this);
        SetTheme();
        Instance.Host.Services.GetService<IScreenCaptureService>()!.Register();
    }

    private void SetTheme()
    {
        var theme = Instance.Host.Services.GetRequiredService<IThemeService>();
        switch (AppSettings.ElementTheme)
        {
            case "Light":
                theme.SetTheme(ElementTheme.Light);
                break;
            case "Dark":
                theme.SetTheme(ElementTheme.Dark);
                break;
            case "Default":
                theme.SetTheme(ElementTheme.Default);
                break;
            case null:
            default:
                theme.SetTheme(ElementTheme.Default);
                break;
        }
    }
}
