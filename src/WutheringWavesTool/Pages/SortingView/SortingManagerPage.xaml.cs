using Haiyu.Common.Contracts;
using Haiyu.Model;
using Haiyu.ViewModel.SortingViewModels;

namespace Haiyu.Pages.SortingView;

public sealed partial class SortingManagerPage : Page, IWindowInitializable, IDisposable
{
    private readonly WindowSession session;
    private bool disposed;
    public SortingManagerViewModel ViewModel { get; }

    public SortingManagerPage(SortingManagerViewModel viewModel, WindowSession session)
    {
        ViewModel = viewModel;
        this.session = session;
        InitializeComponent();
        RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        Loaded += OnLoaded;
    }

    public void Initialize()
    {
        TitleBar.Window = session.Context.GetWindow();
        if (XamlRoot is not null)
            session.Context.DialogManager.RegisterRoot(XamlRoot);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Initialize();
        await ViewModel.RefreshAsync();
    }

    private async void Template_SelectionChanged(
        ItemsView sender,
        ItemsViewSelectionChangedEventArgs args
    )
    {
        if (
            disposed
            || ViewModel.IsBusy
            || sender.SelectedItem is not SortingTemplateCard card
            || ViewModel.SelectedTemplate == card
        )
            return;
        ViewModel.Select(card);
        await ViewModel.ScoreAsync();
    }

    public Visibility Empty(bool hasItems) => hasItems ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility Shown(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Hidden(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    public static BitmapImage? LoadImage(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            ? new BitmapImage(uri)
            : null;

    private void Image_Failed(object sender, ExceptionRoutedEventArgs e) =>
        ((Image)sender).Source = null;

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
