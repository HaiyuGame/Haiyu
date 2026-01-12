using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haiyu.ServiceHost.XBox
{
    public class XBoxRumble : XBoxComponent<float>
    {
        public const int INFINITE = -1;

        public XBoxRumble(float initialValue = 0.0f) : base(initialValue) { }

        public void Rumble(float value, int timeInMilliseconds = INFINITE)
        {
            Value = value;
            if (timeInMilliseconds > INFINITE)
            {
                Task.Delay(timeInMilliseconds).ContinueWith(t => Value = 0.0f);
            }
        }
    }
}
