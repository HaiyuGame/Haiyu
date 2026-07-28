using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Converter;

public partial class KuroBBSCoinCompletedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value is double d && d == 1)
        {
            return GetString("KuroCoin_Process_On")!;
        }
        return GetString("KuroCoin_Process_Off")!; ;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value;
    }

    public string? GetString(string key) => LanguageService.GetString(key);
}
