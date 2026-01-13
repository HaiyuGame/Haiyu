using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Core.Adaptives
{
    public class NullBoolAdaptive : IDataAdaptive<bool?, string?>
    {
        public static NullBoolAdaptive Instance { get; } = new();

        public string? GetBack(bool? forward)
        {
            if(forward == null)
            {
                return null;
            }
            return forward.ToString();
        }

        public bool? GetForward(string? value)
        {
            if(value == null)
            {
                return null;
            }
            if(bool.TryParse(value,out var result))
            {
                return result;
            }
            return null;
        }
    }
}
