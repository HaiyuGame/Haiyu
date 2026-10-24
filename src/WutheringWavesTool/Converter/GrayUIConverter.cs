using GrayExtension;

namespace Haiyu.Converter;

/// <summary>
/// 灰度UI显示器
/// </summary>
public partial class GrayUIToVisiblityConverter : IValueConverter
{
    public static GrayUIToVisiblityConverter Instance => new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var grayConfig = Haiyu.Instance.Host.Services.GetRequiredService<GrayOpenService>();
        if (value is uint str)
        {
            var flage = grayConfig.GetOpen(str, false);
            return flage ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
