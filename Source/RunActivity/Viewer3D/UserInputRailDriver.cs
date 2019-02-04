// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using Orts.Viewer3D.Processes;
using ORTS.Common;
using ORTS.Common.Input;
using ORTS.Settings;

namespace Orts.Viewer3D
{
    /// <summary>
    /// Class to get data from RailDriver and translate it into something useful for UserInput
    /// </summary>
    public class UserInputRailDriver
    {
        private byte[] writeBuffer;                 // Buffer for sending data to RailDriver
        private bool active;                        // True when RailDriver values are used to control player loco

        private byte[] readBuffer;
        private byte[] readBufferHistory;

        private const byte EnableRailDriverCommand = 14;
        private const byte EmergencyStopCommandUp = 36;
        private const byte EmergencyStopCommandDown = 37;

        private readonly RailDriverBase railDriverInstance;
        private static RailDriverSettings settings;
        private static byte cutOff;

        public float DirectionPercent;      // -100 (reverse) to 100 (forward)
        public float ThrottlePercent;       // 0 to 100
        public float DynamicBrakePercent;   // 0 to 100 if active otherwise less than 0
        public float TrainBrakePercent;     // 0 (release) to 100 (CS), does not include emergency
        public float EngineBrakePercent;    // 0 to 100
        public bool BailOff;                // true when bail off pressed
        public bool Emergency;              // true when train brake handle in emergency or E-stop button pressed
        public int Wipers;                  // wiper rotary, 1 off, 2 slow, 3 full
        public int Lights;                  // lights rotary, 1 off, 2 dim, 3 full

        private readonly (byte, byte, byte) reverser, dynamicBrake, wipers, headlight;
        private readonly (byte, byte) throttle, autoBrake, independentBrake, emergencyBrake, bailoffDisengaged, bailoffEngaged;
        private readonly bool fullRangeThrottle;

