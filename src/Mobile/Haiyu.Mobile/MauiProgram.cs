using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Services;
using Haiyu.Mobile.ViewModels;
using Haiyu.Mobile.ViewModels.Popups;
using Haiyu.Mobile.Views;
using Haiyu.Mobile.Views.Popups;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace Haiyu.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
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
            return builder;
        }

        public static MauiAppBuilder RegisterView(this MauiAppBuilder builder)
        {
            #region Popup
            builder.Services.AddTransientPopup<LoginGeetPopup,LoginGeetViewModel>();
            #endregion

            builder.Services.AddTransient<AddKuroUserPage>();
            builder.Services.AddTransient<AddKuroUserViewModel>();
            builder.Services.AddTransient<UserManagerPage>();
            builder.Services.AddTransient<UserManagerViewModel>();
            builder.Services.AddTransient<SettingPage>();
            builder.Services.AddTransient<SettingViewModel>();
            return builder;
        }
    }
}
