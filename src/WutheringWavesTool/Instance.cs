using Cacheing;
using Haiyu.Common.Contracts;
using Haiyu.Helpers;
using Haiyu.Pages.Communitys;
using Haiyu.Pages.Toolkits;
using Haiyu.Plugin.Common;
using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Services;
using Haiyu.ServiceHost;
using Haiyu.ServiceHost.Contracts;
using Haiyu.ServiceHost.Services;
using Haiyu.ServiceHost.XBox.Commons;
using Haiyu.Services.Navigations.NavigationViewServices;
using Haiyu.Services.Tasks;
using Haiyu.ViewModel.Communitys;
using Haiyu.ViewModel.GameViewModels;
using Haiyu.ViewModel.GameViewModels.GameContexts;
using Haiyu.ViewModel.OOBEViewModels;
using Haiyu.ViewModel.ToolkitsViewModel;
using Haiyu.ViewModel.WikiViewModels;
using MemoryPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Waves.Api.Models.CloudGame;
using Waves.Api.Models.Record;
using Waves.Api.Models.Wrappers;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models;
using Waves.Core.Services;
using Waves.Core.Services.CloudGameServices;
using Waves.Settings;

namespace Haiyu;

public static class Instance
{
    public static IHost Host { get; private set; }

    public static async Task InitServiceAsync()
    {
        EnsureMemoryPackFormatters();
        Host = Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder()
            .RegisterCache()
            .AppBuilder()
            .Build();
        _ = Task.Run(async () => await Host.StartAsync());
    }

    static void EnsureMemoryPackFormatters()
    {
        MemoryPackFormatterProvider.Register<RecordCardItemWrapper>();
        MemoryPackFormatterProvider.Register<RecordCacheDetily>();
        MemoryPackFormatterProvider.Register<WavesAnalysisPlayerCard>();
        MemoryPackFormatterProvider.Register<WavesAnalysisPlayerCardItem>();
        MemoryPackFormatterProvider.Register<Datum>();
        MemoryPackFormatterProvider.Register<LocalAccount>();
    }

    /// <summary>
    /// 主窗口获取Service位置
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static T? GetService<T>()
        where T : notnull
    {
        var windowManager = Host.Services.GetService<IWindowManager>();
        var shellContext = windowManager?.GetWindowContext(IWindowManager.ShellKey);
        var provider = shellContext?.Service.ServiceProvider ?? Host.Services;

        if (provider.GetRequiredService<T>() is not T v)
        {
            throw new ArgumentException(LanguageService.GetStringByText("服务未注入"));
        }
        return v;
    }
}

