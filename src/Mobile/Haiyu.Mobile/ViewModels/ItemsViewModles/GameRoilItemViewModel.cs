using CommunityToolkit.Mvvm.Input;
using Haiyu.Mobile.Common;
using Waves.Api.Models.Communitys;
using Waves.Api.Models.Enums;

namespace Haiyu.Mobile.ViewModels.ItemsViewModles;

public partial class GameRoilItemViewModel : ItemsViewModelBase<GameRoilDataItem>
{
    public WikiType Type { get; }
    public string CoverImage { get; private set; }
    public int GamerId { get; private set; }
    public string Id { get; private set; }
    public string GameLevel { get; private set; }
    public string ServerName { get; private set; }
    public string ServerId { get; private set; }
    public GameRoilDataItem BaseData { get; private set; }

    public GameRoilItemViewModel(IServiceProvider serviceProvider)
        : base(serviceProvider) { }

    public override void SetData(GameRoilDataItem args)
    {
        this.BaseData = args;
        this.CoverImage = BaseData.GameHeadUrl;
        this.GamerId = BaseData.GameId;
        this.Id = BaseData.Id;
        this.GameLevel = BaseData.GameLevel;
        this.ServerName = BaseData.ServerName;
        this.ServerId = BaseData.ServerId;
    }

    [RelayCommand]
    async Task GotoGameSession()
    {
        Dictionary<string, object> param = new()
        {
            { "type", Type },
            { "gameRoil", this.BaseData },
        };
    }
}
