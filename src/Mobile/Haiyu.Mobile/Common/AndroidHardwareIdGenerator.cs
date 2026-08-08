using System.Security.Cryptography;
using System.Text;

namespace Haiyu.Mobile.Common;

public static class AndroidHardwareIdGenerator
{
    private static string? _cached;

    public static string GenerateUniqueId()
    {
        if (!string.IsNullOrEmpty(_cached))
            return _cached;

        try
        {
            var seed = BuildSeed();
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(seed));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("X2"));
            _cached = sb.ToString();
            return _cached;
        }
        catch
        {
            return "UNKNOWN_HARDWARE_ID";
        }
    }

    private static string BuildSeed()
    {
#if ANDROID
        var context = Android.App.Application.Context;
        var androidId =
            Android.Provider.Settings.Secure.GetString(
                context.ContentResolver,
                Android.Provider.Settings.Secure.AndroidId
            ) ?? string.Empty;

        var model = Android.OS.Build.Model ?? string.Empty;
        var manufacturer = Android.OS.Build.Manufacturer ?? string.Empty;
        var device = Android.OS.Build.Device ?? string.Empty;

        // 对应 Windows 的 diskSerial|cpuId 组合方式
        return $"{androidId}|{manufacturer}|{model}|{device}";
#else
        return $"fallback|{Environment.MachineName}|{Environment.ProcessorCount}";
#endif
    }
}
