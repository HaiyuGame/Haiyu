
namespace Haiyu.Converter;

public sealed partial class LocalUserBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value is bool b)
        {
            if(b)
            {
                return new Thickness(1);
            }
            else
            {
            }
        }
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
