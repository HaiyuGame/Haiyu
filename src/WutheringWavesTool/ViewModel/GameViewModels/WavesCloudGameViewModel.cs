using Haiyu.Services.DialogServices;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts.CloudGame;

namespace Haiyu.ViewModel.GameViewModels;

public sealed partial class WavesCloudGameViewModel : ViewModelBase
{
    public IKuroCloudGameContext KuroCloudGameContext { get; }
    public IDialogManager DialogManager { get; }
    public IWallpaperService WallpaperService { get; }

    [ObservableProperty]
    public partial ObservableCollection<LoginData> Logins { get; set; }

    public WavesCloudGameViewModel(
        IWallpaperService wallpaperService,
        IKuroCloudGameContext kuroCloudGameContext,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager
    )
    {
        WallpaperService = wallpaperService;
        KuroCloudGameContext = kuroCloudGameContext;
        DialogManager = dialogManager;
        RegisterMessager();
    }

    private void RegisterMessager()
    {
        this.Messenger.Register<CloudLoginMessager>(this, CloudLoginMethod);
    }

    private async void CloudLoginMethod(object recipient, CloudLoginMessager message)
    {
        await this.RefreshUserAsync();
    }

    async Task RefreshUserAsync()
    {
        var users = await KuroCloudGameContext.CloudGameService.ConfigManager.GetUsersAsync(
            this.CTS.Token
        );
        this.Logins = [.. users];
    }

    [RelayCommand]
    async Task Loaded()
    {
        WallpaperService.SetMediaForUrl(
            Waves.Core.Models.Enums.WallpaperShowType.Image,
            "https://aki-gm-resources-back.aki-game.com/pv/cg/login.webp"
        );
        await KuroCloudGameContext.CheckLocalUserAsync(this.CTS.Token);
    }

    [RelayCommand]
    async Task AddUserAsync()
    {
        await DialogManager.ShowWebGameDialogAsync();
    }
}
