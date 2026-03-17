using Waves.Core.Common;
using Waves.Core.Models;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

partial class KuroGameContextBase
{

    private async Task<int> DecompressKrdiffFile(
        string folder,
        string? krdiffPath,
        int curent,
        int total,
        string? tempFolder = null
    )
    {
        if (krdiffPath == null)
            return -1000;
        DiffDecompressManager manager = new DiffDecompressManager(
            folder,
            tempFolder ?? folder,
            krdiffPath
        );
        IProgress<(double, double)> progress = new Progress<(double, double)>();
        ((Progress<(double, double)>)progress).ProgressChanged += async (s, e) =>
        {
            if (gameContextOutputDelegate == null)
                return;
            await gameContextOutputDelegate
                .Invoke(
                    this,
                    new GameContextOutputArgs
                    {
                        Type = GameContextActionType.Decompress,
                        CurrentSize = (long)e.Item1,
                        TotalSize = (long)e.Item2,
                        DownloadSpeed = 0,
                        VerifySpeed = 0,
                        RemainingTime = TimeSpan.FromMicroseconds(0),
                        IsAction = _downloadState?.IsActive ?? false,
                        IsPause = _downloadState?.IsPaused ?? false,
                        TipMessage = "正在解压合并资源",
                        CurrentDecompressCount = curent,
                        MaxDecompressValue = total,
                    }
                )
                .ConfigureAwait(false);
        };
        var result = await manager.StartAsync(progress);
        Logger.WriteInfo($"解压程序结果{result}");
        return result;
    }
}
