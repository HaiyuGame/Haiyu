namespace Waves.Core.Models;

public sealed class CustomLauncherOptions
{
    public bool Enabled { get; set; }
    public string TargetPath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;

    public static async Task<CustomLauncherOptions> LoadAsync(GameLocalConfig config) => new()
    {
        Enabled = bool.TryParse(await config.GetConfigAsync(GameLocalSettingName.CustomLauncherEnabled), out var enabled) && enabled,
        TargetPath = await config.GetConfigAsync(GameLocalSettingName.CustomLauncherPath) ?? string.Empty,
        Arguments = await config.GetConfigAsync(GameLocalSettingName.CustomLauncherArguments) ?? string.Empty,
    };

    public Task SaveAsync(GameLocalConfig config) => config.SaveConfigsAsync(new Dictionary<string, string>
    {
        [GameLocalSettingName.CustomLauncherEnabled] = Enabled.ToString(),
        [GameLocalSettingName.CustomLauncherPath] = TargetPath.Trim(),
        [GameLocalSettingName.CustomLauncherArguments] = Arguments,
    });

    public string? Validate()
    {
        if (!Enabled) return null;
        if (!Path.IsPathFullyQualified(TargetPath)) return "请选择启动程序的完整路径。";
        if (!string.Equals(Path.GetExtension(TargetPath), ".exe", StringComparison.OrdinalIgnoreCase))
            return "请选择 .exe 可执行文件。";
        if (!File.Exists(TargetPath)) return "启动程序不存在，请重新选择。";
        return null;
    }

    public ProcessStartInfo CreateStartInfo(string gameArguments)
    {
        var error = Validate();
        if (error is not null) throw new InvalidOperationException(error);
        var arguments = string.Join(" ", new[] { gameArguments.Trim(), Arguments.Trim() }.Where(x => x.Length > 0));
        return new(TargetPath, arguments)
        {
            WorkingDirectory = Path.GetDirectoryName(TargetPath),
            UseShellExecute = true,
        };
    }
}
