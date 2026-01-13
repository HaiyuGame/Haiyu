using Waves.Core.Adaptives;
using Waves.Core.Common;

namespace Waves.Core.Settings;

/// <summary>
/// Xbox 配置信息
/// </summary>
public class XBoxConfig : SettingBase
{
    private static readonly string XboxConfigFilelPath = Path.Combine(
        AppSettings.BassFolder,
        "xbox.json"
    );

    public XBoxConfig()
        : base(XboxConfigFilelPath) { }

    /// <summary>
    /// 是否启用模拟鼠标控制
    /// </summary>
    public bool IsEnable
    {
        get => BoolAdaptive.Instance.GetForward(Read());
        set => Write(BoolAdaptive.Instance.GetBack(value));
    }

    /// <summary>
    /// A 键映射
    /// </summary>
    public string? A
    {
        get => Read();
        set => Write(value);
    }

    /// <summary>
    /// B 键映射
    /// </summary>
    public string? B
    {
        get => Read();
        set => Write(value);
    }

    /// <summary>
    /// X 键映射
    /// </summary>
    public string? X
    {
        get => Read();
        set => Write(value);
    }

    /// <summary>
    /// Y 键映射
    /// </summary>
    public string? Y
    {
        get => Read();
        set => Write(value);
    }

    /// <summary>
    /// 左键映射
    /// </summary>
    public string? Left
    {
        get => Read();
        set => Write(value);
    }
    /// <summary>
    /// 上键映射
    /// </summary>
    public string? Top
    {
        get => Read();
        set => Write(value);
    }

    /// <summary>
    /// 右键映射
    /// </summary>
    public string? Right
    {
        get => Read();
        set => Write(value);
    }

    /// <summary>
    /// 下键映射
    /// </summary>
    public string? Bottom
    {
        get => Read();
        set => Write(value);
    }
}
