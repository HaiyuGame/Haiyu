using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Converter;

public partial class BoolToSolidBrushColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value is bool b && b)
        {
            return new SolidColorBrush(Colors.Green);
        }
        return new SolidColorBrush(Colors.Red);
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
