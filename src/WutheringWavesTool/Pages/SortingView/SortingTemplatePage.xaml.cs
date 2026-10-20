using Haiyu.Common.Contracts;
using Haiyu.Model;
using Haiyu.ViewModel.SortingViewModels;

namespace Haiyu.Pages.SortingView;

public sealed partial class SortingTemplatePage : Page, IWindowInitializable, IDisposable
{
    private readonly WindowSession session;
    private bool initialized;
    private bool disposed;
    public SortingTemplateViewModel ViewModel { get; }

    public SortingTemplatePage(SortingTemplateViewModel viewModel, WindowSession session)
    {
        ViewModel = viewModel;
        this.session = session;
        InitializeComponent();
        RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        Loaded += OnLoaded;
    }

    public void Initialize()
    {
        if (disposed)
            return;
        TitleBar.Window = session.Context.GetWindow();
        if (XamlRoot is not null)
            session.Context.DialogManager.RegisterRoot(XamlRoot);
        if (initialized)
            return;
        initialized = true;
        ViewModel.Initialize(session.GetParameter<SortingEditorRequest>());
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Initialize();

    private void RemoveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is SortingTargetViewModel row)
            ViewModel.RemoveTargetCommand.Execute(row);
    }

    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is SortingEntryViewModel row)
            ViewModel.EditRuleCommand.Execute(row);
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is SortingEntryViewModel row)
            ViewModel.RemoveRuleCommand.Execute(row);
    }

    private async void Source_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.HasSource)
                await Windows.System.Launcher.LaunchUriAsync(new Uri(ViewModel.SourceUrl));
        }
        catch
        {
            ViewModel.Message = "打开链接失败";
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Loaded -= OnLoaded;
        Bindings.StopTracking();
        ViewModel.Dispose();
    }
}
