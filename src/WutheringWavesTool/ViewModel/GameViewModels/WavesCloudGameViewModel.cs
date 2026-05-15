using Waves.Core.Contracts.CloudGame;

namespace Haiyu.ViewModel.GameViewModels;

public sealed partial class WavesCloudGameViewModel:ViewModelBase
{
    public IKuroCloudGameContext KuroCloudGameContext { get; }
    public IWallpaperService WallpaperService { get; }

    public WavesCloudGameViewModel(IWallpaperService wallpaperService)
    {
        WallpaperService = wallpaperService;
        
    }

    [RelayCommand]
    async Task Loaded()
    {
        WallpaperService.SetMediaForUrl(Waves.Core.Models.Enums.WallpaperShowType.Image, "https://aki-gm-resources-back.aki-game.com/pv/cg/login.webp");
    }
}
