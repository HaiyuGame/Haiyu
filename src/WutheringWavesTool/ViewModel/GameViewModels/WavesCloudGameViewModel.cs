using Haiyu.Services.DialogServices;
using Waves.Api.Models.CloudGame;
using Waves.Core.Common;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Haiyu.ViewModel.GameViewModels;

public sealed partial class WavesCloudGameViewModel : ViewModelBase
{
    public IKuroCloudGameContext KuroCloudGameContext { get; }
    public IDialogManager DialogManager { get; }
    public IAppContext<App> App { get; }
    public IWallpaperService WallpaperService { get; }

    [ObservableProperty]
    public partial ObservableCollection<CloudGameLoginSession> Logins { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial WallDataWrapper WallData { get; set; }

    [ObservableProperty]
    public partial int NodesCount { get; set; }

    [ObservableProperty]
    public partial CloudGameLoginSession SelectLogin { get; set; }

    public WavesCloudGameViewModel(
        IWallpaperService wallpaperService,
        IKuroCloudGameContext kuroCloudGameContext,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IAppContext<App> app
    )
    {
        WallpaperService = wallpaperService;
        KuroCloudGameContext = kuroCloudGameContext;
        DialogManager = dialogManager;
        App = app;
        KuroCloudGameContext.WavesCloudSurivivalService.MessageHandler +=
            WavesCloudSurivivalService_MessageHandler;
        RegisterMessager();
    }

    async partial void OnSelectLoginChanged(CloudGameLoginSession value)
    {
        if (value == null)
            return;
        var result =
            await this.KuroCloudGameContext.WavesCloudSurivivalService.WavesCloudGameService.GetWalletDataAsync(
                value,
                this.CTS.Token
            );
        WallDataWrapper wrapper = new();
        wrapper.FreeTime = TimeSpan.FromSeconds(result.Data.FreeTimeInfo.LeftSeconds);
        wrapper.PlayerCard = DateTimeOffset.FromUnixTimeSeconds(
            result.Data.TimeCardInfo.ExpireTimeSeconds
        );

        wrapper.PayTimer = TimeSpan.FromSeconds(result.Data.PayTimeInfo.LeftSeconds);
        if(result.Data.ExperienceCardInfo!= null)
            wrapper.ExperienceTime = new TimeSpan(
                result.Data.ExperienceCardInfo.Day,
                result.Data.ExperienceCardInfo.Hour,
                result.Data.ExperienceCardInfo.Minute,
                result.Data.ExperienceCardInfo.Second
            );
        wrapper.Coin = result.Data.Coin;
        this.WallData = wrapper;
    }

    private async void WavesCloudSurivivalService_MessageHandler(
        object sender,
        CloudMessageArgs session
    )
    {
        if (session.Type == Waves.Core.Models.Enums.CloudCoreType.DeleteUser)
        {
            await this.RefreshUserAsync();
        }
    }

    private void RegisterMessager()
    {
        this.Messenger.Register<CloudLoginMessager>(this, CloudLoginMethod);
    }

    private async void CloudLoginMethod(object recipient, CloudLoginMessager message)
    {
        await this.KuroCloudGameContext.WavesCloudSurivivalService.RefreshTaskAsync();
        await Task.Delay(2000);
        await this.RefreshUserAsync();
    }

    async Task RefreshUserAsync()
    {
        var users = KuroCloudGameContext.WavesCloudSurivivalService.Cache.ToList();
        await App.TryInvokeAsync(async () =>
        {
            this.Logins = new(users);
            if (Logins.Count > 0)
            {
                SelectLogin = Logins[0];
            }
        });
    }

    public override void Dispose()
    {
        KuroCloudGameContext.WavesCloudSurivivalService.MessageHandler -=
            WavesCloudSurivivalService_MessageHandler;
        base.Dispose();
    }

    [RelayCommand]
    async Task Loaded()
    {
        try
        {
            IsRefreshing = true;
            WallpaperService.SetMediaForUrl(
                Waves.Core.Models.Enums.WallpaperShowType.Image,
                "https://aki-gm-resources-back.aki-game.com/pv/cg/login.webp"
            );
            await RefreshUserAsync();
            await RefreshCloudNodesAsync();
            IsRefreshing = false;
        }
        catch (Exception ex)
        {
            IsRefreshing = false;
            this.Logger.WriteError(ex.Message + ex.StackTrace);
        }
    }

    [RelayCommand]
    async Task RefreshCardAsync()
    {
        IsRefreshing = true;
        await this.RefreshUserAsync();
        await this.RefreshCloudNodesAsync();
        IsRefreshing = false;
    }

    private async Task RefreshCloudNodesAsync()
    {
        var nodes =
            await KuroCloudGameContext.WavesCloudSurivivalService.WavesCloudGameService.CloudNetworkSpeedTestService.GetNodeListAsync(
                CloudNetworkSpeedTestService.DefaultBaseUrl,
                this.CTS.Token
            );
        if (nodes == null)
        {
            NodesCount = 0;
            return;
        }
        NodesCount = nodes.Lines.Count;
    }

    [RelayCommand]
    async Task AddUserAsync()
    {
        await DialogManager.ShowWebGameDialogAsync();
    }
}
