using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Core.Common;

/// <summary>
/// 资源包组合控制器
/// </summary>
public static class DownloadResourceExtensions
{
    /// <summary>
    /// 合并V1的下载资源
    /// </summary>
    /// <param name="gameLauncherSource"></param>
    /// <returns></returns>
    public static async Task<GameVersionInfo> MargeFile(IndexGameResource gameLauncherSource)
    {
        return new GameVersionInfo()
        {
            DefaultResource = gameLauncherSource
                .Resource.Select(x => new GameFileInfo()
                {
                    Chunks = x.ChunkInfos,
                    Dest = x.Dest,
                    End = x.End,
                    Start = x.Start,
                    Hash = x.Md5,
                    Size = x.Size,
                })
                .ToList(),
        };
    }

    public static async Task MargeFile(GameLauncherBunleSource gameLauncherBunleSource) { }

    public static async Task MargeFile(PatchIndexGameResource pathResource) { }
}
