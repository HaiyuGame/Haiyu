using Haiyu.ServiceHost.XBox.helpers;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ServiceHost.XBox.Commons
{

    public class XBoxController
    {
        private Task fastPollTask;
        private Task slowPollTask;

        public const float MinTrigger = 0.0f;
        public const float MaxTrigger = 1.0f;

        public const float MinThumb = -1.0f;
        public const float MaxThumb = 1.0f;

        public const float MinRumble = 0.0f;
        public const float MaxRumble = 1.0f;

        private Controller controller;
        private Vibration vibration = new Vibration();

        //Buttons
        public XBoxButton A { get; } = new XBoxButton();
        public XBoxButton B { get; } = new XBoxButton();
        public XBoxButton X { get; } = new XBoxButton();
        public XBoxButton Y { get; } = new XBoxButton();

        public XBoxButton LeftShoulder { get; } = new XBoxButton();
        public XBoxButton RightShoulder { get; } = new XBoxButton();

        public XBoxButton Start { get; } = new XBoxButton();
        public XBoxButton Back { get; } = new XBoxButton();

        //D-pad
        public XBoxButton Up { get; } = new XBoxButton();
        public XBoxButton Down { get; } = new XBoxButton();
        public XBoxButton Left { get; } = new XBoxButton();
        public XBoxButton Right { get; } = new XBoxButton();

        //Thumb sticks
        public XBoxButton LeftThumbclick { get; } = new XBoxButton();
        public XBoxButton RightThumbclick { get; } = new XBoxButton();

        //Triggers
        public XBoxTrigger LeftTrigger { get; } = new XBoxTrigger(/*Gamepad.TriggerThreshold.RemapF(0, byte.MaxValue, 0.0f, TriggerMax)*/);
        public XBoxTrigger RightTrigger { get; } = new XBoxTrigger(/*Gamepad.TriggerThreshold.RemapF(0, byte.MaxValue, 0.0f, TriggerMax)*/);

        //Thumb sticks
        public XBoxThumbstick LeftThumbstick { get; } = new XBoxThumbstick(/*Gamepad.LeftThumbDeadZone.RemapF(0, short.MaxValue, 0.0f, ThumbMax)*/);
        public XBoxThumbstick RightThumbstick { get; } = new XBoxThumbstick(/*Gamepad.RightThumbDeadZone.RemapF(0, short.MaxValue, 0.0f, ThumbMax)*/);

        public XBoxBattery Battery { get; } = new XBoxBattery();

        public XBoxConnection Connection { get; } = new XBoxConnection();

        public XBoxRumble LeftRumble { get; } = new XBoxRumble();
        public XBoxRumble RightRumble { get; } = new XBoxRumble();

        public XBoxController(int fastPollIntervalMilliseconds = 10, int slowPollIntervalMilliseconds = 1000)
        {
            controller = new Controller(UserIndex.One);

            //Forward changed left rumble speed to the controller 
            LeftRumble.ValueChanged += (s, e) =>
            {
                if (!controller.IsConnected)
                    return;

                vibration.LeftMotorSpeed = (ushort)e.Value.RemapF(MinRumble, MaxRumble, ushort.MinValue, ushort.MaxValue);
                controller.SetVibration(vibration);
            };

            //Forward changed right rumble speed to the controller
            RightRumble.ValueChanged += (s, e) =>
            {
                if (!controller.IsConnected)
                    return;

                vibration.RightMotorSpeed = (ushort)e.Value.RemapF(MinRumble, MaxRumble, ushort.MinValue, ushort.MaxValue);
                controller.SetVibration(vibration);
            };

            //Fast poll loop
            fastPollTask = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(fastPollIntervalMilliseconds);
                    FastPoll();
                }
            });

            //Slow poll loop
            slowPollTask = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(slowPollIntervalMilliseconds);
                    SlowPoll();
                }
            });
        }

        /// <summary>
        /// The fast poll method to readout the button states, trigger positions, thumb stick states and positions.
        /// </summary>
        private void FastPoll()
        {
            if (!controller.IsConnected)
                return;

            var gamepad = controller.GetState().Gamepad;

            A.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
            B.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
            X.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
            Y.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);

            LeftShoulder.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
            RightShoulder.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);

            Start.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);
            Back.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);

            Up.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
            Down.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);
            Left.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);
            Right.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);

            LeftThumbclick.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
            RightThumbclick.Value = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);

            LeftTrigger.Value = gamepad.LeftTrigger.RemapF(byte.MinValue, byte.MaxValue, MinTrigger, MaxTrigger);
            RightTrigger.Value = gamepad.RightTrigger.RemapF(byte.MinValue, byte.MaxValue, MinTrigger, MaxTrigger);

            LeftThumbstick.SetValue(gamepad.LeftThumbX.RemapF(short.MinValue, short.MaxValue, MinThumb, MaxThumb),
                                       gamepad.LeftThumbY.RemapF(short.MinValue, short.MaxValue, MinThumb, MaxThumb));


            RightThumbstick.SetValue(gamepad.RightThumbX.RemapF(short.MinValue, short.MaxValue, MinThumb, MaxThumb),
                                       gamepad.RightThumbY.RemapF(short.MinValue, short.MaxValue, MinThumb, MaxThumb));
        }

        /// <summary>
        /// The slow poll method to readout the connection state and battery level.
        /// </summary>
        private void SlowPoll()
        {
            Connection.Value = controller.IsConnected;

            Battery.Value = controller.IsConnected ? (BatteryLevel)controller.GetBatteryInformation(BatteryDeviceType.Gamepad).BatteryLevel : BatteryLevel.Empty;
        }
    }
}
