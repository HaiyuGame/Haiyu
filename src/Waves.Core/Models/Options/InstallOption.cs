namespace Waves.Core.Models.Options;

/// <summary>
/// 下载安装模式
/// </summary>
public class InstallOption
{
    /// <summary>下载载体的实际缓存目录（已包含 Diff 或 prodDownloads），不是用户选择的根目录。</summary>
    public string? DownloadFolder { get; set; }

    public bool IsProd { get; set; } = false;

    public bool IsAdvance { get; set; } = false;

    /// <summary>从用户选择的根目录生成操作缓存目录。记录的实际缓存路径不再次调用此方法。</summary>
    public static string BuildCacheFolder(string rootFolder, bool isPredownload) =>
        Path.Combine(Path.GetFullPath(rootFolder), isPredownload ? "prodDownloads" : "Diff");

    public string ResolveDownloadFolder(string gameFolder)
    {
        var folder = Path.GetFullPath(string.IsNullOrWhiteSpace(DownloadFolder)
            ? BuildCacheFolder(gameFolder, IsProd || IsAdvance)
            : DownloadFolder);
        if (string.Equals(folder.TrimEnd(Path.DirectorySeparatorChar),
                Path.GetPathRoot(folder)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || string.Equals(folder.TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(gameFolder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("请选择独立的下载缓存目录。", nameof(DownloadFolder));
        return folder;
    }

    /// <summary>
    /// 普通更新游戏
    /// </summary>
    /// <returns></returns>
    public static InstallOption CreateDefault()
    {
        return new();
    }

    /// <summary>
    /// 预下载模式
    /// </summary>
    /// <returns></returns>
    public static InstallOption CreateProdownlad()
    {
        return new()
        {
            IsProd = true,
            IsAdvance = false
        };
    }

    /// <summary>
    /// 提前安装模式
    /// </summary>
    /// <returns></returns>
    public static InstallOption CreateAdvance()
    {
        return new()
        {
            IsProd = false,
            IsAdvance = true
        };
    }
}
