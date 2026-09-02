using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Haiyu.ABI.Common.Navtive;

public partial class NativePrivilege
{
    private const int ERROR_NOT_ALL_ASSIGNED = 1300;

    public static void EnablePrivilege(string privilegeName)
    {
        if (!OpenProcessToken(
                GetCurrentProcess(),
                TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
                out var tokenHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            if (!LookupPrivilegeValue(
                    null,
                    privilegeName,
                    out var luid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var tokenPrivileges = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                }
            };

            if (!AdjustTokenPrivileges(
                    tokenHandle,
                    false,
                    ref tokenPrivileges,
                    0,
                    0,
                    0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var error = Marshal.GetLastWin32Error();

            if (error == ERROR_NOT_ALL_ASSIGNED)
            {
                throw new InvalidOperationException(
                    $"当前进程 Token 不包含权限：{privilegeName}");
            }
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }
}
