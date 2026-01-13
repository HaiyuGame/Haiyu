namespace Waves.Core.Adaptives;

/// <summary>
/// 数据适配器接口
/// </summary>
/// <typeparam name="Forward"></typeparam>
/// <typeparam name="Back"></typeparam>
public interface IDataAdaptive<Forward, Back>
{
    /// <summary>
    /// 转换
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public Forward GetForward(Back value);

    /// <summary>
    /// 反向转换
    /// </summary>
    /// <param name="forward"></param>
    /// <returns></returns>
    public Back? GetBack(Forward forward);
}
