using System.Text.Json.Serialization;

namespace Haiyu.Models
{
    public sealed class KuroLoginSnapshot
    {
        [JsonPropertyName("token")]
        public string Token { get; init; } = string.Empty;

        [JsonPropertyName("did")]
        public string Did { get; init; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; init; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; init; } = string.Empty;

        [JsonPropertyName("headUrl")]
        public string HeadUrl { get; init; } = string.Empty;

        [JsonPropertyName("requestIp")]
        public string RequestIp { get; init; } = string.Empty;

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; init; } = "3.0.2";

        [JsonPropertyName("channelId")]
        public string ChannelId { get; init; } = "2";

        [JsonPropertyName("enterSource")]
        public string EnterSource { get; init; } = "12";

        [JsonPropertyName("ua")]
        public string UserAgentName { get; init; } = "KuroGameBox";

        [JsonPropertyName("os")]
        public string Os { get; init; } = "Android";
    }

    public sealed class WebSessionContext
    {
        private const string DataCenterUrlTemplate = "https://web-static.kurobbs.com/mcbox/index.html#/mc-role-box?accessType=1&roleId={0}&serverId={1}";
        private const string GrowthCalculatorUrl = "https://web-static.kurobbs.com/growth-calculator/index.html#/";
        private const string CalendarUrl = "https://web-static.kurobbs.com/mccalendar/index.html#/";
        private const string MapUrlTemplate = "https://www.kurobbs.com/mc/map/?v=4.0&state={0}&country={1}&x={2}&y={3}&zoom={4}";
        private const string ResourceBriefingUrl = "https://web-static.kurobbs.com/resource-briefing/index.html#/home";

        private WebSessionContext(
            KuroLoginSnapshot snapshot,
            string pageUrl,
            string serverId,
            string roleId,
            string serverName,
            string roleName,
            int gameId = 3)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            PageUrl = pageUrl;
            ServerId = serverId ?? string.Empty;
            RoleId = roleId ?? string.Empty;
            ServerName = serverName ?? string.Empty;
            RoleName = roleName ?? string.Empty;
            GameId = gameId;
        }

        [JsonPropertyName("snapshot")]
        public KuroLoginSnapshot Snapshot { get; }

        [JsonPropertyName("token")]
        public string Token => Snapshot.Token;

        [JsonPropertyName("did")]
        public string Did => Snapshot.Did;

        [JsonPropertyName("userId")]
        public string UserId => Snapshot.UserId;

        [JsonPropertyName("userName")]
        public string UserName => Snapshot.UserName;

        [JsonPropertyName("headUrl")]
        public string HeadUrl => Snapshot.HeadUrl;

        [JsonPropertyName("requestIp")]
        public string RequestIp => Snapshot.RequestIp;

        [JsonPropertyName("appVersion")]
        public string AppVersion => Snapshot.AppVersion;

        [JsonPropertyName("channelId")]
        public string ChannelId => Snapshot.ChannelId;

        [JsonPropertyName("enterSource")]
        public string EnterSource => Snapshot.EnterSource;

        [JsonPropertyName("ua")]
        public string UserAgentName => Snapshot.UserAgentName;

        [JsonPropertyName("os")]
        public string Os => Snapshot.Os;

        [JsonPropertyName("serverId")]
        public string ServerId { get; }

        [JsonPropertyName("roleId")]
        public string RoleId { get; }

        [JsonPropertyName("serverName")]
        public string ServerName { get; }

        [JsonPropertyName("roleName")]
        public string RoleName { get; }

        [JsonPropertyName("gameId")]
        public int GameId { get; }

        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; }

        public string GetDataCenterUrl()
        {
            return string.Format(DataCenterUrlTemplate, RoleId, ServerId);
        }

        public string GetGrowthCalculatorUrl()
        {
            return GrowthCalculatorUrl;
        }

        public string GetResourceBriefingUrl()
        {
            return ResourceBriefingUrl;
        }

        public string GetCalendarUrl()
        {
            return CalendarUrl;
        }

        public string GetMapUrl(
            int state = 8,
            int country = 1,
            double x = 0,
            double y = 0,
            string zoom = "0.00")
        {
            return string.Format(MapUrlTemplate, state, country, x, y, zoom);
        }

        public string GetPageUrl()
        {
            return PageUrl;
        }

        public static WebSessionContext CreateDataCenter(
            KuroLoginSnapshot snapshot,
            string serverId,
            string roleId,
            string? serverName = null,
            string? roleName = null)
        {
            return new WebSessionContext(
                snapshot,
                string.Format(DataCenterUrlTemplate, roleId, serverId),
                serverId,
                roleId,
                serverName ?? string.Empty,
                roleName ?? string.Empty);
        }

        public static WebSessionContext CreateGrowthCalculator(
            KuroLoginSnapshot snapshot,
            string serverId,
            string roleId,
            string? serverName = null,
            string? roleName = null)
        {
            return new WebSessionContext(
                snapshot,
                GrowthCalculatorUrl,
                serverId,
                roleId,
                serverName ?? string.Empty,
                roleName ?? string.Empty);
        }

        public static WebSessionContext CreateResourceBriefing(
            KuroLoginSnapshot snapshot,
            string serverId,
            string roleId,
            string? serverName = null,
            string? roleName = null)
        {
            return new WebSessionContext(
                snapshot,
                ResourceBriefingUrl,
                serverId,
                roleId,
                serverName ?? string.Empty,
                roleName ?? string.Empty);
        }

        public static WebSessionContext CreateCalendar(
            KuroLoginSnapshot snapshot,
            string serverId,
            string roleId,
            string? serverName = null,
            string? roleName = null)
        {
            return new WebSessionContext(
                snapshot,
                CalendarUrl,
                serverId,
                roleId,
                serverName ?? string.Empty,
                roleName ?? string.Empty);
        }

        public static WebSessionContext CreateMap(
            KuroLoginSnapshot snapshot,
            string serverId,
            string roleId,
            string? serverName = null,
            string? roleName = null,
            int state = 8,
            int country = 1,
            double x = 0,
            double y = 0,
            string zoom = "0.00")
        {
            return new WebSessionContext(
                snapshot,
                string.Format(MapUrlTemplate, state, country, x, y, zoom),
                serverId,
                roleId,
                serverName ?? string.Empty,
                roleName ?? string.Empty);
        }
    }

    public sealed class KuroBootstrapPayload
    {
        [JsonPropertyName("token")]
        public string Token { get; init; } = string.Empty;

        [JsonPropertyName("did")]
        public string Did { get; init; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; init; } = string.Empty;

        [JsonPropertyName("serverId")]
        public string ServerId { get; init; } = string.Empty;

        [JsonPropertyName("roleId")]
        public string RoleId { get; init; } = string.Empty;

        [JsonPropertyName("serverName")]
        public string ServerName { get; init; } = string.Empty;

        [JsonPropertyName("roleName")]
        public string RoleName { get; init; } = string.Empty;

        [JsonPropertyName("requestIp")]
        public string RequestIp { get; init; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; init; } = string.Empty;

        [JsonPropertyName("headUrl")]
        public string HeadUrl { get; init; } = string.Empty;

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; init; } = "3.0.2";

        [JsonPropertyName("channelId")]
        public string ChannelId { get; init; } = "2";

        [JsonPropertyName("enterSource")]
        public string EnterSource { get; init; } = "12";

        [JsonPropertyName("ua")]
        public string UserAgentName { get; init; } = "KuroGameBox";

        [JsonPropertyName("os")]
        public string Os { get; init; } = "Android";

        [JsonPropertyName("gameId")]
        public int GameId { get; init; } = 3;
    }

    public sealed class KuroSession()
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("did")]
        public string Did { get; set; }

        [JsonPropertyName("userId")]
        public string UserId { get; set; }
    }

    [JsonSerializable(typeof(KuroSession))]
    [JsonSerializable(typeof(KuroLoginSnapshot))]
    [JsonSerializable(typeof(WebSessionContext))]
    [JsonSerializable(typeof(KuroBootstrapPayload))]
    [JsonSerializable(typeof(Dictionary<string, object?>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(bool))]
    public partial class KuroSessionContext : JsonSerializerContext
    {
    }
}
