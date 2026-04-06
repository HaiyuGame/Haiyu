namespace Waves.Core.Contracts.Events;

public interface IGameEventSubscription : IDisposable
{
    /// <summary>
    /// 订阅是否仍然活跃
    /// </summary>
    bool IsActive { get; }
}
