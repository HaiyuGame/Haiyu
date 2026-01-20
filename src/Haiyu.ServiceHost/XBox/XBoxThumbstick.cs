using System;
using System.Numerics;
using Haiyu.ServiceHost.XBox.helpers;

namespace Haiyu.ServiceHost.XBox
{
    public class XBoxThumbstick : XBoxComponent<Vector2>
    {
        public float DeadZone { get; set; } = 0.0f;

        public override Vector2 Value
        {
            get => base.Value;
            internal set => base.Value = value.DeadZoneCorrected(DeadZone);
        }

        public XBoxThumbstick(float deadZone = 0.0f, float initialX = 0.0f, float initialY = 0.0f) : base(new Vector2(initialX, initialY))
        {
            DeadZone = deadZone;
        }

        internal void SetValue(float x, float y)
        {
            Value = new Vector2(x, y);
        }
    }
}
