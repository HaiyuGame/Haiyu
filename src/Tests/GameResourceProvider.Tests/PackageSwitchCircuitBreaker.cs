using Waves.Core.Contracts;

namespace Project.Test;

internal sealed class PackageSwitchCircuitBreaker : IIoCircuitBreaker
{
    public bool TryAcquire() => true;
    public void Release() { }
}
