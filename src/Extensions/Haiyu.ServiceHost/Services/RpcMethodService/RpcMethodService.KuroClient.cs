using System.Text.Json;
using Haiyu.KuroClient;
using Waves.Api.Models;
using Waves.Api.Models.Communitys;
using Waves.Api.Models.KuroClient.Options;
using Waves.Api.Models.Rpc;

namespace Haiyu.ServiceHost.Services;

public partial class RpcMethodService
{
    private static readonly string[] KuroClientMethodNames =
    [
        "IsLoginAsync",
        "GetGamerDataAsync",
        "GetGamerAsync",
        "SendSMSAsync",
        "LoginAsync",
        "GetSignInDataAsync",
        "GetSignRecordAsync",
        "SignInAsync",
        "GetWavesMineAsync",
        "PostQrValueAsync",
        "QRLoginAsync",
        "GetQrCodeAsync",
        "GetDeviceInfosAsync",
        "GetBindServerAsync",
        "SendVerifyGameCode",
        "BindGamer",
        "GetGamerBassDataAsync",
        "GetGamerRoleDataAsync",
        "GetGamerCalabashDataAsync",
        "GetGamerTowerIndexDataAsync",
        "GetGamerExploreIndexDataAsync",
        "GetGamerChallengeIndexDataAsync",
        "RefreshGamerDataAsync",
        "GetGamerRoilDetily",
        "GetGamerChallengeDetails",
        "GetGamerSkinAsync",
        "GetGamerSlashDetailAsync",
        "GetBriefHeaderAsync",
        "GetVersionBrefItemAsync",
        "GetWeekBrefItemAsync",
        "GetMonthBrefItemAsync",
        "UpdateRefreshToken",
        "InitAsync",
        "GetMainWikiAsync",
        "InitMapPostion",
        "SignInClientAsync",
        "FeedHomeListsAsync",
        "PostIdLikeAsync",
        "SharedPostIdAsync",
        "GetFeedPageDetailAsync",
        "GetEncourageProcessAsync",
        "GetEncourageTotalGoldAsync",
    ];

    private static readonly JsonSerializerOptions KuroRpcJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public Task<string> GetKuroClientMethodsAsync(string _, List<RpcParams>? parameters = null)
    {
        VerifyToken(parameters);
        return Task.FromResult(JsonSerializer.Serialize(KuroClientMethodNames, KuroRpcJsonOptions));
    }

    public async Task<string> CallKuroClientAsync(string _, List<RpcParams>? parameters = null)
    {
        VerifyToken(parameters);
        var operation = RequireParameter(parameters, "operation");
        var argumentsJson = GetParameter(parameters, "arguments") ?? "{}";
        using var argumentsDocument = JsonDocument.Parse(argumentsJson);
        var arguments = argumentsDocument.RootElement;

        KuroAccount? selectedAccount = null;
        if (arguments.TryGetProperty("accountId", out var accountIdElement))
        {
            var accountId = accountIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(accountId))
            {
                var localAccount = await KuroAccountService.GetUserAsync(accountId);
                selectedAccount = localAccount is null ? null : KuroAccount.Create(localAccount);
            }
        }
        selectedAccount ??= KuroAccountService.CurrentAccount;

        KuroAccount Account() => selectedAccount
            ?? throw new ArgumentException("No Kuro account selected. Pass accountId or select an account in Haiyu.");
        GameRoilDataItem Role() => ReadArgument<GameRoilDataItem>(arguments, "role");

