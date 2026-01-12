using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Haiyu.ServiceHost.XBox.helpers
{
    public static class DeadZoneCorrectedExtension
    {
        public static float DeadZoneCorrected(this float value, float deadZone)
        {
            return (Math.Abs(value) > deadZone) ? value : 0.0f;
        }

        public static Vector2 DeadZoneCorrected(this Vector2 vector, float deadZone)
        {
            return new Vector2(vector.X.DeadZoneCorrected(deadZone), vector.Y.DeadZoneCorrected(deadZone));
        }
    }
}
