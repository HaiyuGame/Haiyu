using Haiyu.Common.Contracts;
using Haiyu.ViewModel.ToolkitsViewModel;

namespace Haiyu.Pages.Toolkits;

public sealed partial class AutoKuroTokenPage : Page
{
    private bool _disposed;

    public AutoKuroTokenPage(AutoKuroTokenViewModel viewModel)
    {
        InitializeComponent();
        this.ViewModel = viewModel;
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        if (this.Content is Panel panel)
        {
            this.ViewModel.WindowSession.Context.TipShow.Owner = panel;
        }
    }

    public AutoKuroTokenViewModel? ViewModel { get; private set; }

}
