using System.Text.Json;
using Waves.Api.Models.Enums;
using Waves.Api.Models.Rpc;
using Waves.Core.Models.CloudGame;

namespace Haiyu.ServiceHost.Services;

public partial class RpcMethodService
{
    public async Task<string> GetCloudAccountsAsync(string _, List<RpcParams>? parameters = null)
    {
        VerifyToken(parameters);
        var selectedId = await AppSettings.GetSelectCloudUserIDAsync();
        var users = await CloudConfigManager.GetUsersAsync();
        var result = users.Select(x => new
        {
            accountId = x.GetId(),
            displayName = MaskPhone(x.Phone),
            isSelected = string.Equals(x.GetId(), selectedId, StringComparison.Ordinal),
        });
        return JsonSerializer.Serialize(result, KuroRpcJsonOptions);
    }

    public async Task<string> SelectCloudAccountAsync(string _, List<RpcParams>? parameters = null)
    {
        VerifyToken(parameters);
        var accountId = RequireParameter(parameters, "accountId");
        var session = await GetCloudSessionAsync(accountId);
        return JsonSerializer.Serialize(new
        {
            success = true,
            accountId = session.GetId(),
        }, KuroRpcJsonOptions);
    }

    public async Task<string> GetCloudGachaRecordInfoAsync(string _, List<RpcParams>? parameters = null)
    {
        VerifyToken(parameters);
        var session = await GetCloudSessionAsync(GetParameter(parameters, "accountId"));
        var result = await WavesCloudGameService.GetRecordAsync(session);
        return JsonSerializer.Serialize(result, KuroRpcJsonOptions);
    }

    public async Task<string> GetCloudGachaRecordsAsync(string _, List<RpcParams>? parameters = null)
    {
        VerifyToken(parameters);
        var session = await GetCloudSessionAsync(GetParameter(parameters, "accountId"));
        var record = await WavesCloudGameService.GetRecordAsync(session);
        if (record?.Data is null)
            throw new InvalidOperationException("Cloud gacha record identity was not returned.");

        var poolTypeText = GetParameter(parameters, "poolType");
        var poolTypes = string.IsNullOrWhiteSpace(poolTypeText)
            ? CardPoolTypeValues.All.Select(x => (int)x)
            : [int.TryParse(poolTypeText, out var value)
                ? value
                : throw new ArgumentException("poolType must be an integer.")];

        var pools = new List<object>();
        foreach (var poolType in poolTypes)
        {
            var response = await WavesCloudGameService.GetGameRecordResource(
                session,
                record.Data.RecordId,
                record.Data.PlayerId.ToString(),
                poolType
            );
            pools.Add(new { poolType, response });
        }

        return JsonSerializer.Serialize(new
        {
            accountId = session.GetId(),
            record = record.Data,
            pools,
        }, KuroRpcJsonOptions);
    }

    private async Task<CloudGameLoginSession> GetCloudSessionAsync(string? accountId)
    {
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var user = await CloudConfigManager.GetUserAsync(accountId);
            if (user is null)
                throw new ArgumentException($"Cloud account not found: {accountId}");

            var current = await WavesCloudGameService.GetCurrentUserSession();
            if (current?.GetId() != accountId)
                await WavesCloudGameService.SetCurrentUserSession(accountId);
        }

        return await WavesCloudGameService.GetCurrentUserSession()
            ?? throw new InvalidOperationException("No cloud account is selected or its session could not be established.");
    }

    private static string MaskPhone(string? phone) => phone is { Length: >= 7 }
        ? $"{phone[..3]}****{phone[^4..]}"
        : string.Empty;
}
