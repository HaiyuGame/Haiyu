using Serilog.Core;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.Enums;

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
        DiffDecompressManagerV2 manager = new DiffDecompressManagerV2(
            folder,
            tempFolder ?? folder,
            krdiffPath
        );
        IProgress<KrDiffDecompressResult> decompress = new Progress<KrDiffDecompressResult>();
        ((Progress<KrDiffDecompressResult>)decompress).ProgressChanged += async (s, e) =>
        {
            progress?.Report((GameContextActionType.Download,System.IO.Path.GetFileName(krdiffPath),e));
        };
        var result = await manager.StartAsync(decompress);
        return result;
    }
}
