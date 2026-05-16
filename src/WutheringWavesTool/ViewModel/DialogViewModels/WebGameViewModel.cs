using Haiyu.Services.DialogServices;
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
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager
    )
        : base(dialogManager)
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

    private CloudGameLoginSnapshot _snapshot;

    public string GeetValue { get; set; }

    public IAppContext<App> AppContext { get; }
    public IViewFactorys ViewFactorys { get; }
    public IWavesCloudGameService CloudGameService { get; }

    private async void GeeSuccessMethod(object recipient, GeeSuccessMessanger message)
    {
        if (message.Type == GeetType.WebGame)
        {
            this.GeetValue = message.Result;
            if (string.IsNullOrWhiteSpace(GeetValue))
                return;
            var geetData = JsonSerializer.Deserialize(message.Result, GeetContext.Default.GeetData);
            var sendSMS = await CloudGameService.GetPhoneSMSAsync(
                Phone,
                geetData.CaptchaOutput,
                geetData.PassToken,
                geetData.GenTime,
                geetData.LotNumber,
                this.CTS.Token
            );
            if (sendSMS.Item1 == null)
            {
                TipMessage = "发生验证码失败！";
                return;
            }
            TipMessage = sendSMS.Item1.ErrorDescription;
            this._snapshot = sendSMS.Item2;
        }
    }

    [RelayCommand]
    void ShowGetGeet()
    {
        if (string.IsNullOrWhiteSpace(Phone))
            return;
        var view = ViewFactorys.CreateGeetWindow(GeetType.WebGame);
        view.AppWindowApp.Show();
    }

    [RelayCommand]
    async Task Login()
    {
        var result = await CloudGameService.LoginAsync(this._snapshot,this.Phone, this.Code, this.CTS.Token);
        if (result.Code != 0)
        {
            TipMessage = result.Msg;
            return;
        }
        var saveResult = await CloudGameService.ConfigManager.SaveUserAsync(result.Data);
        WeakReferenceMessenger.Default.Send(new CloudLoginMessager(true, result.Data.Username));
        this.Close();
    }
}
