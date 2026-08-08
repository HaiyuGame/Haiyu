using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Haiyu.KuroClient.Helper
{
    public static class PhoneHelper
    {

        public static bool IsMobile(this string mobile)
        {
            if (string.IsNullOrEmpty(mobile))
                return false;
            return Regex.IsMatch(mobile, @"^(1)[3-9]\d{9}$");
        }
    }
}
