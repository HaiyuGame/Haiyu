using System.Security.Principal;

namespace Haiyu.ServiceHost;

public static class SystemHelper
{
    /// <summary>
    /// 管理员模式启动
    /// </summary>
    /// <returns></returns>
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
