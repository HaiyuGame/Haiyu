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

        public HomeViewModel? ViewModel { get; private set; }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            try
            {
                this.Bindings.StopTracking();
                if (frame.Content is IDisposable disposable)
                {
                    disposable.Dispose();
                    frame.Content = null;
                }
                this.ViewModel?.Dispose();
            }
            finally
            {
                this.ViewModel = null;
                base.OnNavigatedFrom(e);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.ViewModel?.NavigationService.RegisterView(this.frame);
        }


    }
}
