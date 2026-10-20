namespace Waves.Core.Common.Downloads;

public class DiffDecompressTask
{
    public static async Task<int> DecompressKrdiffFile(
        string folder,
        string? krdiffPath,
        int curent,
        int total,
        string? tempFolder = null,
        IProgress<(GameContextActionType,string, KrDiffDecompressResult)> progress = null
    )
    {
        if (krdiffPath == null)
            return -1000;
        DiffDecompressManager manager = new DiffDecompressManager(
            folder,
            tempFolder ?? folder,
            krdiffPath
        );
        IProgress<KrDiffDecompressResult> decompress = new Progress<KrDiffDecompressResult>();
        ((Progress<KrDiffDecompressResult>)decompress).ProgressChanged += async (s, e) =>
        {
            progress?.Report((GameContextActionType.Decompress,System.IO.Path.GetFileName(krdiffPath),e));
        };
        var result = await manager.StartAsync(decompress);
        return result;
    }
}
