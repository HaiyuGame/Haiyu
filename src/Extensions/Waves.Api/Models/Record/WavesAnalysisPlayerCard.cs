
using System.Diagnostics.CodeAnalysis;
using MemoryPack;
using Waves.Api.Models.Wrappers;

namespace Waves.Api.Models.Record;

[MemoryPackable]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.PublicProperties
    | DynamicallyAccessedMemberTypes.PublicMethods
)]
public partial class WavesAnalysisPlayerCard
{
    public DateTime LastUpdater { get; set; }

    public string SessionId { get; set; }

    public IList<WavesAnalysisPlayerCardItem> Items { get; set; }

    /// <summary>
    /// 更新源，Cloud为账号名称，GameContext为日志输入并匹配游戏ID
    /// </summary>
    public string UpdateSource { get; set;  }

    /// <summary>
    /// 更新类型：GameContext、CloudGame
    /// </summary>
    public string UpdateType { get; set;  }
}

[MemoryPackable]
[DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.PublicProperties
    | DynamicallyAccessedMemberTypes.PublicMethods
)]
public partial class WavesAnalysisPlayerCardItem
{
    public int PoolType { get; set; }

    public IEnumerable<RecordCardItemWrapper> Resource { get; set; }

}