        object? result = operation switch
        {
            "IsLoginAsync" => await KuroClient.IsLoginAsync(Account()),
            "GetGamerDataAsync" => await KuroClient.GetGamerDataAsync(Account(), Role()),
            "GetGamerAsync" => await KuroClient.GetGamerAsync(Account(), ReadArgument<int>(arguments, "gameId")),
            "SendSMSAsync" => await KuroClient.SendSMSAsync(
                ReadArgument<string>(arguments, "mobile"),
                ReadArgument<string>(arguments, "geeTestData"),
                ReadArgument<string>(arguments, "tokenDid")
            ),
            "LoginAsync" => await KuroClient.LoginAsync(
                ReadArgument<string>(arguments, "mobile"),
                ReadArgument<string>(arguments, "code"),
                ReadArgument<string>(arguments, "tokenDid")
            ),
            "GetSignInDataAsync" => await KuroClient.GetSignInDataAsync(Account(), Role()),
            "GetSignRecordAsync" => await KuroClient.GetSignRecordAsync(Account(), Role()),
            "SignInAsync" => await KuroClient.SignInAsync(Account(), Role()),
            "GetWavesMineAsync" => await KuroClient.GetWavesMineAsync(Account(), ReadArgument<long>(arguments, "id")),
            "PostQrValueAsync" => await KuroClient.PostQrValueAsync(Account(), ReadArgument<string>(arguments, "qrText")),
            "QRLoginAsync" => await KuroClient.QRLoginAsync(
                Account(),
                ReadArgument<string>(arguments, "qrText"),
                ReadArgument<string>(arguments, "verifyCode"),
                ReadArgument<string>(arguments, "id")
            ),
            "GetQrCodeAsync" => await KuroClient.GetQrCodeAsync(Account(), ReadArgument<string>(arguments, "qrCode")),
            "GetDeviceInfosAsync" => await KuroClient.GetDeviceInfosAsync(Account()),
            "GetBindServerAsync" => await KuroClient.GetBindServerAsync(Account(), ReadArgument<int>(arguments, "gameId")),
            "SendVerifyGameCode" => await KuroClient.SendVerifyGameCode(
                Account(),
                ReadArgument<string>(arguments, "gameId"),
                ReadArgument<string>(arguments, "serverId"),
                ReadArgument<string>(arguments, "roleId")
            ),
            "BindGamer" => await KuroClient.BindGamer(
                Account(),
                ReadArgument<string>(arguments, "gameId"),
                ReadArgument<string>(arguments, "serverId"),
                ReadArgument<string>(arguments, "roleId"),
                ReadArgument<string>(arguments, "verifyCode")
            ),
            "GetGamerBassDataAsync" => await KuroClient.GetGamerBassDataAsync(Account(), Role()),
            "GetGamerRoleDataAsync" => await KuroClient.GetGamerRoleDataAsync(Account(), Role()),
            "GetGamerCalabashDataAsync" => await KuroClient.GetGamerCalabashDataAsync(Account(), Role()),
            "GetGamerTowerIndexDataAsync" => await KuroClient.GetGamerTowerIndexDataAsync(Account(), Role()),
            "GetGamerExploreIndexDataAsync" => await KuroClient.GetGamerExploreIndexDataAsync(Account(), Role()),
            "GetGamerChallengeIndexDataAsync" => await KuroClient.GetGamerChallengeIndexDataAsync(Account(), Role()),
            "RefreshGamerDataAsync" => await KuroClient.RefreshGamerDataAsync(Account(), Role()),
            "GetGamerRoilDetily" => await KuroClient.GetGamerRoilDetily(Account(), Role(), ReadArgument<long>(arguments, "roleId")),
            "GetGamerChallengeDetails" => await KuroClient.GetGamerChallengeDetails(Account(), Role(), ReadArgument<int>(arguments, "countryCode")),
            "GetGamerSkinAsync" => await KuroClient.GetGamerSkinAsync(Account(), Role()),
            "GetGamerSlashDetailAsync" => await KuroClient.GetGamerSlashDetailAsync(Account(), Role()),
            "GetBriefHeaderAsync" => await KuroClient.GetBriefHeaderAsync(Account()),
            "GetVersionBrefItemAsync" => await KuroClient.GetVersionBrefItemAsync(
                Account(),
                ReadArgument<string>(arguments, "roleId"),
                ReadArgument<string>(arguments, "serverId"),
                ReadArgument<string>(arguments, "versionId")
            ),
            "GetWeekBrefItemAsync" => await KuroClient.GetWeekBrefItemAsync(
                Account(),
                ReadArgument<string>(arguments, "roleId"),
                ReadArgument<string>(arguments, "serverId"),
                ReadArgument<string>(arguments, "versionId")
            ),
            "GetMonthBrefItemAsync" => await KuroClient.GetMonthBrefItemAsync(
                Account(),
                ReadArgument<string>(arguments, "roleId"),
                ReadArgument<string>(arguments, "serverId"),
                ReadArgument<string>(arguments, "versionId")
            ),
            "UpdateRefreshToken" => await KuroClient.UpdateRefreshToken(Account(), Role()),
            "GetMainWikiAsync" => await KuroClient.GetMainWikiAsync(Account()),
            "SignInClientAsync" => await KuroClient.SignInClientAsync(Account()),
            "FeedHomeListsAsync" => await KuroClient.FeedHomeListsAsync(Account(), ReadArgument<HomeFeedOption>(arguments, "option")),
            "PostIdLikeAsync" => await KuroClient.PostIdLikeAsync(Account(), ReadArgument<HomeFeedLikeOption>(arguments, "option")),
            "SharedPostIdAsync" => await KuroClient.SharedPostIdAsync(Account(), ReadArgument<HomeFeedSharedOption>(arguments, "option")),
            "GetFeedPageDetailAsync" => await KuroClient.GetFeedPageDetailAsync(Account(), ReadArgument<HomeFeedPostDetailOption>(arguments, "option")),
            "GetEncourageProcessAsync" => await KuroClient.GetEncourageProcessAsync(Account(), ReadArgument<EncourageProcessOption>(arguments, "option")),
            "GetEncourageTotalGoldAsync" => await KuroClient.GetEncourageTotalGoldAsync(Account()),
            "InitAsync" => await InvokeWithoutResultAsync(KuroClient.InitAsync),
            "InitMapPostion" => await InvokeWithoutResultAsync(() => KuroClient.InitMapPostion(Account())),
            _ => throw new ArgumentException($"Unsupported KuroClient operation: {operation}"),
        };

