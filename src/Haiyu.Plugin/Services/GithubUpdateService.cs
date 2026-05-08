using Haiyu.Plugin.Common;
using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Haiyu.Plugin.Services;

public class GithubUpdateService : IUpdateService
{
    private const string Owner="HaiyuGame";
    private const string Repo="Haiyu";
    private Tuple<GithubResponseModel?, DateTime>? _cacheInfo;

    private async Task<GithubResponseModel?> GetInfoAsync()
    {
        var resourceUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases";
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0");
                var response = await client.GetAsync(resourceUrl);
                var results = await response.Content.ReadFromJsonAsync(JsonContext.Default.ListGithubResponseModel);
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
        catch (Exception)
        {
            return null;
        }
            
    }

    public async Task<bool> CheckProgramUpdateAsync(string currentVersion)
    {
        var info = await GetInfoAsync();
        this._cacheInfo = new Tuple<GithubResponseModel?, DateTime>(info, DateTime.Now);
        var currentV = currentVersion.ParseVerision();
        var serverV = info?.TagName.ParseVerision();
        if (currentV < serverV)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public Task DownloadProgramInfoAsync(IProgress<double> progress)
    {
        throw new NotImplementedException();
    }

    public async Task<DisplayVersionInfo?> GetLasterProgramInfoAsync()
    {
        if(_cacheInfo == null)
        {
            var info = await GetInfoAsync();
            
            if(info == null ||info.Assets == null || info.Assets.Count == 0)
            {
                return null;
            }
            return new DisplayVersionInfo()
            {
                DownloadLink = info.Assets.FirstOrDefault()!.BrowserDownloadUrl,
                Version = info.TagName,
                HelpLink = "https://github.com/HaiyuGame/Haiyu/releases/",
                Size = info.Assets.FirstOrDefault()!.Size,
                UpdateAt = info.PublishedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        else
        {
            if (_cacheInfo.Item1 == null || _cacheInfo.Item1.Assets == null || _cacheInfo.Item1.Assets.Count == 0)
            {
                return null;
            }
            return new DisplayVersionInfo()
            {
                DownloadLink = _cacheInfo.Item1.Assets.FirstOrDefault()!.BrowserDownloadUrl,
                Version = _cacheInfo.Item1.TagName,
                HelpLink = "https://github.com/HaiyuGame/Haiyu/releases/",
                Size = _cacheInfo.Item1.Assets.FirstOrDefault()!.Size,
                UpdateAt = _cacheInfo.Item1.PublishedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }

    public Task StartInstallProgramAsync()
    {
        throw new NotImplementedException();
    }
}
