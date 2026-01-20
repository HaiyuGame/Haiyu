using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haiyu.ServiceHost.XBox.helpers
{
    public static class RemapExtension
    {
        public static float RemapF(this float value, float inMin, float inMax, float outMin, float outMax)
        {
            return Math.Min(outMax, Math.Max(outMin, (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin));
        }

        public static float RemapF(this byte value, float inMin, float inMax, float outMin, float outMax)
        {
            return ((float)value).RemapF(inMin, inMax, outMin, outMax);
        }

        public static float RemapF(this short value, float inMin, float inMax, float outMin, float outMax)
        {
            return ((float)value).RemapF(inMin, inMax, outMin, outMax);
        }

        public static float RemapF(this int value, float inMin, float inMax, float outMin, float outMax)
        {
            return ((float)value).RemapF(inMin, inMax, outMin, outMax);
        }
    }
}
