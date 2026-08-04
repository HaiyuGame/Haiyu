namespace Haiyu.Converter;

public partial class IntToSecondTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int v)
        {
            return DisplayTimeFormatter.FormatDuration(TimeSpan.FromSeconds(v));
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
