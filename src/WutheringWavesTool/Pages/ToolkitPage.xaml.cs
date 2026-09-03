using Haiyu.Common.Contracts;

namespace Haiyu.Pages;

public sealed partial class ToolkitPage : Page, IPage
{
    public ToolkitPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<ToolkitViewModel>();
    }

    public Type PageType => typeof(ToolkitPage);

    public ToolkitViewModel? ViewModel { get; private set; }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        try
        {
            this.Bindings.StopTracking();
            this.ViewModel?.Dispose();
        }
        finally
        {
            this.ViewModel = null;
            base.OnNavigatedFrom(e);
        }
    }
}
