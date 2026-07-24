using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common.KuroWebView.Initializer
{

    /// <summary>
    /// 鸣潮活动日历。
    /// URL: https://web-static.kurobbs.com/mccalendar/index.html#/
    /// Query/hash: #/ 为首页，页面内部请求活动状态、卡池、推荐、进行中活动。
    /// 初始化规则: aki-calendar:user-info + aki-calendar:role-info + 日历展开状态。
    /// </summary>
    public sealed class KuroCalendarWebViewInitializer : KuroWebViewPageInitializerBase
    {
        public override bool CanInitialize(WebSessionContext session)
        {
            return PageUrlContains(session, "/mccalendar/");
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
                ["aki-calendar:user-info"] = userInfo,
                ["aki-calendar:role-info"] = roleInfo,
                ["aki-calendar:longtime-card-expand"] = "true",
                ["aki-calendar:recommend-card-expand"] = "true",
                ["aki-calendar:banner-card-expand"] = "true",
                ["REQUEST_IP"] = session.RequestIp
            };
        }
    }
}
