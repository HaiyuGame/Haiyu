
using Microsoft.Windows.ApplicationModel.Resources;

namespace Haiyu.Pages;

public sealed partial class CloudGamePage : Page, IPage
{
    public CloudGamePage()
    {
        InitializeComponent();
        this.ViewModel = Instance.GetService<CloudGameViewModel>();
    }

    public Type PageType => typeof(CloudGamePage);

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        this.ViewModel.Dispose();
        base.OnNavigatedFrom(e);
    }
    public CloudGameViewModel ViewModel { get; }
}
