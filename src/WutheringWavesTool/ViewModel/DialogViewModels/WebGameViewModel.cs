using Haiyu.Common.Contracts;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class WebGameViewModel : DialogViewModelBase
{
    private string? _loginType;

    public WebGameViewModel(
        IAppContext<App> appContext,
        IViewFactorys viewFactorys,
        IWavesCloudGameService cloudGameService,
        DialogSession dialogSession
    )
        : base(dialogSession)
    {
        AppContext = appContext;
        ViewFactorys = viewFactorys;
        CloudGameService = cloudGameService;
        RegisterMessanger();
    }

    private void RegisterMessanger()
    {
        this.Messenger.Register<GeeSuccessMessanger>(this, GeeSuccessMethod);
    }

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
    public partial string TipMessage { get; set; }


    DispatcherTimer _timer { get; set; }

    [ObservableProperty]
    public partial int TimeCount { get; set; } = 60;

    [ObservableProperty]
    public partial bool SendCodeEnable { get; set; } = true;


    [ObservableProperty]
    public partial string SendCodeText { get; set; } = LanguageService.GetString("Display_Item4");

    private CloudGameLoginSnapshot _snapshot;

    public IAppContext<App> AppContext { get; }
    public IViewFactorys ViewFactorys { get; }
    public IWavesCloudGameService CloudGameService { get; }

    private async void GeeSuccessMethod(object recipient, GeeSuccessMessanger message)
    {
        if (message.Type == GeetType.WebGame) { }
    }

    [RelayCommand]
    async Task ShowGetGeet()
    {
        if (string.IsNullOrWhiteSpace(Phone) || this.TimeCount != 60)
            return;
        var sendSMS = await CloudGameService.GetPhoneSMSAsync(Phone, this.CTS.Token);
        if (sendSMS.Item1 == null)
        {
            TipMessage = LanguageService.GetStringByText("发生验证码失败！");
            return;
        }
        TipMessage = sendSMS.Item1.ErrorDescription;
        this._snapshot = sendSMS.Item2;
        if (sendSMS.Item1.Codes == 0)
        {
            SetDispatcherTimer();
        }
    }

    private void SetDispatcherTimer()
    {
        if (_timer == null)
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += _timer_Tick;
        }
        _timer.Start();
    }

    private void _timer_Tick(object? sender, object e)
    {
        SendCodeEnable = false;
        TimeCount--;
        this.SendCodeText = $"{TimeCount}S";
        if (TimeCount == 0)
        {
            SendCodeEnable = true;
            TimeCount = 60;
            this.SendCodeText = LanguageService.GetString("Display_Item4");
            _timer.Stop();
        }
    }

    [RelayCommand]
    async Task Login()
    {
        var result = await CloudGameService.LoginAsync(
            this._snapshot,
            this.Phone,
            this.Code,
            this.CTS.Token
        );
        if (result.Code != 0)
        {
            TipMessage = result.Msg;
            return;
        }
        var saveResult = await CloudGameService.ConfigManager.SaveUserAsync(result.Data);
        WeakReferenceMessenger.Default.Send(new CloudLoginMessager(true, result.Data.Username));
        await this.Close();
    }
}
