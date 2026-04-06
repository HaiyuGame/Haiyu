using Waves.Core.Models.Downloader;

namespace Waves.Core.Helpers;

public static class BuildFileHelper
{
    public static string BuildFilePath(string folder, IndexResource file)
    {
        var path = Path.Combine(folder, file.Dest.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(
            Path.GetDirectoryName(path) ?? throw new Exception($"文件{file.Dest}创建失败")
        );
        return path;
    }


    public static string BuildFilePath(string folder, PatchInfo item)
    {
        var path = Path.Combine(folder, item.Dest.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(
            Path.GetDirectoryName(path) ?? throw new Exception($"文件{item.Dest}创建失败")
        );
        return path;
    }

    public static string BuildFilePath(string folder, GroupFileInfo item)
    {
        var path = Path.Combine(folder, item.Dest.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(
            Path.GetDirectoryName(path) ?? throw new Exception($"文件{item.Dest}创建失败")
        );
        return path;
    }

    public static string BuildFilePath(string folder, string item)
    {
        var path = Path.Combine(folder, item.Replace('/', Path.DirectorySeparatorChar));
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
}
