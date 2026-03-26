using Waves.Core.Contracts.Events;

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
    public double ProgressValue { get;}

    /// <summary>
    /// 是否允许暂停
    /// </summary>
    public bool CanPause { get; }  

    /// <summary>
    /// 是否允许停止
    /// </summary>
    public bool CanStop { get;  }
    /// <summary>
    /// 开始执行
    /// </summary>
    /// <param name="isSync">是否同步执行</param>
    /// <returns></returns>
    public Task<object?> ExecuteAsync(bool isSync = false);

    /// <summary>
    /// 设置参数
    /// </summary>
    /// <param name="param"></param>
    /// <param name="gameEventPublisher"></param>
    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher);
}
