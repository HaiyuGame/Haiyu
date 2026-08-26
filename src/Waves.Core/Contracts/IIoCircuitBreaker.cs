namespace Waves.Core.Contracts;

/// <summary>
/// 控制并发 IO 任务；超过上限的新任务立即熔断退出。
/// </summary>
public interface IIoCircuitBreaker
{
    /// <summary>
    /// 检查是否可以获取一个 IO 任务许可；如果当前并发数超过上限，则返回 false。
    /// </summary>
    /// <returns></returns>
    bool TryAcquire();

    /// <summary>
    /// 释放
    /// </summary>
    void Release();
}
