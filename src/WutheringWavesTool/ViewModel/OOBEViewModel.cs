using Haiyu.ViewModel.OOBEViewModels;

namespace Haiyu.ViewModel;

public sealed partial class OOBEViewModel:ViewModelBase
{
    public OOBEViewModel([FromKeyedServices(nameof(OOBENavigationService))]INavigationService navigationService)
    {
        NavigationService = navigationService;
        RegisterManager();
    }

    public INavigationService NavigationService { get; }
    public OOBEArgsMessager CurrentArgs { get; private set; }

    [ObservableProperty]
    public bool IsNext { get; set;}

    [ObservableProperty]
    public bool IsForward { get; set; }

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
    public void Next()
    {
        if(this.CurrentArgs == null)
        {
            return;
        }
        this.NavigationService.NavigationTo(CurrentArgs.NextPage, null, new DrillInNavigationTransitionInfo());
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
