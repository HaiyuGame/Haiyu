using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Services;
using Haiyu.KuroClient;
using Haiyu.Mobile.Contracts;
using Haiyu.Mobile.Services;
using Haiyu.Mobile.ViewModels;
using Haiyu.Mobile.ViewModels.ButtonSheet;
using Haiyu.Mobile.ViewModels.ItemsViewModles;
using Haiyu.Mobile.ViewModels.Popups;
using Haiyu.Mobile.Views;
using Haiyu.Mobile.Views.ButtonSheets;
using Haiyu.Mobile.Views.Popups;
using Microsoft.Extensions.Logging;
using Plugin.Maui.BottomSheet.Hosting;
using ZXing.Net.Maui.Controls;

namespace Haiyu.Mobile;

public static class MauiProgram
{
    public static string MobileBaseFolder => FileSystem.Current.AppDataDirectory;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseBottomSheet()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .UseBarcodeReader();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.RegisterApp();
        builder.RegisterView();
        return builder.Build();
    }

    public static MauiAppBuilder RegisterApp(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IMobileLocalAccountService, MobileLocalAccountService>();
        builder.Services.AddSingleton<ItemFactory>();
        return builder;
    }

    public static MauiAppBuilder RegisterView(this MauiAppBuilder builder)
    {
        #region Popup
        builder.Services.AddSingleton<IKuroClient, KuroClient.KuroClient>();
        builder.Services.AddTransientPopup<LoginGeetPopup, LoginGeetViewModel>();
        builder.Services.AddTransientPopup<AddKuroTokenPopup, AddKuroTokenViewModel>();
        #endregion

        #region ButtonSheet
        builder.Services.AddBottomSheet<AccountMoreSessionButtomSheet,AccountMoreSessionViewModel>("accountMore");
        #endregion

        #region ItemViewModel
        builder.Services.AddTransient<LocalUserItemViewModel>();
        #endregion

        builder.Services.AddTransient<AddKuroUserPage>();
        builder.Services.AddTransient<AddKuroUserViewModel>();
        builder.Services.AddTransient<ScanGameQrPage>();
        builder.Services.AddTransient<ScanGameQrViewModel>();
        builder.Services.AddTransient<UserManagerPage>();
        builder.Services.AddTransient<UserManagerViewModel>();
        builder.Services.AddTransient<SettingPage>();
        builder.Services.AddTransient<SettingViewModel>();
        return builder;
    }
}
