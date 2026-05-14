namespace Waves.Core.Models.CloudGame;

public sealed class SessionLaunchOptions
{
    public required string GameUrl { get; init; }

    public string BootstrapUrl { get; init; } = string.Empty;

    public CloudGameStreamSession? StreamSession { get; init; }

    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public string CookieDomain { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> AdditionalHeaders { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> Cookies { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> StorageItems { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<string> HeaderHostPatterns { get; init; } = new List<string>();

    public string PreloadScript { get; init; } = string.Empty;

    public StreamQualityOptions Quality { get; init; } = StreamQualityOptions.Default;
}

public sealed record StreamQualityOptions(int BitRate, int Fps, int Width, int Height, int CodecType)
{
    public static readonly StreamQualityOptions Default = new(18000, 60, 1920, 1080, 21);

    public string ResolutionKey => $"{Width}x{Height}";
}


public sealed record CloudGameStreamSession
{
    public required string DispatchMessage { get; init; }

    public required string TenantKey { get; init; }

    public required string ScriptUrl { get; init; }

    public required WelinkStartParameters StartParameters { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public string SessionKey { get; init; } = string.Empty;

    public string WalletSummary { get; init; } = string.Empty;
}



public sealed record WelinkStartParameters(
    string TenantKey,
    string GameId,
    string Resolution,
    int BitRate,
    int Fps,
    int CodecType,
    string Version,
    string CmdLine,
    string BizData);