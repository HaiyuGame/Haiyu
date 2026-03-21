using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Contracts.Events;

namespace Waves.Core.Common;

public static class DownloadExtension
{
    extension(Dictionary<string, object> values)
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

    extension(IGameEventPublisher publisher)
    {
        public async ValueTask PublisAsync(
            Models.Enums.GameContextActionType cdnSelect,
            string message
        )
        {
            publisher.Publish(
                new Models.GameContextOutputArgs() {  Type = cdnSelect, TipMessage = message }
            );
        }

        
    }
}
