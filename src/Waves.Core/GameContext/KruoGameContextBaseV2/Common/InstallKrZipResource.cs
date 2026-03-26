using System.IO.Compression;
using Waves.Core.Common;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 安装库洛解压报资源类
/// </summary>
public class InstallKrZipResource : IProgressSetup
{
    private List<ZipFileInfo> zipInfos;
    private string baseGamePath;
    private List<IndexResource> resourceInfos;
    private string zipDownFolder;

    public string ProgressName { get; set; }

    public double ProgressValue { get; private set; }

    public bool CanPause => true;

    public bool CanStop => true;

    public IGameEventPublisher GameEventPublisher { get; private set; }
    public Dictionary<string, object> Param { get; private set; }

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher)
    {
        this.GameEventPublisher = gameEventPublisher;
        this.Param = param;
    }

    public async Task<bool> CheckAsync()
    {
        if (!Param.CheckParam<List<ZipFileInfo>>("zipinfos",out var zipInfos))
        {
            return false;
        }
        if (!Param.CheckParam<List<IndexResource>>("resourceInfos", out var resourceInfos))
        {
            return false;
        }
        if (!Param.CheckParam<string>("zipPath", out var zipPath))
        {
            return false;
        }
        if (!Param.CheckParam<string>("baseGamePath", out var baseGamePath))
        {
            return false;
        }
        if(!Param.CheckParam<string>("zipDownFolder",out var zipDownFolder))
        {
            return false;
        }
        this.zipInfos = zipInfos!;
        this.baseGamePath = baseGamePath!;
        this.resourceInfos = resourceInfos!;
        this.zipDownFolder = zipDownFolder!;
        return true;
    }

    public async Task<bool> RunAsync()
    {
        if(!(await CheckAsync()))
        {
            this.GameEventPublisher.Publish(new GameContextOutputArgs()
            {
                Type = Models.Enums.GameContextActionType.TipMessage,
                TipMessage = "参数不正确，无法解压"
            });
            return false;
        }
        var zipTempFolder = Path.Combine(this.baseGamePath, "zipTemp");
        foreach (var item in this.zipInfos)
        {
            var zipInfoFile = Path.Combine(zipTempFolder,item.Dest);
            if (!File.Exists(zipInfoFile))
            {
                // 在此进行调试
                return false;
            }
            using var zipArchive = new ZipArchive(File.OpenRead(zipInfoFile));
            var fileSize = zipArchive.Entries.Sum(x => x.Length);
            //1. 增加解压工具方法
            //2. 接收解压数据信息
            //3. 确认无误后进行释放文件到临时目录
            //4. 直接开始合并目录，只需提醒一句即可，无需显示移动目录过程
            //foreach (var entries in zipArchive.Entries)
            //{
            //    var extractFilePath = Path.Combine(zipTempFolder, entries.FullName);
            //    using(var fs = await entries.OpenAsync()) 
            //    {

            //    }
            //} 
        }
        return true;
    }

    public Task<object?> ExecuteAsync(bool isSync = false)
    {
        throw new NotImplementedException();
    }
}
