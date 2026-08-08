using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haiyu.KuroClient;
using Haiyu.Mobile.Common;
using Haiyu.Mobile.Contracts;
using Waves.Api.Models.QRLogin;
using ZXing.Net.Maui;

namespace Haiyu.Mobile.ViewModels;

public sealed partial class ScanGameQrViewModel : ViewModelBase, IQueryAttributable
{
    private readonly IKuroClient _kuroClient;
    private int _isProcessingBarcode;
    private string _playerId = string.Empty;
    private LocalAccount? _userData;
    private bool _verificationRequested;

    public ScanGameQrViewModel(
        IMobileLocalAccountService mobileLocalAccountService,
        IKuroClient kuroClient
    )
    {
        MobileLocalAccountService = mobileLocalAccountService;
        _kuroClient = kuroClient;
        Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = false,
            Multiple = false,
            TryHarder = false,
            TryInverted = false,
            InitialDelayBeforeAnalyzingFrames = 100,
            DelayBetweenAnalyzingFrames = 50,
            DelayBetweenContinuousScans = 300,
            CameraResolutionSelector = availableResolutions =>
                availableResolutions
                    .OrderBy(resolution =>
                        Math.Abs((resolution.Width * resolution.Height) - (1280 * 720))
                    )
                    .ThenBy(resolution =>
                        Math.Abs(resolution.Width - 1280) + Math.Abs(resolution.Height - 720)
                    )
                    .First(),
        };
    }

    public IMobileLocalAccountService MobileLocalAccountService { get; }

    [ObservableProperty]
    public partial BarcodeReaderOptions Options { get; set; }

    [ObservableProperty]
    public partial bool IsDetecting { get; set; }

    [ObservableProperty]
    public partial bool IsProcessing { get; set; }

    [ObservableProperty]
    public partial bool IsScannerVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsRoleSelectionVisible { get; set; }

    [ObservableProperty]
    public partial bool IsVerificationVisible { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "请将游戏登录二维码放入扫描框";

    [ObservableProperty]
    public partial string? QrText { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<Datum> Datums { get; set; } = [];

    [ObservableProperty]
    public partial Datum? SelectedDatum { get; set; }

    [ObservableProperty]
    public partial string VerifyCode { get; set; } = string.Empty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("playerId", out var id) && id is string playerId)
        {
            _playerId = playerId;
        }
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
        {
            StatusText = "需要相机权限才能扫描二维码";
            await Toast.Make("请允许应用使用相机", ToastDuration.Short).Show();
            return;
        }

        _userData = await MobileLocalAccountService.GetUserAsync(_playerId);
        if (_userData is null)
        {
            StatusText = "账号信息不存在，请返回后重试";
            return;
        }

        ShowScanner();
    }

    internal void HandleBarcodesDetected(BarcodeDetectionEventArgs args)
    {
        var value = args.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (Interlocked.CompareExchange(ref _isProcessingBarcode, 1, 0) != 0)
            return;

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            IsDetecting = false;
            await ProcessBarcodeAsync(value);
        });
    }

    private async Task ProcessBarcodeAsync(string qrText)
    {
        try
        {
            IsProcessing = true;
            QrText = qrText;
            StatusText = "正在读取登录信息…";

            if (_userData is null)
            {
                await Toast.Make("库街区账号失效，请返回首页查看", ToastDuration.Short).Show();
                return;
            }

            var result = await _kuroClient.PostQrValueAsync(
                KuroAccount.Create(_userData),
                qrText,
                CTS.Token
            );

            if (result is null || !result.Success)
            {
                StatusText = result?.Msg ?? "二维码验证失败";
                await Toast.Make(StatusText, ToastDuration.Short).Show();
                return;
            }

            if (result.Data is null || result.Data.Count == 0)
            {
                StatusText = "二维码中没有可登录的游戏角色";
                await Toast.Make(StatusText, ToastDuration.Short).Show();
                return;
            }

            Datums = new ObservableCollection<Datum>(result.Data);
            SelectedDatum = Datums[0];
            IsScannerVisible = false;
            IsRoleSelectionVisible = true;
            IsVerificationVisible = false;
            StatusText = "请选择需要登录的游戏角色";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText = $"扫描处理失败：{ex.Message}";
            await Toast.Make("扫描处理失败", ToastDuration.Short).Show();
        }
        finally
        {
            IsProcessing = false;
            Interlocked.Exchange(ref _isProcessingBarcode, 0);
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsProcessing || _userData is null || SelectedDatum is null || string.IsNullOrWhiteSpace(QrText))
            return;

        if (_verificationRequested && string.IsNullOrWhiteSpace(VerifyCode))
        {
            await Toast.Make("请输入手机收到的验证码", ToastDuration.Short).Show();
            return;
        }

        try
        {
            IsProcessing = true;
            StatusText = _verificationRequested ? "正在进行安全验证…" : "正在确认游戏登录…";

            var account = KuroAccount.Create(_userData);
            var result = await _kuroClient.QRLoginAsync(
                account,
                QrText,
                VerifyCode.Trim(),
                SelectedDatum.Id,
                CTS.Token
            );

            if (result is null)
            {
                StatusText = "登录失败，请稍后重试";
                return;
            }

            if (result.Code == 2240)
            {
                if (!_verificationRequested)
                {
                    var smsResult = await _kuroClient.GetQrCodeAsync(account, QrText, CTS.Token);
                    if (smsResult is null || !smsResult.Success)
                    {
                        StatusText = smsResult?.Msg ?? "安全验证码发送失败";
                        await Toast.Make(StatusText, ToastDuration.Short).Show();
                        return;
                    }

                    _verificationRequested = true;
                    VerifyCode = string.Empty;
                    IsRoleSelectionVisible = false;
                    IsVerificationVisible = true;
                    StatusText = string.IsNullOrWhiteSpace(SelectedDatum.Mobile)
                        ? "当前设备需要安全验证，验证码已发送至绑定手机"
                        : $"验证码已发送至 {SelectedDatum.Mobile}";
                    await Toast.Make("安全验证码已发送", ToastDuration.Short).Show();
                }
                else
                {
                    StatusText = result.Msg ?? "安全验证码错误，请重新输入";
                    await Toast.Make(StatusText, ToastDuration.Short).Show();
                }

                return;
            }

            if (result.Code != 200 || !result.Success)
            {
                StatusText = result.Msg ?? "游戏登录失败";
                await Toast.Make(StatusText, ToastDuration.Short).Show();
                return;
            }

            StatusText = "游戏登录成功";
            await Toast.Make("游戏登录成功", ToastDuration.Short).Show();
            await Task.Delay(500, CTS.Token);
            await Shell.Current.GoToAsync("..");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText = $"登录处理失败：{ex.Message}";
            await Toast.Make("登录处理失败", ToastDuration.Short).Show();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ScanAgain()
    {
        if (IsProcessing)
            return;

        ShowScanner();
    }

    private void ShowScanner()
    {
        _verificationRequested = false;
        QrText = null;
        VerifyCode = string.Empty;
        Datums.Clear();
        SelectedDatum = null;
        IsRoleSelectionVisible = false;
        IsVerificationVisible = false;
        IsScannerVisible = true;
        StatusText = "请将游戏登录二维码放入扫描框";
        IsDetecting = true;
    }
}
