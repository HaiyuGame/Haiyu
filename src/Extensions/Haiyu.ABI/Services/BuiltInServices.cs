using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using ABIRuntime.Abstractions;

namespace Haiyu.ABI.Services;


public static class BuiltInServices
{
    public static void Register(this IPrivilegedServiceRegistry registry)
    {
        registry.Add(new MemoryCleanerService());
        registry.Add(new ComputerMonitorService());
        registry.Add(new ABISystemConfigService());
    }
}

