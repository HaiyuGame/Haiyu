namespace Haiyu.Pages
{
    public sealed partial class HomePage : Page, IPage
    {
        public HomePage()
        {
            InitializeComponent();
            this.ViewModel = Instance.GetService<HomeViewModel>();

        }

        public Type PageType => typeof(HomePage);

        public HomeViewModel ViewModel { get; }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if(frame.Content is IDisposable disposable)
            {
                disposable.Dispose();
                frame.Content = null;
            }
            this.ViewModel.NavigationService.UnRegisterView();
            ViewModel.Dispose();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.ViewModel.NavigationService.RegisterView(this.frame);
        }


    }
}
