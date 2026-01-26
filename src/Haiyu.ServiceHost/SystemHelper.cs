using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Haiyu.ServiceHost;

public static class SystemHelper
{
    
}

public static class AdminPrivilegeHelper
{
    // 导入Windows API相关函数（修正CloseHandle的DLL归属）
    #region Windows API 声明
    /// <summary>
    /// 关闭一个打开的对象句柄（核心修正：从kernel32.dll导入）
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// 获取当前进程的访问令牌
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    /// <summary>
    /// 检查令牌是否属于指定的SID组
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CheckTokenMembership(IntPtr TokenHandle, IntPtr SidToCheck, out bool IsMember);

    /// <summary>
    /// 创建已知SID的实例
    /// </summary>
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateWellKnownSid(WellKnownSidType WellKnownSid, IntPtr DomainSid, IntPtr pSid, ref uint cbSid);
    #endregion
    private enum WellKnownSidType
    {
        WinBuiltinAdministratorsSid = 26
    }

    public static bool IsRunningAsAdministrator()
    {
        try
        {
            uint sidSize = 0;
            CreateWellKnownSid(WellKnownSidType.WinBuiltinAdministratorsSid, IntPtr.Zero, IntPtr.Zero, ref sidSize);

            // 分配内存存储SID
            IntPtr sidPtr = Marshal.AllocHGlobal((int)sidSize);
            try
            {
                bool createSidSuccess = CreateWellKnownSid(
                    WellKnownSidType.WinBuiltinAdministratorsSid,
                    IntPtr.Zero,
                    sidPtr,
                    ref sidSize);

                if (!createSidSuccess)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                bool isMember;
                bool checkSuccess = CheckTokenMembership(IntPtr.Zero, sidPtr, out isMember);

                if (!checkSuccess)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return isMember;
            }
            finally
            {
                Marshal.FreeHGlobal(sidPtr);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"权限判断出错：{ex.Message}");
            return false;
        }
    }

    // 测试示例
    public static void Main()
    {
        bool isAdmin = IsRunningAsAdministrator();
        Console.WriteLine($"当前程序是否以管理员权限运行：{isAdmin}");
        Console.ReadLine();
    }
}