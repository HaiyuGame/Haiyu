using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common.KuroWebView.Initializer
{
    /// <summary>
    /// 资源简报。
    /// URL: https://web-static.kurobbs.com/resource-briefing/index.html#/home
    /// Query/hash: #/home，项目已有入口但尚未通过 adb 采样；先复用通用用户和角色上下文。
    /// 初始化规则: initUserInfo + token/did/userId + roleInfo 兜底。
    /// </summary>
    public sealed class KuroResourceBriefingWebViewInitializer : KuroWebViewPageInitializerBase
    {
        public override bool CanInitialize(WebSessionContext session)
        {
            return PageUrlContains(session, "/resource-briefing/");
        }

        protected override Dictionary<string, object?> CreateStorageItems(WebSessionContext session)
        {
            return new(StringComparer.Ordinal)
            {
                ["token"] = session.Token,
                ["did"] = session.Did,
                ["userId"] = session.UserId,
                ["initUserInfo"] = CreateUserInfo(session),
                ["roleInfo"] = CreateRoleInfo(session),
                ["REQUEST_IP"] = session.RequestIp
            };
        }
    }
}
