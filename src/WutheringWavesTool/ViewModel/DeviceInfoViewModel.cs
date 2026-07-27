using Waves.Api.Models.QRLogin;

using Haiyu.Helpers;

namespace Haiyu.ViewModel;

public class DeviceInfoDisplayHeader
{
    public string? DisplayName { get; set; }
    public string? Tag { get; set; }

    public DeviceInfoDisplayHeader(string? displayName, string? tag)
    {
        DisplayName = displayName;
        Tag = tag;
    }
}

public class GamerId
{
    public string DisplayName { get; set; }

    public int Id { get; set; }

    public static ObservableCollection<GamerId> Gamers() =>
        new ObservableCollection<GamerId>()
        {
            new() { DisplayName = LanguageService.GetString("WutheringName")!, Id = 3 },
            new() { DisplayName = LanguageService.GetString("PunishName")!, Id = 2 },
        };
}

public partial class DeviceInfoViewModel : WindowViewModelBase, IDisposable
{
    private bool disposedValue;

    public DeviceInfoViewModel(IKuroClient wavesClient, IKuroAccountService accountService)
    {
        WavesClient = wavesClient;
        AccountService = accountService;
    }

    [ObservableProperty]
    public partial ObservableCollection<DeviceInfoDisplayHeader> Displays { get; set; } =
        new()
        {
            new DeviceInfoDisplayHeader(LanguageService.GetString("ViewModel_PCAuthorization"), "PC"),
            new DeviceInfoDisplayHeader(LanguageService.GetString("ViewModel_AccountAuthorization"), "User"),
        };

    [ObservableProperty]
    public partial ObservableCollection<GamerId> Gamers { get; set; } = GamerId.Gamers();

    [ObservableProperty]
    public partial GamerId SelectGamer { get; set; }

    [ObservableProperty]
    public partial DeviceInfoDisplayHeader SelectHeader { get; set; }

    [ObservableProperty]
    public partial Visibility DeviceInfosVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility GamerRoleVisibility { get; set; }
    public IKuroClient WavesClient { get; }
    public IKuroAccountService AccountService { get; }

    [ObservableProperty]
    public partial string VerifyCode { get; set; }

    [ObservableProperty]
    public partial string BindRoleId { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DeviceDatum> Devices { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<AddUserDatum> UserServers { get; set; }

    [ObservableProperty]
    public partial bool? BindCheck { get; set; }

    [ObservableProperty]
    public partial AddUserDatum SelectUserServer { get; set; }

    [ObservableProperty]
    public partial string TipMessage { get; private set; }

    [RelayCommand]
    async Task Loaded()
    {
        SelectHeader = Displays[0];
        await RefreshAsync();
    }

    [RelayCommand]
    async Task RefreshAsync()
    {
        var account = AccountService.CurrentAccount;
        if (account is null)
            return;
        var devices = await WavesClient.GetDeviceInfosAsync(account);
        if (devices != null)
            this.Devices = devices.Data.Where(x => x != null).ToObservableCollection();
    }

    partial void OnSelectHeaderChanged(DeviceInfoDisplayHeader value)
    {
        if (value == null)
            return;
        if (value.Tag == "PC")
        {
            DeviceInfosVisibility = Visibility.Visible;
            GamerRoleVisibility = Visibility.Collapsed;
        }
        else
        {
            DeviceInfosVisibility = Visibility.Collapsed;
            GamerRoleVisibility = Visibility.Visible;
        }
    }

    async partial void OnSelectGamerChanged(GamerId value)
    {
        if (value == null)
            return;
        var account = AccountService.CurrentAccount;
        if (account is null)
            return;
        var gameServer = await WavesClient.GetBindServerAsync(account, value.Id, this.CTS.Token);
        if (gameServer != null && gameServer.Code == 200)
            this.UserServers = gameServer.Data.ToObservableCollection();
    }

    [RelayCommand]
    async Task SendVerifyCode()
    {
        if (SelectUserServer == null || SelectGamer == null)
            return;
        var result = await WavesClient.SendVerifyGameCode(
            AccountService.CurrentAccount ?? throw new InvalidOperationException(LanguageService.GetStringByText("当前未选择账号。")),
            SelectGamer.Id.ToString(),
            SelectUserServer.ServerId,
            this.BindRoleId,
            this.CTS.Token
        );
        if (result == null)
        {
            TipMessage = LanguageService.GetString("ViewModel_VerificationRejected")!;
            return;
        }
        if (result.Code == 200)
        {
            TipMessage = LanguageService.GetString("ViewModel_VerificationCodeSent")!;
        }
        if (result.Code != 200)
        {
            TipMessage = result.Msg;
        }
    }

    [RelayCommand]
    async Task BindGameCode()
    {
        if (
            SelectUserServer == null
            || SelectGamer == null
            || string.IsNullOrWhiteSpace(VerifyCode)
        )
            return;
        var result = await WavesClient.BindGamer(
            AccountService.CurrentAccount ?? throw new InvalidOperationException(LanguageService.GetStringByText("当前未选择账号。")),
            SelectGamer.Id.ToString(),
            SelectUserServer.ServerId,
            this.BindRoleId,
            this.VerifyCode,
            this.CTS.Token
        );
        if (result == null)
        {
            TipMessage = LanguageService.GetStringByText("验证失败!，库洛拒绝回答");
            return;
        }
        if (result.Code == 200)
        {
            if (result.Data != null && !string.IsNullOrWhiteSpace(result.Data.Token))
            {
                TipMessage = LanguageService.GetString("ViewModel_AccountAlreadyBound")!;
                this.VerifyCode = "";
                return;
            }
            TipMessage = LanguageService.GetString("ViewModel_BindingSuccessful")!;
            this.VerifyCode = "";
        }
        if (result.Code != 200)
        {
            TipMessage = result.Msg;
        }
    }

    public override void Dispose()
    {
        this.Displays.RemoveAll();
        this.Gamers.RemoveAll();
        this.CTS.Cancel();
        this.CTS.Dispose();
        GC.SuppressFinalize(this);
    }
}
