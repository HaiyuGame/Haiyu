using Haiyu.Models.Dialogs;
using Haiyu.Services.DialogServices;
using Waves.Api.Models.Launcher;
using Waves.Core.Common;
using Windows.ApplicationModel.DataTransfer;
using Windows.Security.Credentials.UI;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class GameLauncherCacheViewModel : DialogViewModelBase
{
    private GameLauncherCacheArgs _args;

    public IGameContextV2 GameContext { get; private set; }

    [ObservableProperty]
    public partial ObservableCollection<KRSDKLauncherCacheWrapper> Items { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetSelectCommand))]

    public partial bool IsLoading { get; set; }

    
    public bool IsOk() => !IsLoading;

    public GameLauncherCacheViewModel(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager
    )
        : base(dialogManager)
    {
        RegisterMessager();
    }

    private void RegisterMessager()
    {
        this.Messenger.Register<GameLauncheCacheMessager>(this, GameLauncheCacheMethod);
    }

    private async void GameLauncheCacheMethod(object recipient, GameLauncheCacheMessager message)
    {
        if (message.isVerify)
        {
            await VerifySystem(message.cache.OauthCode);
        }
    }

    public override void BeforeClose()
    {
        this.Messenger.UnregisterAll(this);
        Items.Clear();
    }

    async Task VerifySystem(string oauthCode)
    {
        var result = await UserConsentVerifier.RequestVerificationAsync(
            "复制游戏登陆码需要系统用户进行验证"
        );
        if (result == UserConsentVerificationResult.Verified)
        {
            var oAuth = KrKeyHelper.Xor(oauthCode, 5);
            var package = new DataPackage();
            package.SetText(oAuth);
            Clipboard.SetContent(package);
        }
    }

    public async void SetData(GameLauncherCacheArgs args)
    {
        IsLoading = true;
        this._args = args;
        Items = [];
        this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(
            args.GameContextName
        );
        var localSelect = await GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LasterSelectLocalUser
        );
        var result = await GameContext.GetLocalGameOAuthAsync(this.CTS.Token);
        if (result == null)
            return;
        foreach (var item in result)
        {
            var code = KrKeyHelper.Xor(item.OauthCode, 5);
            var userPlayers = await GameContext.QueryPlayerInfoAsync(code);

            if(userPlayers.Code != 0)
            {
                continue;
            }
            foreach (var player in userPlayers.Items)
            {
                KRSDKLauncherCacheWrapper? info = null;
                if (this.GameContext.GameType == Waves.Core.Models.Enums.GameType.Waves)
                {
                    info = new KRSDKLauncherCacheWrapper(item, (WavesQueryPlayerItem)player);
                    if (info.GetKey == localSelect)
                    {
                        info.IsSelect = true;
                    }
                }

                if(this.GameContext.GameType == Waves.Core.Models.Enums.GameType.Punish)
                {
                    info = new KRSDKLauncherCacheWrapper(item, (PunishQueryPlayerItem)player);
                    if (info.GetKey == localSelect)
                    {
                        info.IsSelect = true;
                    }
                }
                if (info == null)
                    continue;
                Items.Add(info);
            }
        }
        IsLoading = false;
    }

    [RelayCommand(CanExecute = nameof(IsOk))]
    public async Task SetSelect()
    {
        var item = Items.Where(x => x.IsSelect).FirstOrDefault();
        if(item == null)
        {
            return;
        }
        await GameContext.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LasterSelectLocalUser,item.GetKey);
        WeakReferenceMessenger.Default.Send<LocalGameRefreshBindUser>(new LocalGameRefreshBindUser(item));
        this.Close();
    }

    public override void AfterClose()
    {
        this.Items.Clear();
        this.Items = null;
        base.AfterClose();
    }

    
}
