using Haiyu.Mobile.Views;

namespace Haiyu.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(AddKuroUserPage), typeof(AddKuroUserPage));
            Routing.RegisterRoute(nameof(SettingPage), typeof(SettingPage));
            Routing.RegisterRoute(nameof(UserManagerPage), typeof(UserManagerPage));
        }
    }
}
