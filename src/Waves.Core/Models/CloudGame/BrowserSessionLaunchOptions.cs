namespace Waves.Core.Models.CloudGame
{
    public class BrowserSessionLaunchOptions
    {

        public string BootstrapUrl { get; init; } = string.Empty;

        public string AccessToken { get; init; } = string.Empty;

        public string RefreshToken { get; init; } = string.Empty;

        public string CloudApiToken { get; init; } = string.Empty;

        public string CookieDomain { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> AdditionalHeaders { get; init; } =
            new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> Cookies { get; init; } =
            new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> StorageItems { get; init; } =
            new Dictionary<string, string>();

        public IReadOnlyList<string> HeaderHostPatterns { get; init; } = new List<string>();

        public StreamQualityOptions Quality { get; init; }

        public int StreamDpi { get; init; } = 120;

        public CloudGameStreamSession StreamOptions { get; internal set; }

        public string DispatchMessage { get; set; }

        public bool IsComplete { get; internal set; }

        internal BrowserSessionLaunchOptions Clone()
        {
            return new BrowserSessionLaunchOptions()
            {
                AccessToken = this.AccessToken,
                Quality = this.Quality,
                AdditionalHeaders = this.AdditionalHeaders,
                BootstrapUrl = this.BootstrapUrl,
                CookieDomain = this.CookieDomain,
                Cookies = this.Cookies,
                HeaderHostPatterns = this.HeaderHostPatterns,
                RefreshToken = this.RefreshToken,
                CloudApiToken = this.CloudApiToken,
                StorageItems = this.StorageItems,
                StreamDpi = this.StreamDpi,
                StreamOptions = this.StreamOptions,
            };
        }
    }
}

public sealed record StreamQualityOptions(
    int BitRate,
    int BitRateMin,
    int Fps,
    int Width,
    int Height,
    int CodecType,
    string StreamStrategy,
    bool EnableImageEnhancement,
    int DPI,
    CloudQualityType Type = CloudQualityType.Clarity
)
{
    public const string SmoothPreset = "smooth";

    public const string ClearPreset = "clear";

    public string ResolutionKey => $"{Width}x{Height}";

    public int BitRateMax => BitRate;

    public CloudQualityType OfficialPreset => Type;

    
}
