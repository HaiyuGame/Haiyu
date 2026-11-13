namespace Waves.Core.Services;

public static class GamePackageStateService
{
    public static string ToValue(GamePackageType type) => type == GamePackageType.Minimal ? "min" : "max";

    public static GamePackageType? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "max" => GamePackageType.Full,
        "min" => GamePackageType.Minimal,
        _ => null
    };

    private static async Task<JsonObject?> ReadAsync(string path, CancellationToken token)
    {
        try
        {
            return JsonNode.Parse(await File.ReadAllTextAsync(path, token)) as JsonObject;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static async Task<GamePackageType?> ReadAsync(
        string gameFolder, string relativePath, string bundleName, CancellationToken token = default)
    {
        var state = await ReadAsync(BuildFileHelper.ResolveFilePath(gameFolder, relativePath), token);
        var value = (state?["tiers"] as JsonObject)?[bundleName] as JsonObject;
        return value?["packageType"] is JsonValue type && type.TryGetValue<string>(out var text)
            ? Parse(text) : null;
    }

    public static async Task WriteAsync(string gameFolder, string relativePath, string bundleName,
        GamePackageType packageType, CancellationToken token = default)
    {
        var path = BuildFileHelper.ResolveFilePath(gameFolder, relativePath);
        var state = await ReadAsync(path, token) ?? new JsonObject { ["schemaVersion"] = 1 };
        if (state["tiers"] is not JsonObject tiers)
        {
            tiers = new JsonObject();
            state["tiers"] = tiers;
        }
        tiers[bundleName] = new JsonObject
        {
            ["packageType"] = ToValue(packageType),
            ["updatedAtUtc"] = DateTime.UtcNow.ToString("o")
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, state.ToJsonString(), token);
        token.ThrowIfCancellationRequested();
        File.Move(temporary, path, true);
    }
}
