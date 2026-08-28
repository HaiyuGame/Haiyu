using System.ComponentModel;

namespace ABIRuntime.Runtime;

public static unsafe class TokenHelper
{
    public static bool IsElevated
    {
        get
        {
            if (!NativeMethods.OpenProcessToken(
                NativeMethods.GetCurrentProcess(), NativeMethods.TokenQuery, out nint token))
            {
                throw new Win32Exception();
            }

            try
            {
                NativeMethods.TokenElevationInfo elevation = default;
                if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenElevation,
                    &elevation, (uint)sizeof(NativeMethods.TokenElevationInfo), out _))
                {
                    throw new Win32Exception();
                }

                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }
    }
}
