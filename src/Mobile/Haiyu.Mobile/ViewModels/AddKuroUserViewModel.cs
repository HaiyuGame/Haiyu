using System.Text.RegularExpressions;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Haiyu.KuroClient;
using Haiyu.Mobile.Common;
using Haiyu.Mobile.Contracts;
using Haiyu.Mobile.Models.Messanger;
using Haiyu.Mobile.ViewModels.Popups;
using Haiyu.KuroClient.Helper;
using Microsoft.Maui.Controls.Shapes;

namespace Haiyu.Mobile.ViewModels;

public sealed partial class AddKuroUserViewModel : ViewModelBase
{
    private CancellationTokenSource? _verificationCodeCountdownCts;
    private readonly IServiceProvider _service;
    private readonly IPopupService _popupService;
    private readonly IKuroClient _kuroClient;
    private readonly IMobileLocalAccountService _mobileLocalAccountService;
    private readonly string _devCode;

    public AddKuroUserViewModel(
        IServiceProvider service,
        IPopupService popupService,
        IKuroClient kuroClient,
        IMobileLocalAccountService mobileLocalAccountService
    )
    {
        _service = service;
        _popupService = popupService;
        _kuroClient = kuroClient;
        _mobileLocalAccountService = mobileLocalAccountService;
        _devCode = AndroidHardwareIdGenerator.GenerateUniqueId();
    }

    [ObservableProperty]
    public partial string UserPhone { get; set; }

    [ObservableProperty]
    public partial string VerifyCode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignGeetButtonText))]
    public partial int RemainingSeconds { get; set; }

    public string SignGeetButtonText =>
        RemainingSeconds > 0 ? $"{RemainingSeconds} 秒后重发" : "发送验证码";

    private bool CanSignGeet() => RemainingSeconds == 0;

    partial void OnRemainingSecondsChanged(int value)
    {
        SignGeetCommand.NotifyCanExecuteChanged();
    }

   

    [RelayCommand(CanExecute = nameof(CanSignGeet))]
    public async Task SignGeetAsync()
    {
        if (string.IsNullOrWhiteSpace(this.UserPhone) || !this.UserPhone.IsMobile())
        {
            await Toast.Make("手机号码格式不正确", ToastDuration.Short, 14).Show();
            return;
        }
        await _kuroClient.InitAsync();
        var page = Shell.Current?.CurrentPage ?? Shell.Current;
        if (page is null)
            return;
        var result = await _popupService.ShowPopupAsync<LoginGeetViewModel, string>(
            page,
            new PopupOptions()
            {
                CanBeDismissedByTappingOutsideOfPopup = true,

                Shape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(16)
                },
                Shadow = null,
            },
            this.CTS.Token
        );
        if (result.WasDismissedByTappingOutsideOfPopup)
        {
            return;
        }
        if (result.WasDismissedByTappingOutsideOfPopup || string.IsNullOrWhiteSpace(result.Result))
            return;
        var sendSMS = await this._kuroClient.SendSMSAsync(
            this.UserPhone,
            result.Result,
            this._devCode,
            this.CTS.Token
        );

        if (sendSMS == null)
        {
            await Toast.Make("验证失败！", ToastDuration.Short, 14).Show();
            return;
        }
        if (sendSMS.Code == 242)
        {
            await Toast.Make("短信验证码发送频繁", ToastDuration.Short, 14).Show();
            return;
        }
        if (sendSMS.Data.GeeTest == false)
        {
            _ = StartVerificationCodeCountdownAsync(60);
            await Toast.Make("验证码发送成功", ToastDuration.Short, 14).Show();
        }
    }

    private async Task StartVerificationCodeCountdownAsync(int seconds)
    {
        _verificationCodeCountdownCts?.Cancel();
        _verificationCodeCountdownCts?.Dispose();
        _verificationCodeCountdownCts = CancellationTokenSource.CreateLinkedTokenSource(CTS.Token);

        var token = _verificationCodeCountdownCts.Token;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(seconds);

        try
        {
            while (!token.IsCancellationRequested)
            {
                RemainingSeconds = Math.Max(
                    0,
                    (int)Math.Ceiling((deadline - DateTimeOffset.UtcNow).TotalSeconds)
                );

                if (RemainingSeconds == 0)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            RemainingSeconds = 0;
        }
    }

    [RelayCommand]
    public async Task Login()
    {
        var login = await _kuroClient.LoginAsync(
            this.UserPhone,
            this.VerifyCode,
            this._devCode,
            this.CTS.Token
        );
        if (login == null || !login.Success)
        {
            await Toast
                .Make($"登录失败:" + login == null ? "" : login!.Msg, ToastDuration.Short, 14)
                .Show();
            return;
        }
        LocalAccount account = new LocalAccount();
        account.Token = login.Data.Token;
        account.TokenId = login.Data.UserId;
        account.TokenDid = this._devCode;
        await _mobileLocalAccountService.SaveUserAsync(account);
        WeakReferenceMessenger.Default.Send<HomeAccountMessanger>(new(true));
        await Shell.Current.GoToAsync("../");
    }
}
