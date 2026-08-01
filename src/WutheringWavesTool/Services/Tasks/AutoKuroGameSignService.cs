using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Core.Contracts.Tasks;
using Waves.Core.Services;
using Waves.Core.Services.Tasks;
using Waves.Settings;
namespace Haiyu.Services.Tasks;

public sealed class AutoKuroGameSignService : TimedTaskServiceBase,ITaskName
{

    public string DisplayName => LanguageService.GetStringByText("库街区自动签到服务");

    public string Description => LanguageService.GetStringByText("根据已登录的所有账号进行自动签到，包括战双、鸣潮");

    public string Guid => "17A23862-4CEE-48F0-BF95-8B1CEF119158";

    public string Note => "AutoSignCommunity";

    private readonly IKuroAccountService _kuroAccountService;
    private readonly IKuroClient _kuroClient;
    public AutoKuroGameSignService(
        SystemEventPublisher publisher,
        [FromKeyedServices("AppLog")] LoggerService logger,
        IKuroAccountService kuroAccountService,
        IKuroClient kuroClient
    )
        : base(publisher, logger)
    {
        TargetTime = new TimeOnly(8, 0);
        this._kuroAccountService = kuroAccountService;
        this._kuroClient = kuroClient;
    }


    public override async Task InvokeAsync(CancellationToken token = default) 
    {
        int successCount = 0;
        int errorCount = 0;
        var accounts = await _kuroAccountService.GetUsersAsync();
        foreach (var account in accounts)
        {
            var requestAccount = KuroAccount.From(account);
            var wavesGamers = await _kuroClient.GetGamerAsync(
                requestAccount,
                Waves.Core.Models.Enums.GameType.Waves,
                token
            );
            var punish = await _kuroClient.GetGamerAsync(
                requestAccount,
                Waves.Core.Models.Enums.GameType.Punish,
                token
            );
            if (wavesGamers == null || wavesGamers.Code != 200 || punish==null || punish.Code != 200)
                return;
            var items = wavesGamers.Data.Concat(punish.Data);
            foreach (var item in items)
            {
                var sign = await _kuroClient.SignInAsync(requestAccount, item, token);
                if(sign == null)
                {
                    return;
                }
                if (sign.Code == 1511 || sign.Code == 0)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                }
            }
        }
        Publisher.Publish(
            new()
            {
                Message = LanguageService.FormatByText(LanguageService.GetStringByText("签到结果{0}个成功，总数{1}"), successCount, successCount + errorCount),
                Delay = 5,
            }
        );
    }
}
