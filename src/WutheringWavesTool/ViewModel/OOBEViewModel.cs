using Haiyu.ViewModel.OOBEViewModels;
using Microsoft.Windows.AppLifecycle;
using Waves.Settings;

namespace Haiyu.ViewModel;

public sealed partial class OOBEViewModel:ViewModelBase
{
    public OOBEViewModel(
        [FromKeyedServices(nameof(OOBENavigationService))] INavigationService navigationService,
        AppSettings appSettings)
    {
        NavigationService = navigationService;
        AppSettings = appSettings;
        RegisterManager();
    }

    public INavigationService NavigationService { get; }
    private AppSettings AppSettings { get; }
    public OOBEArgsMessager CurrentArgs { get; private set; }

    [ObservableProperty]
    public partial bool IsNext { get; set;}

    [ObservableProperty]
    public partial bool IsForward { get; set; }

    private void RegisterManager()
    {
        this.Messenger.Register<OOBEArgsMessager>(this,OOBEArgsMethod);
    }

    private void OOBEArgsMethod(object recipient, OOBEArgsMessager message)
    {
        this.CurrentArgs = message;
        this.IsForward = message.IsBack;
        this.IsNext = message.IsNext;
    }


    [RelayCommand]
    public void Loaded()
    {
        this.NavigationService.NavigationTo<LanguageSelectViewModel>(null, new DrillInNavigationTransitionInfo());
    }

    [RelayCommand]
    public async Task NextAsync()
    {
        await AppSettings.SetAutoOOBEAsync(false);
        AppInstance.Restart(null);
    }

    [RelayCommand]
    public void Forward()
    {
        if (this.CurrentArgs == null)
        {
            return;
        }
        this.NavigationService.NavigationTo(CurrentArgs.ForwardPage, null, new DrillInNavigationTransitionInfo());
    }
}
