namespace Waves.Core.Services.GameResourceProvider;

public sealed partial class BundleGameResourceProvider
{
    private IEnumerable<string> ConfigurationUrls() =>
        new[] { ApiConfig.BunleConfigUrl, ApiConfig.BunleBackUpConfigUrl }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct();

    private async Task<T> ReadJsonAsync<T>(IEnumerable<string> urls,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken token)
    {
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                using var response = await _getClient().GetAsync(url, token);
                response.EnsureSuccessStatusCode();
                var bytes = await ReadJsonBytesAsync(response.Content, token);
                return JsonSerializer.Deserialize(bytes, typeInfo)
                    ?? throw new JsonException($"资源响应为空：{url}");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
            { last = ex; }
        }
        throw new IOException("新版启动器资源请求失败。", last);
    }

    private static async Task<byte[]> ReadJsonBytesAsync(HttpContent content, CancellationToken token)
    {
        var bytes = await content.ReadAsByteArrayAsync(token);
        if (bytes.Length < 2 || bytes[0] != 0x1F || bytes[1] != 0x8B) return bytes;
        using var input = new MemoryStream(bytes, writable: false);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        await gzip.CopyToAsync(output, token);
        return output.ToArray();
    }

    public async Task ConfigureFromLauncherAsync(string launcherDirectory, CancellationToken token = default)
    {
        if (ApiConfig is null) throw new InvalidOperationException("请先调用 SetConfig。");
        var path = Path.Combine(launcherDirectory, "Assets", "KRApp.conf");
        var bytes = Convert.FromBase64String(await File.ReadAllTextAsync(path, token));
        for (int i = 0; i < bytes.Length; i++) bytes[i] ^= 99;
        var root = JsonSerializer.Deserialize(bytes, BundleBootstrapJsonContext.Default.BundleLauncherBootstrap)
            ?? throw new JsonException("启动器引导配置为空。");
        var matches = root.Games.Where(game =>
            (string.IsNullOrEmpty(ApiConfig.GameID) || game.GameId == ApiConfig.GameID)
            && (string.IsNullOrEmpty(ApiConfig.AppId) || game.ResId == ApiConfig.AppId)).ToList();
        if (matches.Count != 1) throw new InvalidDataException("启动器中的游戏/服务器与 ApiConfig 不匹配。");
        var selected = matches[0];
        if (string.IsNullOrWhiteSpace(selected.ConfigUrl) || string.IsNullOrWhiteSpace(selected.AppKey)
            || string.IsNullOrWhiteSpace(selected.ServerCode)) throw new InvalidDataException("启动器分包引导配置缺少地址、key 或服务器编号。");
        token.ThrowIfCancellationRequested();
        ApiConfig.BunleConfigUrl = selected.ConfigUrl;
        ApiConfig.BunleBackUpConfigUrl = selected.BackUpConfigUrl;
        ApiConfig.BunleAppKey = selected.AppKey;
        ApiConfig.BunleServerCode = selected.ServerCode;
        ApiConfig.BunleDefaultBundleName = selected.DefaultBundleName;
        ApiConfig.BunleLauncherAppId = root.AppId;
        ApiConfig.BunleLauncherAppKey = root.AppKey;
        ApiConfig.BunleLauncherConfigUrl = root.LauncherConfigUrl;
        ApiConfig.BunleLauncherBackUpConfigUrl = root.LauncherBackUpConfigUrl;
    }

    private IEnumerable<string> ContentUrls(string category, string relative)
    {
        if (string.IsNullOrWhiteSpace(ApiConfig.BunleAppKey) || string.IsNullOrWhiteSpace(ApiConfig.BunleServerCode))
            throw new InvalidOperationException("请配置 BunleAppKey 和 BunleServerCode，或读取新版启动器配置。");
        var appId = ApiConfig.BunleLauncherAppId ?? ApiConfig.AppId;
        var key = ApiConfig.BunleLauncherAppKey ?? ApiConfig.BunleAppKey;
        var candidates = new[] { ApiConfig.BunleLauncherConfigUrl, ApiConfig.BunleLauncherBackUpConfigUrl }
            .Concat(ConfigurationUrls()).Where(x => !string.IsNullOrWhiteSpace(x));
        return candidates.Select(x => new Uri(x!).GetLeftPart(UriPartial.Authority)).Distinct()
            .Select(root => $"{root}/launcher/{category}/{Uri.EscapeDataString(appId)}_{Uri.EscapeDataString(key)}/{Uri.EscapeDataString(ApiConfig.GameID)}/{Uri.EscapeDataString(ApiConfig.BunleServerCode)}/{relative}");
    }

    public async Task<GameLauncherStarter?> GetLauncherStarterAsync(CancellationToken token = default) =>
        await ReadJsonAsync(ContentUrls("app", $"information/{Uri.EscapeDataString(ApiConfig.Language)}.json?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"),
            GameLauncherStarterContext.Default.GameLauncherStarter, token);

    public async Task<LIndex?> GetDefaultLauncherValue(CancellationToken token = default)
    {
        var source = await ReadConfigAsync(token);
        return new LIndex { FunctionCode = new global::Waves.Api.Models.Launcher.FunctionCode { Background = source.Config.FunctionCode.Background } };
    }

    public async Task<LauncherBackgroundData?> GetLauncherBackgroundDataAsync(string backgroundCode, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(backgroundCode)) return new LauncherBackgroundData { FunctionSwitch = 0 };
        return await ReadJsonAsync(ContentUrls("game", $"background/{Uri.EscapeDataString(backgroundCode)}/{Uri.EscapeDataString(ApiConfig.Language)}.json"),
            LauncherConfig.Default.LauncherBackgroundData, token);
    }
}

internal sealed class BundleLauncherBootstrap
{
    [JsonPropertyName("appId")] public string AppId { get; set; } = "";
    [JsonPropertyName("appKey")] public string AppKey { get; set; } = "";
    [JsonPropertyName("launcherConfigUrl")] public string LauncherConfigUrl { get; set; } = "";
    [JsonPropertyName("launcherBackUpConfigUrl")] public string LauncherBackUpConfigUrl { get; set; } = "";
    [JsonPropertyName("games")] public List<BundleGameBootstrap> Games { get; set; } = [];
}
internal sealed class BundleGameBootstrap
{
    [JsonPropertyName("gameId")] public string GameId { get; set; } = "";
    [JsonPropertyName("resId")] public string ResId { get; set; } = "";
    [JsonPropertyName("appKey")] public string AppKey { get; set; } = "";
    [JsonPropertyName("serverCode")] public string ServerCode { get; set; } = "";
    [JsonPropertyName("configUrl")] public string ConfigUrl { get; set; } = "";
    [JsonPropertyName("backUpConfigUrl")] public string BackUpConfigUrl { get; set; } = "";
    [JsonPropertyName("defaultBundleName")] public string DefaultBundleName { get; set; } = "";
}
[JsonSerializable(typeof(BundleLauncherBootstrap))]
internal partial class BundleBootstrapJsonContext : JsonSerializerContext { }
