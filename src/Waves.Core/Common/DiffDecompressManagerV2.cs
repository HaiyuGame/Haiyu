using Waves.Diff;

namespace Waves.Core.Common;

public class DiffDecompressManagerV2
{
    public DiffDecompressManagerV2(string oldFolder, string newFolder, string diffFile)
    {
        OldFolder = oldFolder;
        NewFolder = newFolder;
        DiffFile = diffFile;
    }

    public string OldFolder { get; }
    public string NewFolder { get; }
    public string DiffFile { get; }

    public async Task<int> StartAsync(IProgress<KrDiffDecompressResult> progress)
    {
        try
        {
            IProgress<DiffProgress> inProgress = new Progress<DiffProgress>(
                (s) =>
                {
                    progress.Report(
                        new KrDiffDecompressResult()
                        {
                            FileTotalCount = s.FileTotalCount,
                            PatchedCurrentBytes = s.PatchedCurrentBytes,
                            PatchedFileCount = s.PatchedFileCount,
                            PatchingFileCurrentSize = s.PatchingFileCurrentSize,
                            PatchingFileTotalSize = s.PatchingFileTotalSize,
                            PatchTotalBytes = s.PatchTotalBytes,
                            SpeedValue = s.SpeedValue,
                        }
                    );
                }
            );
            DiffPatcher.PatchDirectory(
                OldFolder,
                DiffFile,
                NewFolder,
                (s) =>
                {
                    inProgress.Report(s);
                }
            );
            return await Task.FromResult(0);
        }
        catch (Exception)
        {
            Debug.WriteLine($"原版补丁解压失败！");
            return await Task.FromResult(-1);
        }
    }
}
