namespace Waves.Core.Helpers;

public static class BuildFileHelper
{
    public static string BuildFilePath(string folder, GameFileInfo file) => BuildFilePath(folder, file.Dest);

    public static string ResolveFilePath(string folder, string relativePath)
    {
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"资源路径超出目标目录：{relativePath}");
        return path;
    }

    public static string BuildFilePath(string folder, string item)
    {
        var path = ResolveFilePath(folder, item);
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(path) ?? throw new Exception($"文件{item}创建失败")
            );
        }
        catch (Exception)
        {
        }
        return path;
    }

    public static Task<long> GetDiskAvailableSize(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
        {
            return Task.FromResult(0L);
        }

        try
        {
            var fullPath = Path.GetFullPath(v);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return Task.FromResult(0L);
            }

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!string.Equals(drive.Name, root, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!drive.IsReady)
                {
                    return Task.FromResult(0L);
                }

                return Task.FromResult(drive.AvailableFreeSpace);
            }

            return Task.FromResult(0L);
        }
        catch
        {
            return Task.FromResult(0L);
        }
    }

    public static bool GetFileLength(string path,out long size)
    {
        if (File.Exists(path))
        {
            FileInfo info = new FileInfo(path);
            size = info.Length;
            return true;
        }
        size = 0;
        return false;
    }
}
