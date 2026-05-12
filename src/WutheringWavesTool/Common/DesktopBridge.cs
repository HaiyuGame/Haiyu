using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common
{
    public static class DesktopBridge
    {
        const long APPMODEL_ERROR_NO_PACKAGE = 15700L;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

        /// <summary>
        /// 检查当前应用运行模式
        /// </summary>
        /// <returns></returns>
        public static bool IsRunningAsMsix()
        {
            int length = 0;
            StringBuilder sb = new StringBuilder(0);
            int result = GetCurrentPackageFullName(ref length, sb);
            sb = new StringBuilder(length);
            result = GetCurrentPackageFullName(ref length, sb);
            return result != APPMODEL_ERROR_NO_PACKAGE;
        }

    }
}
