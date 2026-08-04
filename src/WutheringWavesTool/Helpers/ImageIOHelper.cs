using System.Buffers;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Haiyu.Helpers;

public static class ImageIOHelper
{
    public static async Task<BitmapImage> GetNetworkAsync(string url)
    {
        using (HttpClient httpClient = new HttpClient())
        {
            var stream = await httpClient.GetStreamAsync(url);

            InMemoryRandomAccessStream randomAccessStream = new InMemoryRandomAccessStream();

            using (var originalStream = stream.AsInputStream())
            {
                await RandomAccessStream.CopyAsync(originalStream, randomAccessStream);
            }

            randomAccessStream.Seek(0);
            BitmapImage source = new BitmapImage();
            await source.SetSourceAsync(randomAccessStream);
            return source;
        }
    }

    public static async Task<BitmapImage> GetLocalFileAsync(string url)
    {
        BitmapImage source = new BitmapImage();
        StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(url));
        using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
        {
            await source.SetSourceAsync(fileStream);
        }
        return source;
    }

    public static async Task<(BitmapImage?, string, string?)> HexImageAsync(
        string sourceFolder,
        string filePath
    )
    {
        System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        if (!Directory.Exists(sourceFolder))
            return (null, LanguageService.GetStringByText("源文件夹不存在！"), "");
        var dir = new DirectoryInfo(sourceFolder);
        var items = dir.EnumerateFiles()
            .Where(x => x.Name.EndsWith(".png") || x.Name.EndsWith(".jpg"));
        var fileMd5 = "";
        if (items.Count() == 0)
        {
            using (
                var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true
                )
            )
            {
                byte[] hashBytes = await md5.ComputeHashAsync(stream);
                fileMd5 = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            var extension = new FileInfo(filePath).Extension;
            var guid = Guid.NewGuid().ToString("N");
            var file = $"{sourceFolder}\\{guid}.png";
            File.Move(filePath, file);
            md5.Dispose();
            return new(new(new(file)), file, fileMd5);
        }
        using (
            var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true
            )
        )
        {
            byte[] hashBytes = await md5.ComputeHashAsync(stream);
            fileMd5 = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        if (string.IsNullOrWhiteSpace(fileMd5))
        {
            return (null, LanguageService.GetStringByText("文件MD5计算失败！"), fileMd5);
        }
        List<(string, string)> hashTable = new();
        foreach (var item in items)
        {
            using (
                var stream = new FileStream(
                    item.FullName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true
                )
            )
            {
                byte[] hashBytes = await md5.ComputeHashAsync(stream);
                var folderFile = BitConverter
                    .ToString(hashBytes)
                    .Replace("-", "")
                    .ToLowerInvariant();
                hashTable.Add(new(item.FullName, folderFile));
            }
        }
        //如果文件夹中存在文件
        if (hashTable.Where(x => x.Item2 == fileMd5).Any())
        {
            var value = hashTable.Where(x => x.Item2 == fileMd5).First()!;
            return new(new(new(value.Item1)), value.Item1, value.Item2);
        }
        else
        {
            var name = sourceFolder + $"\\{System.IO.Path.GetFileName(filePath)}";
            File.Copy(filePath, name);
            return new(new(new(name)), name, fileMd5);
        }
    }

    public static async Task<IRandomAccessStream?> GetImageDataAsync(this string filePath)
    {
        using (
            FileStream fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                4096,
                true
            )
        )
        {
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent((int)fs.Length);
            try
            {
                await fs.ReadAsync(buffer);
                InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream();
                await Stream.WriteAsync(buffer.AsBuffer());
                Stream.Seek(0);
                return Stream;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public static async Task<ImageSource> ConvertBitmapImageAsync(
        this IRandomAccessStream imageStream,
        int? desiredWidth = null
    )
    {
        var bitmapImage = new BitmapImage { DecodePixelType = DecodePixelType.Logical };
        if (desiredWidth is { } width)
            bitmapImage.DecodePixelWidth = width;
        await bitmapImage.SetSourceAsync(imageStream);
        return bitmapImage;
    }
}
