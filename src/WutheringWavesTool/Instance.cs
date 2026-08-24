using Haiyu.Helpers;
using Haiyu.Pages.Communitys;
using Haiyu.Pages.Toolkits;
using Haiyu.Plugin.Common;
using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Services;
using Haiyu.ServiceHost;
using Cacheing;
using Haiyu.ServiceHost.XBox.Commons;
using Haiyu.Services.DialogServices;
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
using Waves.Settings;
using Waves.Core.Services.CloudGameServices;

namespace Haiyu;

public static class Instance
{
    public static IHost Host { get; private set; }

    public static async Task InitServiceAsync()
    {
        EnsureMemoryPackFormatters();
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().RegisterCache().AppBuilder().Build();
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

    public static T? GetService<T>()
        where T : notnull
    {
        if (Host.Services.GetRequiredService<T>() is not T v)
        {
            throw new ArgumentException(LanguageService.GetStringByText("服务未注入"));
            ;
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
                    .AddSingleton<ShellPage>()
                    .AddSingleton<ShellViewModel>()
                    .AddSingleton<OOBEPage>()
                    .AddSingleton<OOBEViewModel>()
                    .AddTransient<WavesAnalysisRecordPage>()
                    .AddTransient<WavesAnalysisRecordViewModel>()
                    .AddTransient<SettingViewModel>()
                    .AddTransient<GameEnhancedDialog>()
                    .AddTransient<GameEnhancedViewModel>()
                    .AddTransient<WebViewCabManagerDialog>()
                    .AddTransient<WavesCloudUserViewModel>()
                    .AddTransient<WavesCloudUserDialog>()
                    .AddTransient<WebViewCabManagerViewModel>()
                    .AddTransient<GamerSignPage>()
                    .AddTransient<GamerSignViewModel>()
                    .AddTransient<CloudSelectNodeDialog>()
                    .AddTransient<CloudSelectNodeViewModel>()
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
                    .AddTransient<UpdateGameDialog>()
                    .AddTransient<UpdateGameViewModel>()
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
                    #endregion
                #endregion
                    #region More
                    .AddTransient<IPageService, PageService>()
                    .AddTransient<IPickersService>(serviceProvider => new NativePickersService(() =>
                        WinRT.Interop.WindowNative.GetWindowHandle(
                            serviceProvider.GetRequiredService<IAppContext<App>>().App.MainWindow
                        )
                    ))
                    .AddSingleton<ITipShow, TipShow>()
                    .AddKeyedTransient<ITipShow, PageTipShow>("Cache")
                    .AddKeyedTransient<IDialogManager, MainDialogService>("Cache")
                    .AddSingleton<IWavesCloudGameService, WavesCloudGameService>()
                    .AddKeyedSingleton<IUpdateService, GithubUpdateService>("GitHub")
                    .AddKeyedSingleton<IUpdateService, MirrorUpdateService>("Mirror")
                    #endregion
                    #region Base
                    .AddSingleton<IAppContext<App>, AppContext<App>>()
                    .AddTransient<IKuroClient, global::Haiyu.KuroClient.KuroClient>()
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
                            var service = new WallpaperService(s.GetRequiredService<ITipShow>());
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
                    #endregion
                    #region Toolkit
                    .AddTransient<ToolkitPage>()
                    .AddTransient<ToolkitViewModel>()
                    .AddTransient<AutoKuroTokenPage>()
                    .AddTransient<AutoKuroTokenViewModel>()
                    #endregion
                    .AddKeyedSingleton<IDialogManager, MainDialogService>(nameof(MainDialogService))
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
                    .AddScoped<IDialogManager, ScopeDialogService>()
                    .AddScoped<ITipShow, TipShow>()
                    .AddKeyedScoped<IPlayerRecordContext, PlayerRecordContext>("PlayerRecord")
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
