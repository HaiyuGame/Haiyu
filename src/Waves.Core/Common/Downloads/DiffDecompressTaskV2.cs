using Waves.Diff;

namespace Waves.Core.Common.Downloads;

/// <summary>
/// 解压2功能
/// </summary>
public class DiffDecompressTaskV2
{
    public static async Task<int> DecompressKrdiffFile(
        string folder,
        string? krdiffPath,
        int curent,
        int total,
        string? tempFolder = null,
        IProgress<(GameContextActionType, string, KrDiffDecompressResult)> progress = null
    )
    {
        if (krdiffPath == null)
            return -1000;
        DiffDecompressManagerV2 manager = new DiffDecompressManagerV2(
            folder,
            tempFolder ?? folder,
            krdiffPath
        );
        IProgress<KrDiffDecompressResult> decompress = new Progress<KrDiffDecompressResult>();
        ((Progress<KrDiffDecompressResult>)decompress).ProgressChanged += async (s, e) =>
        {
            progress?.Report(
                (GameContextActionType.Decompress, System.IO.Path.GetFileName(krdiffPath), e)
            );
        };
        var result = await manager.StartAsync(decompress);
        return result;
    }
}
