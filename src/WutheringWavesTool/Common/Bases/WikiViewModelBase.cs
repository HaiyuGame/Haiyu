namespace Haiyu.Common.Bases;

public partial class WikiViewModelBase : ViewModelBase
{
    public WikiViewModelBase()
    {
        GameWikiClient = Instance.Host.Services.GetRequiredService<IGameWikiClient>();
        WavesClient = Instance.Host.Services.GetRequiredService<IKuroClient>();
        AccountService = Instance.Host.Services.GetRequiredService<IKuroAccountService>();
        this.TipShow = Instance.Host.Services.GetRequiredService<ITipShow>();
    }

    public IGameWikiClient GameWikiClient { get; }

    public IKuroClient WavesClient { get;  }
    public IKuroAccountService AccountService { get; }

    public ITipShow TipShow { get; }

    [ObservableProperty]
    public partial bool IsLogin { get; set; }
}
