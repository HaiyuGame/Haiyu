namespace Haiyu.Common.KuroWebView.Initializer;


/// <summary>
/// 数据面板。
/// URL: https://web-static.kurobbs.com/mcbox/index.html#/mc-role-box
/// Query: accessType=1, roleId={roleId}, serverId={serverId}
/// 初始化规则: mcResMonReport_* + mc_userInfo + mc_role*。
/// </summary>
public sealed class KuroDataPanelWebViewInitializer : KuroWebViewPageInitializerBase
{
    public override bool CanInitialize(WebSessionContext session)
    {
        return PageUrlContains(session, "/mcbox/index.html") || PageUrlContains(session, "mc-role-box");
    }

    protected override Dictionary<string, object?> CreateStorageItems(WebSessionContext session)
    {
        var userInfo = CreateUserInfo(session);
        var roleInfo = CreateRoleInfo(session);
        var compactRoleInfo = CreateCompactRoleInfo(session);

        return new(StringComparer.Ordinal)
        {
            ["token"] = session.Token,
            ["did"] = session.Did,
            ["userId"] = session.UserId,
            ["initUserInfo"] = userInfo,
            ["mcResMonReport_APP_USER_INFO"] = userInfo,
            ["mcResMonReport_ROLE_INFO"] = roleInfo,
            ["mcResMonReport_GUIDE_ETSRC"] = session.EnterSource,
            ["mc_userInfo"] = compactRoleInfo,
            ["mc_serverId"] = session.ServerId,
            ["mc_roleId"] = session.RoleId,
            ["mc_roleName"] = session.RoleName,
            ["REQUEST_IP"] = session.RequestIp
        };
    }
}
