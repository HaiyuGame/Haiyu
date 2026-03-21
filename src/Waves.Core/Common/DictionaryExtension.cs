using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Core.Common;

public static class DictionaryExtension
{
    extension(Dictionary<string,object> values)
    {
        /// <summary>
        /// 检查参数并转换类型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool CheckParam<T>(string key, out T? value)
        {
            if (values.TryGetValue(key, out var result))
            {
                value = (T)result;
                return true;
            }
            value = default;
            return false;
        }
    }
}
