using System.Drawing;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Common;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

public partial  class KuroGameContextBase
{

    public async Task<bool> StartDownloadProdGame(string downloadFolder)
    {
        await this.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.ProdDownloadFolderDone, "False");
        var currentVersion = await GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LocalGameVersion
        );
        var launcher  = await GetGameLauncherSourceAsync();
        PatchIndexGameResource? patch = null;
        var previous = launcher
            .Predownload.Config.PatchConfig.Where(x => x.Version == currentVersion)
            .FirstOrDefault();
        if (previous != null)
        {
            var cdnUrl =
                launcher
                    .ResourceDefault.CdnList.Where(x => x.P != 0)
                    .OrderBy(x => x.P)
                    .FirstOrDefault() ?? null;
            if (cdnUrl == null)
            {
                await CancelDownloadAsync();
                return false;
            }

            _downloadBaseUrl = cdnUrl.Url+ previous.BaseUrl;
            patch = await GetPatchGameResourceAsync(cdnUrl.Url + previous.IndexFile);
            _downloadCTS = new CancellationTokenSource();
            var count = patch.Resource.Where(x => x.Dest.EndsWith(".krpdiff"));
            var size = count.Sum(x => x.Size);
            _totalfileSize = size;
            _totalFileTotal = count.Count() - 1;
            _totalProgressTotal = 0;
            this._downloadState = new DownloadState();
            _downloadState.IsActive = true;
            await _downloadState.SetSpeedLimitAsync(this.SpeedValue);
            //启动预下载线程
            Task.Run(async () =>
                StartDownProdAsync(downloadFolder,patch,previous.Version));
            //保存预下载信息
            await this.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.ProdDownloadPath, downloadFolder);
            await this.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.ProdDownloadVersion, previous.Version);
        }
        else
        {
            Logger.WriteInfo("本地资源与网络版本不匹配，请直接尝试修复游戏！");
            await CancelDownloadAsync();
            return false;
        }

        return true;
    }

    private async Task StartDownProdAsync(string downloadFolder, PatchIndexGameResource patch, string version)
    {
        var downloadResult =  await this.DownloadGroupPatcheToResource(folder: downloadFolder, patch.Resource, ispred: true);
        if (!downloadResult)
        {
            Logger.WriteInfo($"预下载：下载差异组文件失败，请重新尝试");
            await SetNoneStatusAsync(true).ConfigureAwait(false);
            await UpdateFileProgress(
                    GameContextActionType.TipMessage,
                    0,
                    false,
                    true,
                    "预下载：下载差异组文件失败，请重新尝试"
                )
                .ConfigureAwait(false);
            return;
        }
        await this.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.ProdDownloadFolderDone, "True");

        _totalfileSize = 0;
        _totalFileTotal = 0;
        _totalProgressTotal = 0;
        _totalProgressSize = 0;
        _downloadState.IsActive = false;
        await this.SetNoneStatusAsync(true).ConfigureAwait(false);
    }
}
