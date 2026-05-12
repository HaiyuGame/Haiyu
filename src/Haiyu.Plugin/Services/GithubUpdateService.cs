using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Haiyu.Plugin.Common;
using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Models;
using Waves.Core.Settings;

namespace Haiyu.Plugin.Services;

/// <summary>
/// Github更新源
/// </summary>
public class GithubUpdateService : IUpdateService
{
    private const int BufferSize = 81920;
    private const string Owner = "HaiyuGame";
    private const string Repo = "Haiyu";
    private Tuple<GithubResponseModel?, DateTime>? _cacheInfo;

    private async Task<GithubResponseModel?> GetInfoAsync(CancellationToken token = default)
    {
        var resourceUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases";
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0"
                );
                var response = await client.GetAsync(resourceUrl, token);
                var results = await response.Content.ReadFromJsonAsync(
                    JsonContext.Default.ListGithubResponseModel,
                    cancellationToken: token
                );
                if (results.Count > 0)
                {
                    return results.FirstOrDefault();
                }
                else
                {
                    return null;
                }
            }
        }
        catch (OperationCanceledException cancel)
        {
            throw cancel;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> CheckProgramUpdateAsync(
        string currentVersion,
        CancellationToken token = default
    )
    {
        await RefreshDownloadInfo(token);
        if (currentVersion == null)
        {
            //刷新版本，但不提示更新
            return false;
        }
        if (_cacheInfo == null || _cacheInfo.Item1 == null || _cacheInfo.Item1.Assets == null || _cacheInfo.Item1.Assets.Count == 0)
        {
            return false;
        }
        var currentV = currentVersion.ParseVerision();
        var serverV = _cacheInfo.Item1?.TagName.ParseVerision();
        if (currentV < serverV)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    async Task RefreshDownloadInfo(CancellationToken token = default)
    {
        var info = await GetInfoAsync(token);
        this._cacheInfo = new Tuple<GithubResponseModel?, DateTime>(info, DateTime.Now);
    }

    public async Task<string?> DownloadProgramInfoAsync(
        IProgress<double> progress,
        CancellationToken token = default
    )
    {
        try
        {
            if (_cacheInfo == null || DateTime.Now - _cacheInfo.Item2 > TimeSpan.FromMinutes(5))
            {
                await RefreshDownloadInfo(token);
            }

            if (
                _cacheInfo == null
                || _cacheInfo.Item1 == null
                || _cacheInfo.Item1.Assets == null
                || _cacheInfo.Item1.Assets.Count == 0
            )
            {
                return null;
            }
            var url = _cacheInfo?.Item1?.Assets.FirstOrDefault()?.BrowserDownloadUrl;
            if (url == null)
            {
                return null;
            }
            var downloadPath = Path.Combine(
                Path.GetTempPath(),
                System.IO.Path.GetFileName(url)+".exe"
            );
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0"
                );
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    token
                ).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                long downLoadLength = 0;
                var totalLength = response.Content.Headers.ContentLength;
                double lastReportedProgress = 0;

                using (
                    var fs = new FileStream(
                        downloadPath,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.Read,
                        BufferSize,
                        true
                    )
                )
                {
                    var byteShard = ArrayPool<byte>.Shared;
                    var buffer = byteShard.Rent(BufferSize);
                    try
                    {
                        await using var responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                        while (true)
                        {
                            var read = await responseStream.ReadAsync(buffer.AsMemory(0, BufferSize), token).ConfigureAwait(false);
                            downLoadLength += read;
                            if (read == 0)
                            {
                                break;
                            }
                            await fs.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);

                            if (totalLength is > 0)
                            {
                                var radio = (double)downLoadLength / totalLength.Value * 100;
                                if (radio - lastReportedProgress >= 1 || downLoadLength == totalLength.Value)
                                {
                                    lastReportedProgress = radio;
                                    progress.Report(radio);
                                }
                            }
                        }
                    }
                    finally
                    {
                        byteShard.Return(buffer);
                    }
                }
            }
            return downloadPath;
        }
        catch (OperationCanceledException cancel)
        {
            throw cancel;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<DisplayVersionInfo?> GetLasterProgramInfoAsync(
        CancellationToken token = default
    )
    {
        if (_cacheInfo == null)
        {
            var info = await GetInfoAsync(token);

            if (info == null || info.Assets == null || info.Assets.Count == 0)
            {
                return null;
            }
            return new DisplayVersionInfo()
            {
                DownloadLink = info.Assets.FirstOrDefault()!.BrowserDownloadUrl,
                Version = info.TagName.ParseVerision().ToString(),
                HelpLink = "https://github.com/HaiyuGame/Haiyu/releases/",
                Size = info.Assets.FirstOrDefault()!.Size,
                UpdateAt = info.PublishedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            };
        }
        else
        {
            if (
                (
                    _cacheInfo.Item1 == null
                    || _cacheInfo.Item1.Assets == null
                    || _cacheInfo.Item1.Assets.Count == 0
                )
            )
            {
                return null;
            }
            if (DateTime.Now - _cacheInfo.Item2 > TimeSpan.FromMinutes(5))
            {
                await RefreshDownloadInfo(token);
            }
            return new DisplayVersionInfo()
            {
                DownloadLink = _cacheInfo.Item1.Assets.FirstOrDefault()!.BrowserDownloadUrl,
                Version = _cacheInfo.Item1.TagName.ParseVerision().ToString(),
                HelpLink = "https://github.com/HaiyuGame/Haiyu/releases/",
                Size = _cacheInfo.Item1.Assets.FirstOrDefault()!.Size,
                UpdateAt = _cacheInfo.Item1.PublishedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            };
        }
    }

    public Task StartInstallProgramAsync()
    {
        throw new NotImplementedException();
    }
}
