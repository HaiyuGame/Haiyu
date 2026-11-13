namespace Waves.Core.Services.GameResourceProvider;

public sealed partial class BundleGameResourceProvider
{
    private async Task<string?> ReadLaunchBundleAsync(CancellationToken token)
    {
        var name = await LocalConfig.GetConfigAsync(GameLocalSettingName.BunleName, token);
        return string.IsNullOrWhiteSpace(name) ? ApiConfig.BunleDefaultBundleName : name;
    }

    public async Task<string?> ResolveLaunchPackageTypeAsync(string gameFolder, CancellationToken token = default)
    {
        var name = await ReadLaunchBundleAsync(token);
        return await ReadLaunchPackageTypeAsync(gameFolder, name, token);
    }

    private async Task<string?> ReadLaunchPackageTypeAsync(string gameFolder, string? bundleName, CancellationToken token)
    {
        if (!SupportsPackageType || string.IsNullOrWhiteSpace(bundleName))
            return null;
        var type = await GamePackageStateService.ReadAsync(gameFolder, ApiConfig.PackageStatePath!, bundleName, token);
        return GamePackageStateService.ToValue(type ?? GamePackageType.Minimal);
    }

    public async Task<string> ResolveLaunchArgumentsAsync(string gameFolder, string? customArguments,
        CancellationToken token = default)
    {
        // 仅替换独立的受管参数，保留用户引号内的文字和其他参数。
        var arguments = Regex.Replace(customArguments ?? "",
            @"""[^""]*""|(?<!\S)-(?:krqlv|krpkg)=(?:""[^""]*""|\S+)",
            match => match.Value[0] == '"' ? match.Value : "", RegexOptions.IgnoreCase).Trim();
        var bundle = await ReadLaunchBundleAsync(token);
        var quality = bundle?.ToUpperInvariant() switch
        {
            "HD" => "hd",
            "SD" => "sd",
            "UHD" => "uhd",
            _ => null
        };
        if (quality is not null)
            arguments = $"{arguments} -krqlv={quality}".Trim();
        var packageType = await ReadLaunchPackageTypeAsync(gameFolder, bundle, token);
        if (packageType is not null)
            arguments = $"{arguments} -krpkg={packageType}".Trim();
        return arguments;
    }
}
