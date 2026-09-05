using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts.CloudGame;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class CloudSelectNodeViewModel : DialogViewModelBase
{
    public IWavesCloudGameService KuroCloudGameContext { get; }

    public CloudSelectNodeViewModel(
        DialogSession dialogSession,
        IWavesCloudGameService kuroCloudGameContext
    )
        : base(dialogSession)
    {
        this.KuroCloudGameContext = kuroCloudGameContext;
    }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CloudGameNode> Nodes { get; set; }

    [ObservableProperty]
    public partial CloudGameNode? SelectNode { get; set; }

    public string Id { get; set; }

    [RelayCommand]
    private async Task RefreshNodesAsync()
    {
        IsRefreshing = true;
        var session = await this.KuroCloudGameContext.GetCurrentUserSession();
        if (session == null)
        {
            SelectNode = null;
            this.Result = new LauncheNodeConfig()
            {
                Nodes = Nodes,
                SelectNode = SelectNode
            };
            await this.CloseAsync(Result);
            this.Dispose();
            return;
        }

        var nodes = await KuroCloudGameContext.GetPingGameNodeAsync(session, this.CTS.Token);
        this.Nodes = new(nodes.Data);
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task Invoke()
    {
        await this.Close();
    }

    [RelayCommand]
    private async Task CloseDialog()
    {
        this.SelectNode = null;
        await this.Close();
    }
}
