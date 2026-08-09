using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.Communitys;

public sealed partial class GamerSignViewModel : ViewModelBase
{
    public GamerSignViewModel(IKuroClient wavesClient, IKuroAccountService accountService)
    {
        WavesClient = wavesClient;
        AccountService = accountService;
    }

    public IKuroClient WavesClient { get; }
    public IKuroAccountService AccountService { get; }
    public GameRoilDataItem SignRoil { get; internal set; }

    [ObservableProperty]
    public partial string UserName { get; set; }

    [ObservableProperty]
    public partial bool SignBthEnable { get; set; }

    [ObservableProperty]
    public partial bool SignBthCheck { get; set; }

    [ObservableProperty]
    public partial int SignCount { get; set; }

    [ObservableProperty]
    public partial int UnSignCount { get; set; }

    [ObservableProperty]
    public partial string SignStatus { get; set; }

    [ObservableProperty]
    public partial string SignMessage { get; set; }

    [ObservableProperty]
    public partial BitmapImage SignImage { get; set; }

    [ObservableProperty]
    public partial string SignName { get; set; }

    [RelayCommand]
    async Task Loaded()
    {
        UserName = this.SignRoil.RoleName;
        await RefreshSignHistoryAsync();
    }

    async Task RefreshSignHistoryAsync()
    {
        var account = AccountService.CurrentAccount;
        if (account is null)
            return;
        var game = await TryInvokeAsync(async () =>
            await WavesClient.GetGamerAsync(account, this.SignRoil.GameId, this.CTS.Token)
        );
        if (game.Item1 != 0)
        {
            return;
        }
        var games = game.Item2.Data;
        if (games.Count() != 0)
        {
            var result = await WavesClient.GetSignInDataAsync(
                account,
                SignRoil
            );
            var signCount = result!.Data.SigInNum;
            var signs = result.Data.SignInGoodsConfigs.Take(signCount);
            foreach (var item in signs)
            {
                item.SignResult = LanguageService.GetStringByText("已签到");
                item.IsSign = true;
            }
            SignCount = signs.Where(x => x.IsSign).Count();
            UnSignCount = result.Data.SignInGoodsConfigs.Count - SignCount;
            if (result.Data.IsSigIn)
            {
                SignBthEnable = false;
                SignBthCheck = true;
                var todaySign = result.Data.SignInGoodsConfigs.Skip(signCount + 1).Take(1);
                if (todaySign.Any())
                {
                    SignImage = new BitmapImage(new System.Uri(todaySign.First().GoodsUrl));
                    SignName = todaySign.First().GoodsName + $"×{todaySign.First().GoodsNum}";
                    SignStatus = LanguageService.GetStringByText("明日再来吧（奖励在上面写着呢）");
                }
                else
                {
                    SignMessage = LanguageService.GetStringByText("本月奖励已获得");
                    SignStatus = LanguageService.GetStringByText("今日已签到");
                }
            }
            else
            {
                SignBthEnable = true;
                SignBthCheck = false;
                var todaySign = result.Data.SignInGoodsConfigs.Skip(signCount - 1).Take(1);
                SignStatus = LanguageService.GetStringByText("领取奖励");
                SignImage = new BitmapImage(new System.Uri(todaySign.First().GoodsUrl));
                SignName = todaySign.First().GoodsName + $"×{todaySign.First().GoodsNum}";
            }
        }
    }

    [RelayCommand]
    async Task SignAsync()
    {
        var account = AccountService.CurrentAccount;
        if (account is null)
            return;
        var result = await TryInvokeAsync(async () =>
            await WavesClient.SignInAsync(
                account,
                SignRoil,
                this.CTS.Token
            )
        );
        if (result.Item1 != 0)
        {
            return;
        }
        if (result.Item2.Code == 1511)
        {
            Debug.WriteLine("已经签到！");
        }
        if (result.Item2.Code == 220)
        {
            Debug.WriteLine("Token过期，重新登陆");
        }
        if (result.Item2.Code == 1505)
        {
            Debug.WriteLine("活动过期");
        }
        if (result.Item2.Code == 200)
        {
            await RefreshSignHistoryAsync();
        }
    }
}
