using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common.KuroWebView.Initializer
{

    /// <summary>
    /// 养成计算器。
    /// URL: https://web-static.kurobbs.com/growth-calculator/index.html#/
    /// Query/hash: #/, #/resonators/result?from=home 等前端路由。
    /// 初始化规则: mc-growth-simulator-user-info + mc-growth-simulator-role-info。
    /// </summary>
    public sealed class KuroGrowthCalculatorWebViewInitializer : KuroWebViewPageInitializerBase
    {
        public override bool CanInitialize(WebSessionContext session)
        {
            return PageUrlContains(session, "/growth-calculator/");
        }

        protected override Dictionary<string, object?> CreateStorageItems(WebSessionContext session)
        {
            var userInfo = CreateUserInfo(session);
            var roleInfo = CreateRoleInfo(session);

            return new(StringComparer.Ordinal)
            {
                ["token"] = session.Token,
                ["did"] = session.Did,
                ["userId"] = session.UserId,
                ["initUserInfo"] = userInfo,
                ["mc-growth-simulator-user-info"] = userInfo,
                ["mc-growth-simulator-role-info"] = roleInfo,
                ["REQUEST_IP"] = session.RequestIp,
                ["growth-simulator-home-report"] = "true"
            };
        }
    }

}
