using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.GameViewModels.GameContexts;

public partial class WavesV2GameContextViewModel : KuroGameContextViewModelV2
{
    public WavesV2GameContextViewModel(IAppContext<App> appContext, ITipShow tipShow)
        : base(appContext, tipShow) { }

    public override GameType GameType => GameType.Waves;

    public override async void DisposeAfter() { }

    public override async Task LoadAfter() { }

    public override async Task ShowCardAsync(bool showCard) { }
}
