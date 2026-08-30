using Haiyu.Plugin.Extensions;
using Waves.Settings;
using Windows.Graphics.Imaging;
using WinUIEx.Messaging;

namespace Haiyu.Services;

public class ScreenCaptureService : IScreenCaptureService
{
    private WinUIEx.Messaging.WindowMessageMonitor? _monitor;

    private const int HOTKEY_ID = 141;
    public IAppContext<App> AppContext { get; }
    public AppSettings AppSettings { get; }

    public ScreenCaptureService(IAppContext<App> appContext,AppSettings appSettings)
    {
        AppContext = appContext;
        AppSettings = appSettings;
        _monitor = new WinUIEx.Messaging.WindowMessageMonitor(AppContext.App.MainWindow);
        _monitor.WindowMessageReceived += Monitor_WindowMessageReceived;
    }

    public (bool, string) Register()
    {
        Unregister();
        int? modifier = 0;
        int? key = 0;
        var captureModifierKey = AppSettings.GetCaptureModifierKeyAsync().GetAwaiter().GetResult();
        var captureKey = AppSettings.GetCaptureKeyAsync().GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(captureModifierKey) || string.IsNullOrWhiteSpace(captureKey))
        {
            modifier = ModifierKey.GetDefault().Where(x => x.Name == "Win").FirstOrDefault()?.Value;
            key = Keys.GetDefault().Where(x => x.Name == "F8").FirstOrDefault()?.Value;
        }
        else
        {
            modifier = ModifierKey.GetDefault().Where(x => x.Name == captureModifierKey).FirstOrDefault()?.Value;
            key = Keys.GetDefault().Where(x => x.Name == captureKey).FirstOrDefault()?.Value;
        }
        if (modifier == null || key == null)
        {
            return (false, LanguageService.GetStringByText("热键失效！"));
        }
        bool success = Haiyu.Plugin.Extensions.NativeMethods.RegisterHotKey(
               AppContext.App.MainWindow.GetWindowHandle(),
               HOTKEY_ID,
               modifier.Value,
               key.Value
           );
        return (false, LanguageService.FormatByText(LanguageService.GetStringByText("快捷键注册")) + (success ? LanguageService.GetStringByText("成功") : LanguageService.GetStringByText("失败")));
    }

    private void Monitor_WindowMessageReceived(object sender, WinUIEx.Messaging.WindowMessageEventArgs e)
    {
        if (e.Message.MessageId == 0x0312)
        {
            if (e.Message.WParam == HOTKEY_ID)
            {
                if (!Convert.ToBoolean(AppSettings.GetIsCaptureAsync().GetAwaiter().GetResult()))
                    return;
                Task.Run(Capture).GetAwaiter().GetResult();
            }
        }
    }

    private async Task Capture()
    {
        int width,
                height;
        byte[] rawPixels = ScreenCapture.CaptureScreenPixels(out width, out height);
        await ScreenCapture.SaveRawPixelsToFileAsync(
            rawPixels,
            width,
            height,
            AppSettings.ScreenCaptures + "\\" + DateTime.Now.ToString("yyyyMMddHHmmssff") + ".png",
            BitmapEncoder.PngEncoderId
        );
    }

    public void Unregister()
    {
        Haiyu.Plugin.Extensions.NativeMethods.UnregisterHotKey(AppContext.App.MainWindow.GetWindowHandle(), HOTKEY_ID);
    }
}
