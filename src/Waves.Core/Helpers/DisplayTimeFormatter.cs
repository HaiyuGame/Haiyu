using System.Globalization;

namespace Waves.Core.Helpers;

public static class DisplayTimeFormatter
{
    public static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)value.TotalDays:D2}:{value.Hours:D2}:{value.Minutes:D2}:{value.Seconds:D2}"
        );
    }

    public static string FormatDate(DateTime value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string FormatDateTime(DateTime value)
    {
        return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
