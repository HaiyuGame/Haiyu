namespace Waves.Core.Models;

/// <summary>
/// 灰度测试Id
/// </summary>
public enum GrayConfigId : uint
{
    /// <summary>
    /// 差分补丁算法V2，输入1000
    /// </summary>
    CoreDiffV2 = 1000u,

    /// <summary>
    /// 声骇评分功能，输入1001
    /// </summary>
    SoringTesing = 1001u,

    /// <summary>
    /// 材质分包支持，输入1002
    /// </summary>
    WavesGameBunleOpen = 1002u,

    /// <summary>
    /// 官方启动器支持回写开关，输入1003
    /// </summary>
    OfficialLauncheWrite = 1003u,

    /// <summary>
    /// 自定义启动路径，输入1004
    /// </summary>
    CustomLauncheExeMod = 1004,

    /// <summary>
    /// 抽卡记录V3
    /// </summary>
    WavesRecordManager = 1005,
}

public partial class GrayConfigItem : ObservableObject
{
    [ObservableProperty]
    public partial GrayConfigId Id { get; set; }

    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public static ObservableCollection<GrayConfigItem> GetItems =>
        [
            .. GrayConfigIdExtensions.GetIds.Select(x => new GrayConfigItem()
            {
                DisplayName = x.ToString(),
                Id = x,
            }),
        ];
}

internal static class GrayConfigIdExtensions
{
    internal static GrayConfigId[] GetIds => Enum.GetValues<GrayConfigId>();
}
