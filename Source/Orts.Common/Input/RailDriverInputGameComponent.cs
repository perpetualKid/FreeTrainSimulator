using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
    public class RailDriverInputGameComponent : GameComponent
    {
        private const int KeyPressShift = 8;
        private const int KeyDownShift = 13;
        private const int KeyUpShift = 17;

        private byte[] readBuffer;
        private byte[] readBufferHistory;

        private readonly RailDriverBase railDriverInstance;

        private float DirectionPercent;      // -100 (reverse) to 100 (forward)
        private float ThrottlePercent;       // 0 to 100
        private float DynamicBrakePercent;   // 0 to 100 if active otherwise less than 0
        private float TrainBrakePercent;     // 0 (release) to 100 (CS), does not include emergency
        private float EngineBrakePercent;    // 0 to 100
        private bool BailOff;                // true when bail off pressed
        private bool Emergency;              // true when train brake handle in emergency or E-stop button pressed
        private int Wipers;                  // wiper rotary, 1 off, 2 slow, 3 full
        private int Lights;                  // lights rotary, 1 off, 2 dim, 3 full

        private readonly (byte, byte, byte) reverser, dynamicBrake, wipers, headlight;
        private readonly (byte, byte) throttle, autoBrake, independentBrake, emergencyBrake, bailoffDisengaged, bailoffEngaged;
        private readonly bool fullRangeThrottle;

        private Action<int, GameTime, KeyEventType> keyActionHandler;

        public RailDriverInputGameComponent(Game game, EnumArray<byte, RailDriverCalibrationSetting> calibrationSettings) : base(game)
        {
            railDriverInstance = RailDriverBase.GetInstance();
            Enabled = railDriverInstance.Enabled;
            readBuffer = railDriverInstance.GetReadBuffer();
            readBufferHistory = railDriverInstance.GetReadBuffer();

            if (null == calibrationSettings)
                calibrationSettings = RailDriverBase.DefaultCalibrationSettings;

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
            base.OnEnabledChanged(sender, args);
        }

        public void AddInputHandler(Action<int, GameTime, KeyEventType> inputAction)
        {
            keyActionHandler += inputAction;
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
            if (!Game.IsActive)
            {
                return;
            }

            (readBufferHistory, readBuffer) = (readBuffer, readBufferHistory);

            if (0 == railDriverInstance.ReadCurrentData(ref readBuffer))
            {
                DirectionPercent = Percentage(readBuffer[1], reverser);
                ThrottlePercent = Percentage(readBuffer[2], throttle);
                if (!fullRangeThrottle)
                    DynamicBrakePercent = Percentage(readBuffer[2], dynamicBrake);
                TrainBrakePercent = Percentage(readBuffer[3], autoBrake);
                EngineBrakePercent = Percentage(readBuffer[4], independentBrake);
                float a = .01f * EngineBrakePercent;
                float calOff = (1 - a) * bailoffDisengaged.Item1 + a * bailoffDisengaged.Item2;
                float calOn = (1 - a) * bailoffEngaged.Item1 + a * bailoffEngaged.Item2;
                BailOff = Percentage(readBuffer[5], calOff, calOn) > 80;

                if (TrainBrakePercent >= 100)
                    Emergency = Percentage(readBuffer[3], emergencyBrake) > 50;

                Wipers = (int)(.01 * Percentage(readBuffer[6], wipers) + 2.5);
                Lights = (int)(.01 * Percentage(readBuffer[7], headlight) + 2.5);

                //if (IsPressed(EmergencyStopCommandUp) || IsPressed(EmergencyStopCommandDown))
                //    Emergency = true;

                for (byte command = 0; command < 48; command++)
                {
                    bool down = (readBuffer[8 + command / 8] & (1 << (command % 8))) == (1 << (command % 8));
                    bool downPreviously = (readBufferHistory[8 + command / 8] & (1 << (command % 8))) == (1 << (command % 8));

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
