using System;
using System.Text.RegularExpressions;

namespace Haiyu.Plugin.Common;

public static partial class VersionParse
{
    [GeneratedRegex(@"\d+\.\d+\.\d+")]
    public static partial Regex VersionRegex();

    extension(string input)
    {
        /// <summary>
        /// 转换版本信息
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        public Version ParseVerision()
        {
            return VersionParse.VersionRegex().Match(input).Value switch
            {
                "" => throw new FormatException("NONE"),
                var version => Version.Parse(version)
            };
        }
    }
}