        return JsonSerializer.Serialize(result, KuroRpcJsonOptions);
    }

    public async Task<string> GetLocalAccountsAsync(string _, List<RpcParams>? parameters = null)
    {
        VerifyToken(parameters);
        var users = await KuroAccountService.GetUsersAsync() ?? [];
        var result = users.Select(x => new
        {
            accountId = x.TokenId,
            deviceId = x.TokenDid,
            displayName = x.DisplayName,
            phone = x.Phone,
            isSelected = x.IsSelect,
        });
        return JsonSerializer.Serialize(result, KuroRpcJsonOptions);
    }

    public Task<string> GetCurrentAccountAsync(string _, List<RpcParams>? parameters = null)
    {
        VerifyToken(parameters);
        var account = KuroAccountService.CurrentAccount;
        object? result = account is null
            ? null
            : new { accountId = account.UserId, deviceId = account.DeviceId };
        return Task.FromResult(JsonSerializer.Serialize(result, KuroRpcJsonOptions));
    }

    private static async Task<object?> InvokeWithoutResultAsync(Func<Task> action)
    {
        await action();
        return new { success = true };
    }

    private static string? GetParameter(List<RpcParams>? parameters, string key) =>
        parameters?.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string RequireParameter(List<RpcParams>? parameters, string key) =>
        GetParameter(parameters, key) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Missing RPC parameter: {key}");

    private static T ReadArgument<T>(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value))
            throw new ArgumentException($"Missing KuroClient argument: {name}");

        return value.Deserialize<T>(KuroRpcJsonOptions)
            ?? throw new ArgumentException($"Invalid KuroClient argument: {name}");
    }
}
