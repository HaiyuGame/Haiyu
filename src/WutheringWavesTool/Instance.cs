using Haiyu.Helpers;
using Haiyu.ServiceHost;
using Haiyu.Services.DialogServices;
using Haiyu.Services.Navigations.NavigationViewServices;
using Haiyu.ViewModel.GameViewModels;
using Haiyu.ViewModel.GameViewModels.GameContexts;
using Haiyu.ViewModel.OOBEViewModels;
using Haiyu.ViewModel.WikiViewModels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Waves.Api.Models.Rpc;
using Waves.Core.Services;

namespace Haiyu;

public static class Instance
{
    public static IHost Host { get; private set; }

    public static void InitService()
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().AppBuilder().Build();
        Task.Run(async () => await Host.RunAsync());
    }

    public static T? GetService<T>()
        where T : notnull
    {
        if (Host.Services.GetRequiredService<T>() is not T v)
        {
            throw new ArgumentException("服务未注入");
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
                                s.GetRequiredService<ILogger<RpcService>>()
                            );
                            service.RegisterMethod(
                                s.GetRequiredService<IRpcMethodService>().Method
                            );
                            return service;
                        }
                    )
                    .AddSingleton<AppSettings>()
                    .AddHostedService<XBoxService>()
                    .AddTransient<IRpcMethodService, RpcMethodService>()
                    .AddSingleton<ShellPage>()
                    .AddSingleton<ShellViewModel>()
                    .AddSingleton<OOBEPage>()
                    .AddSingleton<OOBEViewModel>()
                    .AddTransient<CommunityPage>()
                    .AddTransient<PlayerRecordPage>()
                    .AddTransient<PlayerRecordViewModel>()
                    .AddTransient<SettingViewModel>()
                    .AddTransient<CommunityViewModel>()
                    .AddTransient<GameResourceDialog>()
                    .AddTransient<GameResourceViewModel>()
                    .AddTransient<DeviceInfoPage>()
                    .AddTransient<DeviceInfoViewModel>()
                    .AddTransient<ResourceBriefViewModel>()
                    .AddTransient<CloudGameViewModel>()
                    .AddTransient<ColorFullGame>()
                    .AddTransient<ColorFullViewModel>()
                    .AddTransient<StartColorFullGamePage>()
                    .AddTransient<StartColorFullGameViewModel>()
                    .AddTransient<AnalysisRecordViewModel>()
                    .AddTransient<AnalysisRecordPage>()
                    .AddTransient<HomeViewModel>()
                    .AddTransient<LanguageSelectViewModel>()
                    #region ColorGame
                    #endregion
                    #region GameContext
                    .AddTransient<WavesGameContextViewModel>()
                    .AddTransient<PunishGameContextViewModel>()
                    #endregion
                    #region Wiki
                    .AddTransient<WavesWikiViewModel>()
                    .AddTransient<PunishWikiViewModel>()
                    #endregion
                    #region Community
                    .AddTransient<GamerSignPage>()
                    .AddTransient<GamerSignViewModel>()
                    .AddTransient<GamerRoilsDetilyViewModel>()
                    .AddTransient<GameRoilsViewModel>()
                    .AddTransient<GamerDockViewModel>()
                    .AddTransient<GamerChallengeViewModel>()
                    .AddTransient<GamerExploreIndexViewModel>()
                    .AddTransient<GamerTowerViewModel>()
                    .AddTransient<GamerSkinViewModel>()
                    .AddTransient<GamerSlashDetailViewModel>()
                #endregion
                #region Record
                    .AddTransient<RecordItemViewModel>()
                    #endregion
                    #region Roil
                    .AddTransient<GamerRoilsDetilyPage>()
                    .AddTransient<GamerRoilViewModel>()
                    #endregion
                    #region Dialog
                    .AddTransient<LoginDialog>()
                    .AddTransient<LoginGameViewModel>()
                    .AddTransient<GameLauncherCacheManager>()
                    .AddTransient<GameLauncherCacheViewModel>()
                    .AddTransient<WebGameLogin>()
                    .AddTransient<WebGameViewModel>()
                    .AddTransient<SelectGameFolderDialog>()
                    .AddTransient<SelectGameFolderViewModel>()
                    .AddTransient<CloseDialog>()
                    .AddTransient<SelectDownoadGameDialog>()
                    .AddTransient<SelectDownloadGameViewModel>()
                    .AddTransient<QRLoginDialog>()
                    .AddTransient<QrLoginViewModel>()
                    .AddTransient<UpdateGameDialog>()
                    .AddTransient<UpdateGameViewModel>()
                    .AddTransient<LocalUserManagerDialog>()
                    .AddTransient<LocalUserManagerViewModel>()
                #endregion
                #endregion
                #region More
                    .AddTransient<IPageService, PageService>()
                    .AddTransient<IPickersService, PickersService>()
                    .AddSingleton<ITipShow, TipShow>()
                    .AddKeyedTransient<ITipShow, PageTipShow>("Cache")
                    .AddKeyedTransient<IDialogManager, MainDialogService>("Cache")
                    .AddTransient<IColorGameManager, ColorGameManager>()
                    #endregion
                    #region Base
                    .AddSingleton<IAppContext<App>, AppContext<App>>()
                    .AddSingleton<IKuroClient, KuroClient>()
                    .AddTransient<IPlayerCardService, PlayerCardService>()
                    .AddSingleton<ICloudGameService, CloudGameService>()
                    .AddSingleton<IScreenCaptureService, ScreenCaptureService>()
                    .AddSingleton<IGameWikiClient, GameWikiClient>()
                    .AddTransient<IViewFactorys, ViewFactorys>()
                    .AddSingleton<IThemeService, ThemeService>()
                    .AddSingleton<IKuroAccountService,KuroAccountService>()
                    .AddTransient<ILauncherTaskService, LauncherTaskService>()
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
}
