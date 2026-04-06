namespace Waves.Core.Models;

/// <summary>
/// 更新下载文件夹配置
/// </summary>
public class DownloadUpdateFolderConfig
{
    public string PatchFolder { get; set; }

    public string PatchGroupFolder { get; set; }

    public string ZipFolder { get; set; }

    public string ResourceFolder { get; set;  }
    public string DownloadFolder { get; internal set; }
}
