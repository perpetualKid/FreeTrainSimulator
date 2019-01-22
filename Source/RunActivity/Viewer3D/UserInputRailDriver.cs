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
using System.Diagnostics;
using Orts.Viewer3D.Processes;
using ORTS.Common;
using ORTS.Settings;

namespace Orts.Viewer3D
{
    /// <summary>
    /// Class to get data from RailDriver and translate it into something useful for UserInput
    /// </summary>
    public class UserInputRailDriver
    {
        private abstract class RailDriverBase
        {
            public abstract int WriteBufferSize { get; }

            public abstract int ReadBufferSize { get; }

            public abstract void WriteData(byte[] writeBuffer);

            public abstract int ReadCurrentData(ref byte[] data);

            public abstract void Shutdown();

            public abstract bool Enabled { get; }
        }

        private class RailDriver32 : RailDriverBase
        {
            private readonly PIEHid32Net.PIEDevice device;                   // Our RailDriver

            public RailDriver32(UserInputRailDriver parent)
            {
                try
                {
                    foreach (PIEHid32Net.PIEDevice currentDevice in PIEHid32Net.PIEDevice.EnumeratePIE())
                    {
                        if (currentDevice.HidUsagePage == 0xc && currentDevice.Pid == 210)
                        {
                            device = currentDevice;
                            device.SetupInterface();
                            device.suppressDuplicateReports = true;
                            break;
                        }
                    }
                }
                catch (Exception error)
                {
                    device = null;
                    Trace.WriteLine(error);
                }
            }

            public override int WriteBufferSize => device?.WriteLength ?? 0;

            public override int ReadBufferSize => device?.ReadLength ?? 0;

            public override bool Enabled => device != null;

            public override int ReadCurrentData(ref byte[] data)
            {
                return device?.ReadLast(ref data) ?? 0;
            }

            public override void Shutdown()
            {
                device?.CloseInterface();
            }

            public override void WriteData(byte[] writeBuffer)
            {
                device?.WriteData(writeBuffer);
            }
        }

        private class RailDriver64 : RailDriverBase
        {
            private readonly PIEHid64Net.PIEDevice device;                   // Our RailDriver

            public RailDriver64(UserInputRailDriver parent)
            {
                try
                {
                    foreach (PIEHid64Net.PIEDevice currentDevice in PIEHid64Net.PIEDevice.EnumeratePIE())
                    {
                        if (currentDevice.HidUsagePage == 0xc && currentDevice.Pid == 210)
                        {
                            device = currentDevice;
                            device.SetupInterface();
                            device.suppressDuplicateReports = true;
                            break;
                        }
                    }
                }
                catch (Exception error)
                {
                    device = null;
                    Trace.WriteLine(error);
                }
            }

            public override int WriteBufferSize => (int)(device?.WriteLength ?? 0);

            public override int ReadBufferSize => (int)(device?.ReadLength ?? 0);

            public override bool Enabled => device != null;

            public override int ReadCurrentData(ref byte[] data)
            {
                return device?.ReadLast(ref data) ?? 0;
            }

            public override void Shutdown()
            {
                device?.CloseInterface();
            }

            public override void WriteData(byte[] writeBuffer)
            {
                device?.WriteData(writeBuffer);
            }
        }

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

        /// <summary>
        /// Tries to find a RailDriver and initialize it
        /// </summary>
        /// <param name="basePath"></param>
        public UserInputRailDriver(Game game)
        {

            if (Environment.Is64BitProcess)
            {
                railDriverInstance = new RailDriver64(this);
            }
            else
            {
                railDriverInstance = new RailDriver32(this);
            }
            if (railDriverInstance.Enabled)
            {
                settings = game.Settings.RailDriver;
                cutOff = settings.CalibrationSettings[(int)RailDriverCalibrationSetting.PercentageCutOffDelta];

                writeBuffer = new byte[railDriverInstance.WriteBufferSize];
                readBuffer = new byte[railDriverInstance.ReadBufferSize];
                readBufferHistory = new byte[railDriverInstance.ReadBufferSize];

                SetLEDs(0x40, 0x40, 0x40);

            }
        }

        public void Update()
        {
            if (railDriverInstance.Enabled && 0 == railDriverInstance.ReadCurrentData(ref readBuffer))
            {
                DirectionPercent = Percentage(readBuffer[1],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.FullReversed],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.Neutral],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.FullForward]);
                ThrottlePercent = Percentage(readBuffer[2],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.ThrottleIdle],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.FullThrottle]);
                DynamicBrakePercent = Percentage(readBuffer[2],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.ThrottleIdle],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.DynamicBrakeSetup],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.DynamicBrake]);
                TrainBrakePercent = Percentage(readBuffer[3],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.AutoBrakeRelease],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.FullAutoBrake]);
                EngineBrakePercent = Percentage(readBuffer[4],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.IndependentBrakeRelease],
                    settings.CalibrationSettings[(int)RailDriverCalibrationSetting.IndependentBrakeFull]);
                float a = .01f * EngineBrakePercent;
                float calOff = (1 - a) * settings.CalibrationSettings[(int)RailDriverCalibrationSetting.BailOffDisengagedRelease] + a * settings.CalibrationSettings[(int)RailDriverCalibrationSetting.BailOffDisengagedFull];
                float calOn = (1 - a) * settings.CalibrationSettings[(int)RailDriverCalibrationSetting.BailOffEngagedRelease] + a * settings.CalibrationSettings[(int)RailDriverCalibrationSetting.BailOffEngagedFull];
                BailOff = Percentage(readBuffer[5], calOff, calOn) > 80;
                if (TrainBrakePercent >= 100)
                    Emergency = Percentage(readBuffer[3],
                        settings.CalibrationSettings[(int)RailDriverCalibrationSetting.FullAutoBrake],
                        settings.CalibrationSettings[(int)RailDriverCalibrationSetting.EmergencyBrake]) > 50;

                Wipers = (int)(.01 * Percentage(readBuffer[6],
                                            settings.CalibrationSettings[(int)RailDriverCalibrationSetting.Rotary1Position1],
                                            settings.CalibrationSettings[(int)RailDriverCalibrationSetting.Rotary1Position2],
                                            settings.CalibrationSettings[(int)RailDriverCalibrationSetting.Rotary1Position3]) + 2.5);
                Lights = (int)(.01 * Percentage(readBuffer[7],
                                            settings.CalibrationSettings[(int)RailDriverCalibrationSetting.Rotary2Position1],
                                            settings.CalibrationSettings[(int)RailDriverCalibrationSetting.Rotary2Position2],
                                            settings.CalibrationSettings[(int)RailDriverCalibrationSetting.Rotary2Position3]) + 2.5);

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
        
        private static float Percentage(byte value, byte p0, byte p100)
        {
            float p = 100 * (value - p0) / (p100 - p0);
            if (p < cutOff)
                return 0;
            if (p > (100 - cutOff))
                return 100;
            return p;
        }

        private static float Percentage(byte value, byte p100Minus, byte p0, byte p100Plus)
        {
            float p = 100 * (value - p0) / (p100Plus - p0);
            if (p < 0)
                p = 100 * (value - p0) / (p0 - p100Minus);
            if (p < (-100 + cutOff))
                return -100;
            if (p > 95)
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
            if (command == UserCommand.GamePauseMenu || raildriverCommand != 0)
                return ButtonCurrentlyDown(raildriverCommand);
            else
                return false;
        }

        public bool Enabled => railDriverInstance.Enabled;

    }
}
