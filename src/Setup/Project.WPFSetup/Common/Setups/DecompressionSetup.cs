using System.IO;
using System.IO.Compression;
using System.Text.Json;
using SharpCompress.Archives.SevenZip;

namespace Project.WPFSetup.Common.Setups;

public class DecompressionSetup : ISetup
{
    /// <summary>
    /// 解压资源
    /// </summary>
    public DecompressionSetup() { }

    public string SetupName => "释放资源";

    public int MaxProgress => 100;

    /// <summary>
    /// 释放文件
    /// </summary>
    /// <param name="fileBuffer"></param>
    /// <param name="rootDir"></param>
    /// <param name="process"></param>
    /// <returns></returns>
    async Task<(string, bool)> ExtractFileAsync(
        byte[] fileBuffer,
        string rootDir,
        IProgress<(double, string)> progress
    )
    {
        try
        {
            List<string> installFile = new List<string>();
            using (var memoryStream = new MemoryStream(fileBuffer))
            using (var archive = SharpCompress.Archives.Zip.ZipArchive.Open(memoryStream))
            {
                long totalBytes = archive.Entries.Sum(x => x.Size);
                long processedBytes = 0;
                byte[] buffer = new byte[81920];

                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        string path = Path.Combine(rootDir, entry.Key);
                    }
                    else
                    {
                        string path = Path.Combine(rootDir, entry.Key);
                        if (File.Exists(path))
                            File.Delete(path);
                        string dir = Path.GetDirectoryName(path);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        using (var entryStream = entry.OpenEntryStream())
                        using (
                            var fileStream = new FileStream(
                                path,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None,
                                buffer.Length,
                                FileOptions.Asynchronous | FileOptions.SequentialScan
                            )
                        )
                        {
                            while (true)
                            {
                                int bytesRead = await entryStream
                                    .ReadAsync(buffer, 0, buffer.Length)
                                    .ConfigureAwait(false);
                                if (bytesRead == 0)
                                    break;
                                await fileStream
                                    .WriteAsync(buffer, 0, bytesRead)
                                    .ConfigureAwait(false);
                                processedBytes += bytesRead;
                                double percentComplete =
                                    (double)processedBytes / totalBytes * 100.0;
                                progress?.Report((percentComplete, SetupName));
                            }
                        }
                        installFile.Add(path);
                        //创建卸载快照
                    }
                }

                var datFile = Path.Combine(rootDir, "unstall.dat");
                var unstallexe = Path.Combine(rootDir, "uninstall.exe");
                if (File.Exists(datFile))
                {
                    File.Delete(datFile);
                }
                installFile.Add(datFile);
                installFile.Add(unstallexe);
                var datFs = File.CreateText(datFile);
                var jsonStr = JsonSerializer.Serialize(installFile);
                await datFs.WriteLineAsync(jsonStr);
                await datFs.FlushAsync();
                datFs.Dispose();
            }
            return ("", true);
        }
        catch (Exception ex)
        {
            return (ex.Message, false);
        }
    }

    public async Task<(string, bool)> ExecuteAsync(
        SetupProperty property,
        IProgress<(double, string)> progress,
        int maxValue
    )
    {
        return await ExtractFileAsync(
                Resources.Resource1.InstallFile,
                property.InstallPath,
                progress
            )
            .ConfigureAwait(false);
    }
}
