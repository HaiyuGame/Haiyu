namespace Waves.Core.Models.CloudGame
{
    public class CloudGameStreamSession
    {
        public int ProviderType { get; init; }

        public string TencentUserKey { get; init; } = string.Empty;

        public string TencentDeviceId { get; init; } = string.Empty;

        public string TencentAllocRespJson { get; init; } = string.Empty;

        public string TencentToken { get; init; } = string.Empty;

        /// <summary>
        /// Welink 分发消息原文。
        /// </summary>
        public string DispatchMessage { get; init; }

        /// <summary>
        /// Welink 租户标识。
        /// </summary>
        public required string TenantKey { get; init; }

        public required string GameId { get; init; }

        /// <summary>
        /// Welink SDK 脚本地址。
        /// </summary>
        public required string ScriptUrl { get; init; }

        /// <summary>
        /// Welink 启动参数。
        /// </summary>
        public required WelinkStartParameters StartParameters { get; init; }

        /// <summary>
        /// 当前会话所在节点区域名称。
        /// </summary>
        public string RegionName { get; init; } = string.Empty;

        /// <summary>
        /// 当前会话键。
        /// </summary>
        public string SessionKey { get; init; } = string.Empty;

        /// <summary>
        /// 钱包时长摘要。
        /// </summary>
        public string WalletSummary { get; init; } = string.Empty;
    }
}
