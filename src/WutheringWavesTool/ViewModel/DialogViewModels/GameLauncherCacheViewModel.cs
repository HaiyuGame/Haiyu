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

    public IGameContext GameContext { get; private set; }

    [ObservableProperty]
    public partial ObservableCollection<KRSDKLauncherCacheWrapper> Items { get; private set; }

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
        this._args = args;
        this.GameContext = Instance.Host.Services.GetKeyedService<IGameContext>(
            args.GameContextName
        );
        var localSelect = await GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LasterSelectLocalUser
        );
        this.Items = (await GameContext.GetLocalGameOAuthAsync(this.CTS.Token))
            .Select(x => new KRSDKLauncherCacheWrapper(x,localSelect))
            .ToObservableCollection();
    }

    [RelayCommand]
    public async Task SetSelect()
    {
        foreach (var item in Items)
        {
            await GameContext.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LasterSelectLocalUser, item.Cache.Username);
        }
    }

    
}
