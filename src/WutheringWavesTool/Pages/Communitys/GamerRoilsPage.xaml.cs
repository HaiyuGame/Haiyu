namespace Haiyu.Pages.Communitys;

public sealed partial class GamerRoilsPage : Page, IPage,IDisposable
{
    private GameRoilsViewModel viewModel;
    private bool disposedValue;

    public GamerRoilsPage()
    {
        this.InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<GameRoilsViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        
        if (e.Parameter is GameRoilDataItem item)
        {
            await this.ViewModel.SetDataAsync(item);
        }
        
        base.OnNavigatedTo(e);
    }


    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        Dispose();
        base.OnNavigatedFrom(e);
    }

    public void Dispose()
    {
        this.ViewModel.Dispose();
        GC.Collect();
    }

    public Type PageType => typeof(GamerRoilsPage);

    public GameRoilsViewModel ViewModel
    {
        get => viewModel;
        set => viewModel = value;
    }

   
}
