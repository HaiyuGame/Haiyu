namespace Waves.Core.Models.Options;

/// <summary>
/// 下载安装模式
/// </summary>
public class InstallOption
{
    public bool IsProd { get; set; } = false;

    public bool IsAdvance { get; set; } = false;

    /// <summary>
    /// 普通更新游戏
    /// </summary>
    /// <returns></returns>
    public static InstallOption CreateDefault()
    {
        return new();
    }

    /// <summary>
    /// 预下载模式
    /// </summary>
    /// <returns></returns>
    public static InstallOption CreateProdownlad()
    {
        return new()
        {
            IsProd = true,
            IsAdvance = false
        };
    }

    /// <summary>
    /// 提前安装模式
    /// </summary>
    /// <returns></returns>
    public static InstallOption CreateAdvance()
    {
        return new()
        {
            IsProd = false,
            IsAdvance = true
        };
    }
}
