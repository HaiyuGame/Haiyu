using CommunityToolkit.WinUI;
using Haiyu.Plugin.Contracts;
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
        [FromKeyedServices("AppLog")] LoggerService loggerService,
        IWindowManager windowManager,
        IAppActivation appActivation,
        ABIRuntimeService aBIRuntimeService
    )
    {
        KuroClient = wavesClient;
        WallpaperService = wallpaperService;
        LoggerService = loggerService;
        WindowManager = windowManager;
        AppActivation = appActivation;
        ABIRuntimeService = aBIRuntimeService;
    }

    private ContentDialog _dialog;

    public T App { get; private set; }

    public IKuroClient KuroClient { get; }
    public IWallpaperService WallpaperService { get; }
    public LoggerService LoggerService { get; }
    public IWindowManager WindowManager { get; }
    public IAppActivation AppActivation { get; }
    public ABIRuntimeService ABIRuntimeService { get; }

    public async Task LauncherAsync(T app)
    {
        try
        {
            
            var xboxConfig = Instance.Host.Services.GetRequiredService<XBoxConfig>();
            if ((await xboxConfig.GetIsEnableAsync()) == true)
            {
                await Instance.Host.Services.GetRequiredService<XBoxService>().StartAsync();
            }
            this.App = app;
            #region Mirror
            if (
                Instance.Host.Services.GetRequiredKeyedService<IUpdateService>("Mirror")
                is IMirrorUpdateService mirror
            )
            {
                mirror.SetMirrorKey(await WindowManager.AppSettings.GetMirrorKeyAsync());
            }
            #endregion
            try
            {
                await WindowManager.CreateShellWindowAsync();
            }
            catch (Exception ex) { }
            WindowManager.Shell.GetWindow().Activate();
            await InitGameCoreAsync();
            await CreateJumpListAsync();
        }
        catch (Exception ex)
        {
            LoggerService.WriteError(ex.Message);
            WindowExtension.MessageBox(
                IntPtr.Zero,
                LanguageService.GetStringByText(
                    "出现故障性错误，请检查网络连接和日志！关闭当前消息自动打开日志文件夹"
                ),
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
            if ((await WindowManager.AppSettings.GetUpdateTypeAsync()) == "Github")
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
                    if (!isApply && info.Version == await WindowManager.AppSettings.GetSkipAppVersionAsync())
                    {
                        return;
                    }
                    info.IsApply = isApply;
                    if(WindowManager.Shell == null)
                    {
                        return;
                    }
                    else
                    {
                        await this.WindowManager.Shell.DialogManager.ShowUpdateDialog(info);
                    }
                }
                else
                {
                    Instance
                        .Host.Services.GetRequiredService<SystemEventPublisher>()
                        .Publish(
                            new SystemMessagerModel()
                            {
                                Message = LanguageService.GetStringByText("获取更新信息失败"),
                                Delay = 5,
                            }
                        );
                }
            }
            else
            {
                Instance
                    .Host.Services.GetRequiredService<SystemEventPublisher>()
                    .Publish(
                        new SystemMessagerModel()
                        {
                            Message = LanguageService.GetStringByText("当前已是最新版本"),
                            Delay = 5,
                        }
                    );
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