public static class InstanceBuilderExtensions
{
    /// <summary>
    /// 构建容器
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IHostBuilder AppBuilder(this IHostBuilder builder)
    {
        builder.ConfigureServices(
            (Service) =>
            {
                #region View and ViewModel
                Service
                    .AddHostedService<RpcService>(
                        (s) =>
                        {
                            RpcService service = new RpcService(
                                s.GetRequiredKeyedService<LoggerService>("AppLog"),
                                s.GetRequiredService<RpcSettings>()
                            );
                            service.RegisterMethod(
                                s.GetRequiredService<IRpcMethodService>().Method
                            );
                            return service;
                        }
                    )
                    .AddSingleton<AppSettings>()
                    .AddSingleton<GithubIpSettings>()
                    .AddSingleton<RpcSettings>()
                    .AddTransient<IAppActivation, AppActivation>()
                    #region XBox
                    .AddSingleton<XBoxConfig>()
                    .AddSingleton<XBoxController>()
                    .AddSingleton<XBoxService>()
                    #endregion
                    .AddTransient<IRpcMethodService, RpcMethodService>()
                    .AddTransient<ShellPage>()
                    .AddTransient<ShellViewModel>()
                    .AddTransient<OOBEPage>()
                    .AddTransient<OOBEViewModel>()
                    .AddTransient<WavesAnalysisRecordPage>()
                    .AddTransient<WavesAnalysisRecordViewModel>()
                    .AddTransient<SettingViewModel>()
                    .AddTransient<GamerSignPage>()
                    .AddTransient<GamerSignViewModel>()
                    .AddTransient<DeviceInfoPage>()
                    .AddTransient<DeviceInfoViewModel>()
                    .AddTransient<HomeViewModel>()
                    .AddTransient<LanguageSelectViewModel>()
                    .AddTransient<CloudGameingViewModel>()
                    #region GameContext
                    .AddTransient<PunishV2GameContextViewModel>()
                    .AddTransient<WavesV2GameContextViewModel>()
                    .AddTransient<WavesCloudGameViewModel>()
                    #endregion
                    #region Wiki
                    .AddTransient<WavesWikiViewModel>()
                    .AddTransient<PunishWikiViewModel>()
                    #endregion
                    #region Dialog
                    .AddTransient<LoginDialog>()
                    .AddTransient<LoginGameViewModel>()
                    .AddTransient<GameLauncherCacheManager>()
                    .AddTransient<GameLauncherCacheViewModel>()
                    .AddTransient<WebGameLogin>()
                    .AddTransient<WebGameViewModel>()
                    .AddTransient<SelectGameFolderDialogV2>()
                    .AddTransient<SelectGameFolderViewModelV2>()
                    .AddTransient<CloseDialog>()
                    .AddTransient<SelectDownoadGameDialogV2>()
                    .AddTransient<QRLoginDialog>()
                    .AddTransient<QrLoginViewModel>()
                    .AddTransient<LocalUserManagerDialog>()
                    .AddTransient<LocalUserManagerViewModel>()
                    .AddTransient<DeleteFileDialog>()
                    .AddTransient<DeleteFileViewModel>()
                    .AddTransient<UpdateGameDialogV2>()
                    .AddTransient<UpdateGameViewModelV2>()
                    .AddTransient<GameResourceDialogV2>()
                    .AddTransient<GameResourceViewModelV2>()
                    .AddTransient<UpdateAppDialog>()
                    .AddTransient<UpdateAppViewModel>()
                    .AddTransient<CloudGameSettingViewModel>()
                    .AddTransient<CloudGameSettingDialog>()
                    .AddTransient<KuroGameSettingDialog>()
                    .AddTransient<KuroGameSettingViewModel>()
                    .AddTransient<LocalGameTokenDialog>()
                    .AddTransient<LocalGameTokenViewModel>()
                    .AddTransient<WebViewCabManagerDialog>()
                    .AddTransient<WavesCloudUserViewModel>()
                    .AddTransient<GameEnhancedDialog>()
                    .AddTransient<GameEnhancedViewModel>()
                    .AddTransient<WavesCloudUserDialog>()
                    .AddTransient<WebViewCabManagerViewModel>()
                    .AddTransient<CloudSelectNodeDialog>()
                    .AddTransient<CloudSelectNodeViewModel>()
                    .AddTransient<ClearMemoryDialog>()
                    .AddTransient<ClearMemoryViewModel>()
                    #endregion
                #endregion
                    #region More
                    .AddTransient<IPageService, PageService>()
                    .AddSingleton<IWavesCloudGameService, WavesCloudGameService>()
                    .AddKeyedSingleton<IUpdateService, GithubUpdateService>("GitHub")
                    .AddKeyedSingleton<IUpdateService, MirrorUpdateService>("Mirror")
                    #endregion
                    #region Base
                    .AddSingleton<IAppContext<App>, AppContext<App>>()
                    .AddSingleton<IKuroClient, global::Haiyu.KuroClient.KuroClient>()
                    .AddTransient<IPlayerCardService, PlayerCardService>()
                    .AddSingleton<IScreenCaptureService, ScreenCaptureService>()
                    .AddSingleton<IGameWikiClient, GameWikiClient>()
                    .AddTransient<IViewFactorys, ViewFactorys>()
                    .AddSingleton<IThemeService, ThemeService>()
                    .AddSingleton<IKuroAccountService, KuroAccountService>()
                    .AddSingleton<AutoKuroGameSignService>()
                    .AddSingleton<AutoKuroClientSignService>()
                    .AddSingleton<ITaskManager, TaskManager>(s => CreateTask(s))
                    .AddSingleton<CloudConfigManager>(
                        (s) =>
                        {
                            var mananger = new CloudConfigManager(AppSettings.CloudFolderPath);
                            return mananger;
                        }
                    )
                    .AddSingleton<IWallpaperService, WallpaperService>(
                        (s) =>
                        {
                            var service = new WallpaperService(
                                s.GetRequiredService<IWindowManager>()
                            );
                            service.RegisterHostPath(AppSettings.WrallpaperFolder);
                            return service;
                        }
                    )
                    #endregion
                    #region Navigation
                    .AddKeyedSingleton<INavigationService, HomeNavigationService>(
                        nameof(HomeNavigationService)
                    )
                    .AddKeyedSingleton<INavigationViewService, HomeNavigationViewService>(
                        nameof(HomeNavigationViewService)
                    )
                    .AddKeyedTransient<INavigationService, CommunityNavigationService>(
                        nameof(CommunityNavigationService)
                    )
                    .AddKeyedTransient<INavigationService, WebGameNavigationService>(
                        nameof(WebGameNavigationService)
                    )
                    .AddKeyedTransient<INavigationService, GameWikiNavigationService>(
                        nameof(GameWikiNavigationService)
                    )
                    .AddKeyedSingleton<INavigationService, OOBENavigationService>(
                        nameof(OOBENavigationService)
                    )
                    #endregion
                    #region Plugin
                    .AddTransient<IWavesPlayerCardCacheServices, WavesPlayerCardCacheServices>(
                        _ => new WavesPlayerCardCacheServices(AppSettings.WavesRecordFolder)
                    )
                    .AddSingleton<ABIRuntimeService>()
                    #endregion
                    #region Toolkit
                    .AddTransient<ToolkitPage>()
                    .AddTransient<ToolkitViewModel>()
                    .AddTransient<AutoKuroTokenPage>()
                    .AddTransient<AutoKuroTokenViewModel>()
                    .AddTransient<MonitorToolPage>()
                    .AddTransient<MonitorToolViewModel>()
                    .AddTransient<MonitorSettingPage>()
                    .AddTransient<MonitorSettingViewModel>()
                    #endregion
                    #region WindowContext
                    .AddScoped<ITipShow, TipShow>()
                    .AddScoped<IDialogManager, DialogManager>()
                    .AddScoped<IPickersService, NativePickersService>()
                    .AddScoped<DialogSession>()
                    .AddScoped<WindowSession>()
                    .AddScoped<KuroDataCenterWindow>()
                    .AddSingleton<IWindowManager, Services.WindowManager>()
                    #endregion
                    .AddKeyedSingleton<LoggerService>(
                        "AppLog",
                        (s, e) =>
                        {
                            var logger = new LoggerService();
                            logger.InitLogger(AppSettings.LogPath, Serilog.RollingInterval.Day);
                            return logger;
                        }
                    )
                    #region Record
                    .AddScoped<ITipShow, TipShow>()
                    .AddKeyedScoped<INavigationService, RecordNavigationService>(
                        nameof(RecordNavigationService)
                    )
                    .AddScoped<IRecordCacheService, RecordCacheService>()
                    .AddKeyedScoped<IGamerRoilContext, GamerRoilContext>(nameof(GamerRoilContext))
                    .AddKeyedScoped<INavigationService, GameRoilNavigationService>(
                        nameof(GameRoilNavigationService)
                    )
                    #endregion
                    .AddGameContext();
            }
        );
        return builder;
    }

    private static TaskManager CreateTask(IServiceProvider s)
    {
        TaskManager taskManager = new TaskManager(s.GetRequiredService<AppSettings>());
        #region 自动签到
        var autoSignTask = s.GetRequiredService<AutoKuroGameSignService>();
        var kuroTask = s.GetRequiredService<AutoKuroClientSignService>();
        taskManager.RegsiterTask(autoSignTask);
        taskManager.RegsiterTask(kuroTask);
        #endregion
        return taskManager;
    }
}
