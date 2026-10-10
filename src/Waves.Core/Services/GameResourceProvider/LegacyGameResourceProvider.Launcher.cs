namespace Waves.Core.Services.GameResourceProvider;

public sealed partial class LegacyGameResourceProvider
{
    private string GetLauncherRoot() => new Uri(string.IsNullOrWhiteSpace(ApiConfig.LauncherConfigUrl)
        ? ApiConfig.ConfigUrl : ApiConfig.LauncherConfigUrl).GetLeftPart(UriPartial.Authority);

    public async Task<GameLauncherStarter?> GetLauncherStarterAsync(CancellationToken token = default) =>
        await ReadAsync($"{GetLauncherRoot()}/launcher/{ApiConfig.AppId}_{ApiConfig.AppKey}/{ApiConfig.GameID}/information/{ApiConfig.Language}.json?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            GameLauncherStarterContext.Default.GameLauncherStarter, token);

    public async Task<LIndex?> GetDefaultLauncherValue(CancellationToken token = default) =>
        await ReadAsync($"{GetLauncherRoot()}/launcher/launcher/{ApiConfig.AppId}_{ApiConfig.AppKey}/{ApiConfig.GameID}/index.json",
            LauncherConfig.Default.LIndex, token);

    public async Task<LauncherBackgroundData?> GetLauncherBackgroundDataAsync(string backgroundCode, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(backgroundCode)) return new LauncherBackgroundData { FunctionSwitch = 0 };
        return await ReadAsync($"{GetLauncherRoot()}/launcher/{ApiConfig.AppId}_{ApiConfig.AppKey}/{ApiConfig.GameID}/background/{Uri.EscapeDataString(backgroundCode)}/{ApiConfig.Language}.json",
            LauncherConfig.Default.LauncherBackgroundData, token);
    }
}
