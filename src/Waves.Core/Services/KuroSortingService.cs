using Haiyu.KuroClient;
using ScoringService;
using ScoringService.Models;
using ScoringService.Models.Enums;
using Waves.Api.Models.Communitys.DataCenter;

namespace Waves.Core.Services;

/// <summary>库街区评分数据入口。只读当前账号，装备和实际角色面板来自同一次角色详情响应。</summary>
public sealed class KuroSortingService(IKuroClient client, IKuroAccountService accounts)
{
    public async Task<IReadOnlyList<RoleList>> GetRolesAsync(CancellationToken token = default)
    {
        var account = accounts.CurrentAccount ?? throw new InvalidOperationException("请先登录并选择库街区账号。");
        var bindings = await client.GetGamerAsync(account, (int)Models.Enums.GameType.Waves, token);
        if (bindings is not { Success: true, Data.Count: > 0 })
            throw new InvalidOperationException($"读取鸣潮绑定失败，请检查登录状态。（状态码 {bindings?.Code}）");
        var binding = bindings.Data.FirstOrDefault(x => x.IsDefault) ?? bindings.Data[0];
        var data = await client.GetGamerRoleDataAsync(account, binding, token);
        if (accounts.CurrentAccount != account) throw new InvalidOperationException("账号已切换，请重新读取。");
        return data?.RoleList?.OrderByDescending(x => x.Level).ThenBy(x => x.RoleId).ToArray()
            ?? throw new InvalidOperationException("角色列表为空，请检查库街区展示设置。");
    }

    public async Task<IReadOnlyList<SortingEcho>> GetEchoesAsync(long roleId, EntryGroup template, CancellationToken token = default) =>
        (await GetSnapshotAsync(roleId, template, token)).Echoes;

    /// <summary>重新捕获账号及绑定，防止切换账号后沿用旧数据；不输出原始响应或凭据。</summary>
    public async Task<SortingRoleSnapshot> GetSnapshotAsync(long roleId, EntryGroup template, CancellationToken token = default)
    {
        if (template.RoleId != roleId.ToString(System.Globalization.CultureInfo.InvariantCulture)) throw new ArgumentException("模板与角色不匹配。");
        var account = accounts.CurrentAccount ?? throw new InvalidOperationException("请先登录库街区。");
        var bindings = await client.GetGamerAsync(account, (int)Models.Enums.GameType.Waves, token);
        if (bindings is not { Success: true, Data.Count: > 0 }) throw new InvalidOperationException("请刷新登录状态。");
        var binding = bindings.Data.FirstOrDefault(x => x.IsDefault) ?? bindings.Data[0];
        var detail = await client.GetGamerRoilDetily(account, binding, roleId, token);
        if (detail?.Role?.RoleId != roleId) throw new InvalidOperationException("角色详情缺失或不匹配。");
        if (accounts.CurrentAccount != account) throw new InvalidOperationException("账号已切换，请重新读取。");
        var echoes = (detail.PhantomData?.EquipPhantomList ?? []).Select((phantom, i) => new SortingEcho(
            phantom.PhantomProp?.Name ?? "声骸", new EchoData
            {
                Id = $"slot-{i + 1}", Cost = phantom.Cost, Level = phantom.Level, Rarity = phantom.Quality,
                MainStats = EchoStatAdapter.Convert((phantom.MainProps ?? []).Select(x => (x.AttributeName, x.AttributeValue)), template, EntryUsage.MainStatScoring),
                SubStats = EchoStatAdapter.Convert((phantom.SubProps ?? []).Select(x => (x.AttributeName, x.AttributeValue)), template, EntryUsage.SubStatScoring)
            }, phantom.PhantomProp?.IconUrl ?? "")).ToArray();
        var panel = new CharacterPanel
        {
            Stats = EchoStatAdapter.Convert((detail.RoleAttributeList ?? []).Select(x => (x.AttributeName, x.AttributeValue)), template, EntryUsage.DevelopmentTarget)
        };
        return new(echoes, panel, detail.RoleSkin?.PicUrl ?? detail.Role.RolePicUrl ?? "", detail.Role.RoleIconUrl ?? "");
    }
}

/// <summary>声骸展示资源与计算输入，不包含账号或凭据。</summary>
public sealed record SortingEcho(string Name, EchoData Data, string IconUrl = "");
/// <summary>同一角色详情响应中的装备、实际面板与立绘。</summary>
public sealed record SortingRoleSnapshot(IReadOnlyList<SortingEcho> Echoes, CharacterPanel Panel, string PortraitUrl, string IconUrl);
