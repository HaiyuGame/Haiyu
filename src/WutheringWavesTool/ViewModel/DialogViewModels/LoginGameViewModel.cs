using Haiyu.Services.DialogServices;
using Waves.Core.Helpers;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class LoginGameViewModel : DialogViewModelBase
{
    private string? _loginType;

    public LoginGameViewModel(
        IAppContext<App> appContext,
        IViewFactorys viewFactorys,
        IKuroClient wavesClient,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IKuroAccountService kuroAccountService
    )
        : base(dialogManager)
    {
        AppContext = appContext;
        ViewFactorys = viewFactorys;
        WavesClient = wavesClient;
        KuroAccountService = kuroAccountService;
        this.IdV2 = HardwareIdGenerator.GenerateUniqueId();
        RegisterMessanger();
    }

    private void RegisterMessanger()
    {
        this.Messenger.Register<GeeSuccessMessanger>(this, GeeSuccessMethod);
    }

    public string IdV2 { get; private set; }

    [ObservableProperty]
    public partial Visibility PhoneVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility TokenVisibility { get; set; }

    [ObservableProperty]
    public partial string Phone { get; set; }

    [ObservableProperty]
    public partial string Code { get; set; }

    [ObservableProperty]
    public partial string Token { get; set; }

    [ObservableProperty]
    public partial string TokenId { get; set; }

    [ObservableProperty]
    public partial string TokenDid { get; set; }

    [ObservableProperty]
    public partial string TipMessage { get; set; }
    public string GeetValue { get; set; }

    public IAppContext<App> AppContext { get; }
    public IViewFactorys ViewFactorys { get; }
    public IKuroClient WavesClient { get; }

    public IKuroAccountService KuroAccountService { get; }

    private async void GeeSuccessMethod(object recipient, GeeSuccessMessanger message)
    {
        if (message.Type == GeetType.Login)
        {
            this.GeetValue = message.Result;
            if (string.IsNullOrWhiteSpace(GeetValue))
                return;
            var sendSMS = await WavesClient.SendSMSAsync(Phone, GeetValue,IdV2);
            if (sendSMS == null)
            {
                TipMessage = "验证失败！";
                return;
            }
            if (sendSMS.Code == 242)
            {
                TipMessage = "短信验证码发送频繁！";
                return;
            }
            if (sendSMS.Data.GeeTest == false)
            {
                TipMessage = "验证码发送成功！";
            }
            else
            {
                TipMessage = "";
            }
        }
    }

    internal void SwitchView(string? v)
    {
        if (v == "Phone")
        {
            this.PhoneVisibility = Visibility.Visible;
            TokenVisibility = Visibility.Collapsed;
        }
        else
        {
            this.PhoneVisibility = Visibility.Collapsed;
            TokenVisibility = Visibility.Visible;
        }
        this._loginType = v;
    }

    [RelayCommand]
    void ShowGetGeet()
    {
        if (string.IsNullOrWhiteSpace(Phone))
            return;
        var view = ViewFactorys.CreateGeetWindow(GeetType.Login);
        view.AppWindowApp.Show();
    }

    [RelayCommand]
    async Task Login()
    {
        if (_loginType == "Phone")
        {
            var login = await WavesClient.LoginAsync(mobile: Phone, code: Code,IdV2);
            if (!login.Success)
            {
                TipMessage = login.Msg;
                await Task.Delay(2000);
                return;
            }
            // 多账号代码
            LocalAccount account = new LocalAccount();
            account.Token = login.Data.Token;
            account.TokenId = login.Data.UserId;
            account.TokenDid = IdV2;
            await KuroAccountService.SaveUserAsync(account);

            this.KuroAccountService.SetCurrentUser(account);
            WeakReferenceMessenger.Default.Send(
                new SelectUserMessanger(true)
            );
            DialogManager.CloseDialog();
        }
        else
        {
            if(long.TryParse(TokenId,out var _tokenID)) 
            {
                var mine = await WavesClient.GetWavesMineAsync(_tokenID, TokenDid, Token);
                if (mine != null && mine.Code == 200)
                {
                    LocalAccount account = new LocalAccount();
                    account.Token = Token;
                    account.TokenId = TokenId;
                    account.TokenDid = TokenDid;
                    await KuroAccountService.SaveUserAsync(account);
                    this.KuroAccountService.SetCurrentUser(account);
                    WeakReferenceMessenger.Default.Send(
                        new SelectUserMessanger(true)
                    );
                    DialogManager.CloseDialog();
                }
                else if (mine != null)
                {
                    TipMessage = mine.Msg;
                }
                else
                {
                    TipMessage = "错误！请反馈开发者";
                }
            }
            else
            {
                TipMessage = "TokenId格式错误！";
                return;
            }
            
        }
    }
}
