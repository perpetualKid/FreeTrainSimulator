using System;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Input
{
    public class RailDriverInputGameComponent : GameComponent
    {
        private const int KeyPressShift = 8;
        private const int KeyDownShift = 13;
        private const int KeyUpShift = 17;

        private byte[] readBuffer;
        private byte[] readBufferHistory;

        private readonly RailDriverDevice railDriverInstance;

        private float directionPercent;      // -100 (reverse) to 100 (forward)
        private float throttlePercent;       // 0 to 100
        private float dynamicBrakePercent;   // 0 to 100 if active otherwise less than 0
        private float trainBrakePercent;     // 0 (release) to 100 (CS), does not include emergency
        private float engineBrakePercent;    // 0 to 100
        private bool bailOff;                // true when bail off pressed
        private bool emergency;              // true when train brake handle in emergency or E-stop button pressed
        private int Wipers;                  // wiper rotary, 1 off, 2 slow, 3 full
        private int Lights;                  // lights rotary, 1 off, 2 dim, 3 full

        private readonly (byte, byte, byte) reverser, dynamicBrake, wipers, headlight;
        private readonly (byte, byte) throttle, autoBrake, independentBrake, emergencyBrake, bailoffDisengaged, bailoffEngaged;
        private readonly bool fullRangeThrottle;

        private Action<int, GameTime, KeyEventType> keyActionHandler;
        private Action<RailDriverHandleEventType, GameTime, UserCommandArgs> handleActionHandler;

        public bool Active { get; private set; }

        public RailDriverInputGameComponent(Game game, EnumArray<byte, RailDriverCalibrationSetting> calibrationSettings) : base(game)
        {
            railDriverInstance = RailDriverDevice.Instance;
            Enabled = railDriverInstance.Enabled;
            readBuffer = railDriverInstance.GetReadBuffer();
            readBufferHistory = railDriverInstance.GetReadBuffer();

            if (null == calibrationSettings)
                calibrationSettings = RailDriverDevice.DefaultCalibrationSettings;

            if (Enabled)
            {
                byte cutOff = calibrationSettings[RailDriverCalibrationSetting.CutOffDelta];

                if (Convert.ToBoolean(calibrationSettings[RailDriverCalibrationSetting.ReverseReverser]))
                    reverser = (calibrationSettings[RailDriverCalibrationSetting.ReverserFullForward], calibrationSettings[RailDriverCalibrationSetting.ReverserNeutral], calibrationSettings[RailDriverCalibrationSetting.ReverserFullReversed]);
                else
                    reverser = (calibrationSettings[RailDriverCalibrationSetting.ReverserFullReversed], calibrationSettings[RailDriverCalibrationSetting.ReverserNeutral], calibrationSettings[RailDriverCalibrationSetting.ReverserFullForward]);
                reverser = UpdateCutOff(reverser, cutOff);

                if (Convert.ToBoolean(calibrationSettings[RailDriverCalibrationSetting.FullRangeThrottle]))
                {
                    if (Convert.ToBoolean(calibrationSettings[RailDriverCalibrationSetting.ReverseThrottle]))
                        throttle = (calibrationSettings[RailDriverCalibrationSetting.DynamicBrake], calibrationSettings[RailDriverCalibrationSetting.ThrottleFull]);
                    else
                        throttle = (calibrationSettings[RailDriverCalibrationSetting.ThrottleFull], calibrationSettings[RailDriverCalibrationSetting.DynamicBrake]);
                    fullRangeThrottle = true;
                }
                else
                {
                    if (Convert.ToBoolean(calibrationSettings[RailDriverCalibrationSetting.ReverseThrottle]))
                    {
                        throttle = (calibrationSettings[RailDriverCalibrationSetting.DynamicBrake], calibrationSettings[RailDriverCalibrationSetting.DynamicBrakeSetup]);
                        dynamicBrake = (calibrationSettings[RailDriverCalibrationSetting.DynamicBrakeSetup], calibrationSettings[RailDriverCalibrationSetting.ThrottleIdle], calibrationSettings[RailDriverCalibrationSetting.ThrottleFull]);
                    }
                    else
                    {
                        throttle = (calibrationSettings[RailDriverCalibrationSetting.ThrottleIdle], calibrationSettings[RailDriverCalibrationSetting.ThrottleFull]);
                        dynamicBrake = (calibrationSettings[RailDriverCalibrationSetting.ThrottleIdle], calibrationSettings[RailDriverCalibrationSetting.DynamicBrakeSetup], calibrationSettings[RailDriverCalibrationSetting.DynamicBrake]);
                    }
                }
                throttle = UpdateCutOff(throttle, cutOff);
                dynamicBrake = UpdateCutOff(dynamicBrake, cutOff);

                if (Convert.ToBoolean(calibrationSettings[RailDriverCalibrationSetting.ReverseAutoBrake]))
                    autoBrake = (calibrationSettings[RailDriverCalibrationSetting.AutoBrakeFull], calibrationSettings[RailDriverCalibrationSetting.AutoBrakeRelease]);
                else
                    autoBrake = (calibrationSettings[RailDriverCalibrationSetting.AutoBrakeRelease], calibrationSettings[RailDriverCalibrationSetting.AutoBrakeFull]);
                if (Convert.ToBoolean(calibrationSettings[RailDriverCalibrationSetting.ReverseIndependentBrake]))
                    independentBrake = (calibrationSettings[RailDriverCalibrationSetting.IndependentBrakeFull], calibrationSettings[RailDriverCalibrationSetting.IndependentBrakeRelease]);
                else
                    independentBrake = (calibrationSettings[RailDriverCalibrationSetting.IndependentBrakeRelease], calibrationSettings[RailDriverCalibrationSetting.IndependentBrakeFull]);
                autoBrake = UpdateCutOff(autoBrake, cutOff);
                independentBrake = UpdateCutOff(independentBrake, cutOff);

                emergencyBrake = (calibrationSettings[RailDriverCalibrationSetting.AutoBrakeFull], calibrationSettings[RailDriverCalibrationSetting.EmergencyBrake]);
                emergencyBrake = UpdateCutOff(emergencyBrake, cutOff);

                wipers = (calibrationSettings[RailDriverCalibrationSetting.Rotary1Position1], calibrationSettings[RailDriverCalibrationSetting.Rotary1Position2], calibrationSettings[RailDriverCalibrationSetting.Rotary1Position3]);
                headlight = (calibrationSettings[RailDriverCalibrationSetting.Rotary2Position1], calibrationSettings[RailDriverCalibrationSetting.Rotary2Position2], calibrationSettings[RailDriverCalibrationSetting.Rotary2Position3]);

                bailoffDisengaged = (calibrationSettings[RailDriverCalibrationSetting.BailOffDisengagedRelease], calibrationSettings[RailDriverCalibrationSetting.BailOffDisengagedFull]);
                bailoffEngaged = (calibrationSettings[RailDriverCalibrationSetting.BailOffEngagedRelease], calibrationSettings[RailDriverCalibrationSetting.BailOffEngagedFull]);
                bailoffDisengaged = UpdateCutOff(bailoffDisengaged, cutOff);
                bailoffEngaged = UpdateCutOff(bailoffEngaged, cutOff);

                railDriverInstance.SetLeds(RailDriverDisplaySign.Hyphen, RailDriverDisplaySign.Hyphen, RailDriverDisplaySign.Hyphen);
            }
        }

        protected override void OnEnabledChanged(object sender, EventArgs args)
        {
            if (!railDriverInstance.Enabled)
                Enabled = false;
            if (!Enabled && railDriverInstance.Enabled)
            {
                railDriverInstance?.ClearDisplay();
                railDriverInstance?.Shutdown();
            }
            base.OnEnabledChanged(sender, args);
        }

        public void AddInputHandler(Action<int, GameTime, KeyEventType> inputAction)
        {
            keyActionHandler += inputAction;
        }

        public void AddInputHandler(Action<RailDriverHandleEventType, GameTime, UserCommandArgs> inputAction)
        {
            handleActionHandler += inputAction;
        }

        public static int KeyEventCode(byte command, KeyEventType keyEventType)
        {
            switch (keyEventType)
            {
                case KeyEventType.KeyDown:
                    return command << KeyDownShift ^ KeyDownShift;
                case KeyEventType.KeyPressed:
                    return command << KeyPressShift ^ KeyPressShift;
                case KeyEventType.KeyReleased:
                    return command << KeyUpShift ^ KeyUpShift;
                default:
                    throw new NotSupportedException();
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!Game.IsActive || !Active)
                return;

            (readBufferHistory, readBuffer) = (readBuffer, readBufferHistory);

            if (0 == railDriverInstance.ReadCurrentData(ref readBuffer))
            {
                // direction
                float handle = directionPercent;
                directionPercent = Percentage(readBuffer[1], reverser);
                if (directionPercent != handle)
                    handleActionHandler?.Invoke(RailDriverHandleEventType.Direction, gameTime, new UserCommandArgs<float>() { Value = directionPercent });
                // throttle
                handle = throttlePercent;
                throttlePercent = Percentage(readBuffer[2], throttle);
                if (throttlePercent != handle)
                    handleActionHandler?.Invoke(RailDriverHandleEventType.Throttle, gameTime, new UserCommandArgs<float>() { Value = throttlePercent });
                // dyanmic brake
                if (!fullRangeThrottle)
                {
                    handle = dynamicBrakePercent;
                    dynamicBrakePercent = Percentage(readBuffer[2], dynamicBrake);
                    if (dynamicBrakePercent != handle)
                        handleActionHandler?.Invoke(RailDriverHandleEventType.DynamicBrake, gameTime, new UserCommandArgs<float>() { Value = dynamicBrakePercent });
                }
                // train brake
                handle = trainBrakePercent;
                trainBrakePercent = Percentage(readBuffer[3], autoBrake);
                if (trainBrakePercent != handle)
                    handleActionHandler?.Invoke(RailDriverHandleEventType.TrainBrake, gameTime, new UserCommandArgs<float>() { Value = trainBrakePercent });
                //engine brake
                handle = engineBrakePercent;
                engineBrakePercent = Percentage(readBuffer[4], independentBrake);
                if (engineBrakePercent != handle)
                    handleActionHandler?.Invoke(RailDriverHandleEventType.EngineBrake, gameTime, new UserCommandArgs<float>() { Value = engineBrakePercent });
                //bail-off
                float a = .01f * engineBrakePercent;
                float calOff = (1 - a) * bailoffDisengaged.Item1 + a * bailoffDisengaged.Item2;
                float calOn = (1 - a) * bailoffEngaged.Item1 + a * bailoffEngaged.Item2;
                bool buttonPress = bailOff;
                bailOff = Percentage(readBuffer[5], calOff, calOn) > 80;
                if (bailOff != buttonPress)
                    handleActionHandler?.Invoke(RailDriverHandleEventType.BailOff, gameTime, new UserCommandArgs<bool>() { Value = bailOff });
                // emergency brake
                buttonPress = emergency;
                emergency = trainBrakePercent >= 100 && Percentage(readBuffer[3], emergencyBrake) > 50;
                if (emergency != buttonPress)
                    handleActionHandler?.Invoke(RailDriverHandleEventType.Emergency, gameTime, new UserCommandArgs<bool>() { Value = emergency });

                int rotaryInput = Wipers;
                Wipers = (int)(.01 * Percentage(readBuffer[6], wipers) + 2.5);
                if (Wipers != rotaryInput)
                    handleActionHandler?.Invoke(RailDriverHandleEventType.Wipers, gameTime, new UserCommandArgs<int>() { Value = Wipers });
                rotaryInput = Lights;
                Lights = (int)(.01 * Percentage(readBuffer[7], headlight) + 2.5);
                if (Lights != rotaryInput)
                    handleActionHandler?.Invoke(RailDriverHandleEventType.Lights, gameTime, new UserCommandArgs<int>() { Value = Lights });
                // Cab activity - any cab lever or handle changes, so there is user activity. Only consider first 8 bytes (cab controls) but misses the cab buttons
                if (BitConverter.ToUInt64(readBuffer, 0) != BitConverter.ToUInt64(readBufferHistory, 0))
                    handleActionHandler?.Invoke(RailDriverHandleEventType.CabActivity, gameTime, UserCommandArgs.Empty);

                for (byte command = 0; command < 48; command++)
                {
                    bool down = (readBuffer[8 + command / 8] & 1 << command % 8) == 1 << command % 8;
                    bool downPreviously = (readBufferHistory[8 + command / 8] & 1 << command % 8) == 1 << command % 8;

                    if (down && !downPreviously)
                    {
                        // button pressed
                        int lookup = command << KeyPressShift ^ KeyPressShift;
                        keyActionHandler?.Invoke(lookup, gameTime, KeyEventType.KeyPressed);
                    }
                    else if (down && downPreviously)
                    {
                        // button still down
                        int lookup = command << KeyDownShift ^ KeyDownShift;
                        keyActionHandler?.Invoke(lookup, gameTime, KeyEventType.KeyDown);
                    }
                    else if (!down && downPreviously)
                    {
                        // button released
                        int lookup = command << KeyUpShift ^ KeyUpShift;
                        keyActionHandler?.Invoke(lookup, gameTime, KeyEventType.KeyReleased);
                    }
                }
            }
            base.Update(gameTime);
        }

        /// <summary>
        /// Updates speed display on RailDriver LED
        /// </summary>
        /// <param name="speed"></param>
        internal void ShowSpeed(double speed)
        {
            if (Active)
                railDriverInstance?.SetLedsNumeric(Math.Abs(speed));
        }

        public void Activate()
        {
            if (Enabled)
            {
                Active = !Active;
                railDriverInstance.EnableSpeaker(Active);
                if (Active)
                {
                    railDriverInstance.SetLeds(0x39, 0x09, 0x0F);
                    InitializeSync();
                }
                else
                    railDriverInstance.SetLeds(RailDriverDisplaySign.Hyphen, RailDriverDisplaySign.Hyphen, RailDriverDisplaySign.Hyphen);
            }
        }

        private void InitializeSync()
        {
            GameTime gameTime = new GameTime();
            Update(gameTime);
            handleActionHandler?.Invoke(RailDriverHandleEventType.Wipers, gameTime, new UserCommandArgs<int>() { Value = Wipers });
            handleActionHandler?.Invoke(RailDriverHandleEventType.Lights, gameTime, new UserCommandArgs<int>() { Value = Lights });
        }

        private static float Percentage(float x, float x0, float x100)
        {
            float p = 100 * (x - x0) / (x100 - x0);
            if (p < 0)
                return 0;
            if (p > 100)
                return 100;
            return p;
        }

        private static float Percentage(byte value, (byte p0, byte p100) range)
        {
            float p = 100 * (value - range.p0) / (range.p100 - range.p0);
            if (p < 0)
                return 0;
            if (p > 100)
                return 100;
            return p;
        }

        private static float Percentage(byte value, (byte p100Minus, byte p0, byte p100Plus) range)
        {
            float p = 100 * (value - range.p0) / (range.p100Plus - range.p0);
            if (p < 0)
                p = 100 * (value - range.p0) / (range.p0 - range.p100Minus);
            if (p < -100)
                return -100;
            if (p > 100)
                return 100;
            return p;
        }

        private static (byte, byte) UpdateCutOff((byte, byte) range, byte cutOff)
        {
            if (range.Item1 < range.Item2)
            {
                range.Item1 += cutOff;
                range.Item2 -= cutOff;
            }
            else
            {
                range.Item2 += cutOff;
                range.Item1 -= cutOff;
            }
            return range;
        }

        private static (byte, byte, byte) UpdateCutOff((byte, byte, byte) range, byte cutOff)
        {
            if (range.Item1 < range.Item3)
            {
                range.Item1 += cutOff;
                range.Item3 -= cutOff;
            }
            else
            {
                range.Item3 += cutOff;
                range.Item1 -= cutOff;
            }
            return range;
        }


    }
}