        /// <summary>
        /// Tries to find a RailDriver and initialize it
        /// </summary>
        /// <param name="basePath"></param>
        public UserInputRailDriver(Game game)
        {

            railDriverInstance = RailDriverBase.GetInstance();
            if (railDriverInstance.Enabled)
            {
                settings = game.Settings.RailDriver;
                byte[] calibrationSettings = settings.CalibrationSettings;

                if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.ReverseReverser]))
                    reverser = (calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserFullForward], calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserNeutral], calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserFullReversed]);
                else
                    reverser = (calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserFullReversed], calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserNeutral], calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserFullForward]);

                if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.FullRangeThrottle]))
                {
                    if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.ReverseThrottle]))
                        throttle = (calibrationSettings[(byte)RailDriverCalibrationSetting.DynamicBrake], calibrationSettings[(byte)RailDriverCalibrationSetting.ThrottleFull]);
                    else
                        throttle = (calibrationSettings[(byte)RailDriverCalibrationSetting.ThrottleFull], calibrationSettings[(byte)RailDriverCalibrationSetting.DynamicBrake]);
                    fullRangeThrottle = true;
                }
                else
                {
                    if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.ReverseThrottle]))
                    {
                        throttle = (calibrationSettings[(byte)RailDriverCalibrationSetting.DynamicBrake], calibrationSettings[(byte)RailDriverCalibrationSetting.DynamicBrakeSetup]);
                        dynamicBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.DynamicBrakeSetup], calibrationSettings[(byte)RailDriverCalibrationSetting.ThrottleIdle], calibrationSettings[(byte)RailDriverCalibrationSetting.ThrottleFull]);
                    }
                    else
                    {
                        throttle = (calibrationSettings[(byte)RailDriverCalibrationSetting.ThrottleIdle], calibrationSettings[(byte)RailDriverCalibrationSetting.ThrottleFull]);
                        dynamicBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.ThrottleIdle], calibrationSettings[(byte)RailDriverCalibrationSetting.DynamicBrakeSetup], calibrationSettings[(byte)RailDriverCalibrationSetting.DynamicBrake]);
                    }
                }
                if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.ReverseAutoBrake]))
                    autoBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeFull], calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeRelease]);
                else
                    autoBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeRelease], calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeFull]);
                if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.ReverseIndependentBrake]))
                    independentBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.IndependentBrakeFull], calibrationSettings[(byte)RailDriverCalibrationSetting.IndependentBrakeRelease]);
                else
                    independentBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.IndependentBrakeRelease], calibrationSettings[(byte)RailDriverCalibrationSetting.IndependentBrakeFull]);

                emergencyBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeFull], calibrationSettings[(byte)RailDriverCalibrationSetting.EmergencyBrake]);

                wipers = (calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary1Position1], calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary1Position2], calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary1Position3]);
                headlight = (calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary2Position1], calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary2Position2], calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary2Position3]);

                bailoffDisengaged = (calibrationSettings[(byte)RailDriverCalibrationSetting.BailOffDisengagedRelease], calibrationSettings[(byte)RailDriverCalibrationSetting.BailOffDisengagedFull]);
                bailoffEngaged = (calibrationSettings[(byte)RailDriverCalibrationSetting.BailOffEngagedRelease], calibrationSettings[(byte)RailDriverCalibrationSetting.BailOffEngagedFull]);

                cutOff = settings.CalibrationSettings[(int)RailDriverCalibrationSetting.PercentageCutOffDelta];

                writeBuffer = railDriverInstance.NewWriteBuffer;
                readBuffer = railDriverInstance.NewReadBuffer;
                readBufferHistory = railDriverInstance.NewReadBuffer;

                SetLEDs(0x40, 0x40, 0x40);
            }
        }

        public void Update()
        {
            if (railDriverInstance.Enabled && 0 == railDriverInstance.ReadCurrentData(ref readBuffer))
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

                (readBufferHistory, readBuffer) = (readBuffer, readBufferHistory);

                if (IsPressed(EmergencyStopCommandUp) || IsPressed(EmergencyStopCommandDown))
                    Emergency = true;
                if (IsPressed(EnableRailDriverCommand))
                {
                    active = !active;
                    EnableSpeaker(active);
                    if (active)
                    {
                        SetLEDs(0x80, 0x80, 0x80);
                    }
                    else
                    {
                        SetLEDs(0x40, 0x40, 0x40);
                    }
                }
            }
        }

        private static float Percentage(float x, float x0, float x100)
        {
            float p = 100 * (x - x0) / (x100 - x0);
            if (p < 5)
                return 0;
            if (p > 95)
                return 100;
            return p;
        }

        private static float Percentage(byte value, (byte p0, byte p100) range)
        {
            float p = 100 * (value - range.p0) / (range.p100- range.p0);
            if (p < cutOff)
                return 0;
            if (p > (100 - cutOff))
                return 100;
            return p;
        }

        private static float Percentage(byte value, (byte p100Minus, byte p0, byte p100Plus) range) 
        {
            float p = 100 * (value - range.p0) / (range.p100Plus - range.p0);
            if (p < 0)
                p = 100 * (value - range.p0) / (range.p0 - range.p100Minus);
            if (p < (-100 + cutOff))
                return -100;
            if (p > (100 - cutOff))
                return 100;
            return p;
        }

        /// <summary>
        /// Set the RailDriver LEDs to the specified values
        /// led1 is the right most
        /// </summary>
        /// <param name="led1"></param>
        /// <param name="led2"></param>
        /// <param name="led3"></param>
        private void SetLEDs(byte led1, byte led2, byte led3)
        {
            if (!railDriverInstance.Enabled)
                return;
            writeBuffer.Initialize();
            writeBuffer[1] = 134;
            writeBuffer[2] = led1;
            writeBuffer[3] = led2;
            writeBuffer[4] = led3;
            railDriverInstance.WriteData(writeBuffer);
        }

        /// <summary>
        /// Turns raildriver speaker on or off
        /// </summary>
        /// <param name="on"></param>
        public void EnableSpeaker(bool state)
        {
            writeBuffer.Initialize();
            writeBuffer[1] = 133;
            writeBuffer[7] = (byte) (state ? 1 : 0);
            railDriverInstance.WriteData(writeBuffer);
        }

        // LED values for digits 0 to 9
        private readonly static byte[] LEDDigits = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
        // LED values for digits 0 to 9 with decimal point
        private readonly static byte[] LEDDigitsPoint = { 0xbf, 0x86, 0xdb, 0xcf, 0xe6, 0xed, 0xfd, 0x87, 0xff, 0xef };

        /// <summary>
        /// Updates speed display on RailDriver LED
        /// </summary>
        /// <param name="playerLoco"></param>
        public void ShowSpeed(float speed)
        {
            if (!active)
                return;
            speed *= 10;    //simplify display setting for fractional part
            int s = (int) (speed >= 0 ? speed + .5 : -speed + .5);
                if (s < 100)
                    SetLEDs(LEDDigits[s % 10], LEDDigitsPoint[s / 10], 0);
                else if (s < 1000)
                    SetLEDs(LEDDigits[s % 10], LEDDigitsPoint[(s / 10) % 10], LEDDigits[(s / 100) % 10]);
                else if (s < 10000)
                    SetLEDs(LEDDigitsPoint[(s / 10) % 10], LEDDigits[(s / 100) % 10], LEDDigits[(s / 1000) % 10]);
        }

        public void Shutdown()
        {
            SetLEDs(0, 0, 0);
            railDriverInstance?.Shutdown();
        }

        private bool ButtonCurrentlyDown(byte command)
        {
            return (readBuffer[8 + command / 8] & (1 << (command % 8))) == (1 << (command % 8));
        }

        private bool ButtonPreviouslyDown(byte command)
        {
            return (readBufferHistory[8 + command / 8] & (1 << (command % 8))) == (1 << (command % 8));
        }

        private bool IsPressed(byte command)
        {
            return (ButtonCurrentlyDown(command) && !ButtonPreviouslyDown(command));
        }

        private bool IsReleased(byte command)
        {
            return (!ButtonCurrentlyDown(command)) && ButtonPreviouslyDown(command);
        }

        public bool IsPressed(UserCommand command)
        {
            if (!active)
                return false;
            byte raildriverCommand = settings.UserCommands[(int)command];
            if (raildriverCommand == byte.MaxValue)
                return false;
            if (command == UserCommand.GamePauseMenu || raildriverCommand != 0)
                return command == UserCommand.ControlHorn ? (IsPressed(raildriverCommand) || IsPressed((byte)(raildriverCommand + 1))) :
                    IsPressed(raildriverCommand);
            else
                return false;
        }

        public bool IsReleased(UserCommand command)
        {
            if (!active)
                return false;
            byte raildriverCommand = settings.UserCommands[(int)command];
            if (raildriverCommand == byte.MaxValue)
                return false;
            if (command == UserCommand.GamePauseMenu || raildriverCommand != 0)
                return command == UserCommand.ControlHorn ? (IsReleased(raildriverCommand) || IsReleased((byte)(raildriverCommand + 1))) :
                IsReleased(raildriverCommand);
            else
                return false;
        }

        public bool IsDown(UserCommand command)
        {
            if (!active)
                return false;
            byte raildriverCommand = settings.UserCommands[(int)command];
            if (raildriverCommand == byte.MaxValue)
                return false;
            if (command == UserCommand.GamePauseMenu || raildriverCommand != 0)
                return ButtonCurrentlyDown(raildriverCommand);
            else
                return false;
        }

        public bool Enabled => railDriverInstance.Enabled;

    }
}
