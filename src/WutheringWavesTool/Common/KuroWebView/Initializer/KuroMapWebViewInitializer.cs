using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common.KuroWebView.Initializer
{

    /// <summary>
    /// 鸣潮大地图。
    /// URL: https://www.kurobbs.com/mc/map/
    /// Query: v=4.0, state={state_id}, country={country}, x={x}, y={y}, zoom={zoom}
    /// 初始化规则: AKI_MAP_* + map_dc + mapStateCache；地图 API 依赖 token、devcode、wiki_type=10、state_id。
    /// </summary>
    public sealed class KuroMapWebViewInitializer : KuroWebViewPageInitializerBase
    {
        private const string DefaultMapDeviceCode = "s5U7bfRZlc5fKsh07E44fmvJ3lA3X3KZ";

        public override bool CanInitialize(WebSessionContext session)
        {
            return PageUrlContains(session, "kurobbs.com/mc/map") || PageUrlContains(session, "/resource/map/");
        }

        protected override Dictionary<string, object?> CreateStorageItems(WebSessionContext session)
        {
            var userInfo = CreateUserInfo(session);
            var mapState = CreateMapState(session.PageUrl);

            return new(StringComparer.Ordinal)
            {
                ["AKI_MAP_USER_TOKEN"] = session.Token,
                ["AKI_MAP_APP_VERSION"] = session.AppVersion,
                ["AKI_MAP_USER_INFO"] = userInfo,
                ["AKI_MAP_USER_PROFILE"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["profile"] = userInfo,
                    ["token"] = session.Token
                },
                ["map_dc"] = DefaultMapDeviceCode,
                ["mapStateCache"] = mapState,
                ["MAP_GUIDE_KEY"] = new Dictionary<string, object?> { ["timestamp"] = DateTimeOffset.Now.ToUnixTimeMilliseconds() },
                ["MAP_GUIDE_PALYER_ROUTE_KEY"] = new Dictionary<string, object?> { ["timestamp"] = DateTimeOffset.Now.ToUnixTimeMilliseconds() },
                ["ENEMY_FILTER_SELECTED"] = "[]",
                ["gravity-guide-finish-status"] = "false"
            };
        }

        protected override Dictionary<string, object?> CreateHostEnvironment(WebSessionContext session)
        {
            var environment = base.CreateHostEnvironment(session);
            environment["wiki_type"] = "10";
            environment["state_id"] = CreateMapState(session.PageUrl)["state"];
            environment["devcode"] = DefaultMapDeviceCode;
            return environment;
        }

        private static Dictionary<string, object?> CreateMapState(string pageUrl)
        {
            Uri uri = new(pageUrl, UriKind.Absolute);
            Dictionary<string, string> query = ParseQuery(uri.Query);
            return new(StringComparer.Ordinal)
            {
                ["state"] = TryParseInt(query.GetValueOrDefault("state"), 8),
                ["country"] = TryParseInt(query.GetValueOrDefault("country"), 1),
                ["x"] = TryParseDouble(query.GetValueOrDefault("x"), 0),
                ["y"] = TryParseDouble(query.GetValueOrDefault("y"), 0),
                ["zoom"] = query.GetValueOrDefault("zoom") ?? "0.00"
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split('=', 2);
                values[Uri.UnescapeDataString(parts[0])] = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }

            return values;
        }

        private static int TryParseInt(string? value, int fallback)
        {
            return int.TryParse(value, out int result) ? result : fallback;
        }

        private static double TryParseDouble(string? value, double fallback)
        {
            return double.TryParse(value, out double result) ? result : fallback;
        }
    }
}
