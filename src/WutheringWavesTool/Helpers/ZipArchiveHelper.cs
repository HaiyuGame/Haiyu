using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace Haiyu.Helpers;

public static class ZipArchiveHelper
{
    public static async Task UnZipFileAsync(
        string fileArchive,
        string targetFolder,
        IProgress<double> progress,
        CancellationToken token = default
    )
    {
        ArgumentNullException.ThrowIfNull(fileArchive);
        ArgumentNullException.ThrowIfNull(targetFolder);
        Directory.CreateDirectory(targetFolder);
        await using (
            FileStream fs = new FileStream(
                fileArchive,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                8192,
                true
            )
        )

        using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read, false))
        {
            int totalEntries = archive.Entries.Count;
            if (totalEntries == 0)
            {
                progress?.Report(1.0);
                return;
            }

            long totalUncompressedSize = 0;
            long processedSize = 0;

            foreach (var entry in archive.Entries)
            {
                totalUncompressedSize += entry.Length;
            }

            using SemaphoreSlim semaphore = new SemaphoreSlim(
                1,1
            );

            var tasks = new List<Task>(archive.Entries.Count);

            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();

                string destinationPath = Path.Combine(targetFolder, entry.FullName);

                // 处理目录条目
                if (
                    string.IsNullOrEmpty(entry.Name)
                    || entry.FullName.EndsWith("/")
                    || entry.FullName.EndsWith("\\")
                )
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }
                string? directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                await semaphore.WaitAsync(token);

                tasks.Add(
                    Task.Run(
                        async () =>
                        {
                            try
                            {
                                await ExtractEntryAsync(entry, destinationPath, token);
                                long newProcessed = Interlocked.Add(
                                    ref processedSize,
                                    entry.Length
                                );
                                double percent = (double)newProcessed / totalUncompressedSize;
                                progress?.Report(Math.Min(percent, 1.0));
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        },
                        token
                    )
                );
            }
            await Task.WhenAll(tasks);
            progress?.Report(1.0);
        }
    }

    private static async Task ExtractEntryAsync(
        ZipArchiveEntry entry,
        string destinationPath,
        CancellationToken token
    )
    {
        await using (
            FileStream output = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                8192,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            )
        )

        await using (Stream input = entry.Open())
        {
            await input.CopyToAsync(output, 81920, token); // 80KB 缓冲区平衡性能与内存
            await output.FlushAsync(token);
        }
    }
}
