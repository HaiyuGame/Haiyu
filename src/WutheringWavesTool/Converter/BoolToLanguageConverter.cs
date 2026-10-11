using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Converter;

public partial class BoolToLanguageConverter : IValueConverter
{
    public string TrueTextKey { get; set; }
    public string FalseTextKey { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b && b)
        {
            return LanguageService.GetString(TrueTextKey);
        }
        return LanguageService.GetString(FalseTextKey);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && s == TrueTextKey)
        {
            return true;
        }
        return false;
    }
}
