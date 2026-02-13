using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Waves.Core;
namespace Waves.Core.Helpers;

public class HardwareIdGenerator
{
    // ==================== API 声明 ====================

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // ==================== 常量 ====================
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 1;
    private const uint FILE_SHARE_WRITE = 2;
    private const uint OPEN_EXISTING = 3;
    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public uint PropertyId;
        public uint QueryType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct STORAGE_DEVICE_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        public byte DeviceType;
        public byte DeviceTypeModifier;
        public bool RemovableMedia;
        public bool CommandQueueing;
        public uint VendorIdOffset;
        public uint ProductIdOffset;
        public uint ProductRevisionOffset;
        public uint SerialNumberOffset;
        public uint BusType;
        public uint RawPropertiesLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] RawDeviceProperties;
    }

    /// <summary>
    /// 生成基于本机的唯一硬件ID V1版本
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string GenerateUniqueId()
    {
        try
        {
            string diskSerial = GetHardDiskSerial();
            string cpuId = GetCpuId();

            string combined = $"{diskSerial}|{cpuId}";

            using (var sha1 = SHA1.Create())
            {
                byte[] hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(combined));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("X2"));
                }
                return sb.ToString();
            }
        }
        catch
        {
            return "UNKNOWN_HARDWARE_ID";
        }
    }

    /// <summary>
    /// 生成模拟硬件ID V2版本
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string GenerateUniqueIdV2(int length = 40)
    {
        if (length <= 0 || length % 2 != 0)
        {
            throw new ArgumentException("字符串长度必须是正偶数", nameof(length));
        }
        int byteCount = length / 2;
        byte[] randomBytes = new byte[byteCount];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        long timeTicks = DateTime.UtcNow.Ticks;
        byte[] timeBytes = BitConverter.GetBytes(timeTicks);

        for (int i = 0; i < Math.Min(timeBytes.Length, randomBytes.Length); i++)
        {
            randomBytes[i] ^= timeBytes[i];
        }
        StringBuilder sb = new StringBuilder(length);
        foreach (byte b in randomBytes)
        {
            sb.Append(b.ToString("X2"));
        }
        return sb.ToString().ToUpper();
    }

    private static string GetHardDiskSerial()
    {
        IntPtr hDevice = CreateFile(
            @"\\.\PhysicalDrive0",
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (hDevice == IntPtr.Zero || hDevice.ToInt64() == -1)
            return "NO_DISK_ACCESS";

        try
        {
            STORAGE_PROPERTY_QUERY query = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = 0, // StorageDeviceProperty
                QueryType = 0
            };

            int querySize = Marshal.SizeOf(query);
            IntPtr queryPtr = Marshal.AllocHGlobal(querySize);
            Marshal.StructureToPtr(query, queryPtr, false);

            int bufferSize = 1024;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            uint bytesReturned;

            bool result = DeviceIoControl(
                hDevice,
                IOCTL_STORAGE_QUERY_PROPERTY,
                queryPtr,
                (uint)querySize,
                buffer,
                (uint)bufferSize,
                out bytesReturned,
                IntPtr.Zero);

            if (result)
            {
                STORAGE_DEVICE_DESCRIPTOR deviceDesc = (STORAGE_DEVICE_DESCRIPTOR)Marshal.PtrToStructure(buffer, typeof(STORAGE_DEVICE_DESCRIPTOR));
                if (deviceDesc.SerialNumberOffset > 0)
                {
                    IntPtr serialPtr = IntPtr.Add(buffer, (int)deviceDesc.SerialNumberOffset);
                    string serial = Marshal.PtrToStringAnsi(serialPtr);
                    return serial?.Trim();
                }
            }
        }
        finally
        {
            CloseHandle(hDevice);
        }

        return "UNKNOWN_SERIAL";
    }

    private static string GetCpuId()
    {
        return Environment.ProcessorCount.ToString();
    }
}