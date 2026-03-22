namespace Waves.Core.Models;

/// <summary>
/// 处理步骤
/// </summary>
public interface IProgressSetup
{
    /// <summary>
    /// 进度名称
    /// </summary>
    public string ProgressName { get; set; }

    /// <summary>
    /// 进度Value
    /// </summary>
    public double ProgressValue { get; set; }


}
