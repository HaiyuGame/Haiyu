using Haiyu.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Waves.Api.Models;
using Waves.Core.Models.Downloader;

namespace Waves.Core.GameContext;

/// <summary>
/// Zip更新下载
/// </summary>
partial class KuroGameContextBase
{
    public async Task<long> GetDownloadSize(string url)
    {
        var downloadCts = GetCTS(false);
        var state = GetState(false);
        HttpRequestMessage message = new HttpRequestMessage();
        message.Method = HttpMethod.Get;
        message.RequestUri = new Uri(url);
        using var response = await HttpClientService
                    .GameDownloadClient.SendAsync(
                        message,
                        HttpCompletionOption.ResponseHeadersRead,
                        downloadCts.Token
                    )
                    .ConfigureAwait(false);
        var length = response.Content.Headers.ContentLength ?? 0;
        return length;
    }

    /// <summary>
    /// 下载解压包程序
    /// </summary>
    /// <param name="diffSavePath"></param>
    /// <param name="zips"></param>
    /// <param name="resource"></param>
    /// <param name="folder"></param>
    /// <param name="isProd"></param>
    /// <returns></returns>
    private async Task<bool>? DownloadZipFilesAsync(string diffSavePath, IEnumerable<ZipFileInfo> zips, GameLauncherSource resource,string folder,bool isProd)
    {
        this.CDNSpeedTester = new CDNSpeedTester();
        var patchInfos = zips.Where(x => x.Dest.EndsWith("krpdiff")).ToList();
        ParallelOptions options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = MAX_Concurrency_Count,
            CancellationToken = _downloadCTS.Token,
        };
        
        return true;
    }
}
