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
using ORTS.Common.Input;
using Orts.ExternalDevices;
using ORTS.Settings;

namespace Orts.Viewer3D
{
    /// <summary>
    /// Class to get data from RailDriver and translate it into something useful for UserInput
    /// </summary>
    public class UserInputRailDriver
    {
        private byte[] readBuffer;
        private byte[] readBufferHistory;

        private const byte EnableRailDriverCommand = 14;
        private const byte EmergencyStopCommandUp = 36;
        private const byte EmergencyStopCommandDown = 37;

        private readonly RailDriverBase railDriverInstance;
        private static RailDriverSettings settings;

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
            try
            {
                if (Environment.Is64BitProcess)
                    railDriverInstance = RailDriverBase.GetInstance64();
                else
                    railDriverInstance = RailDriverBase.GetInstance32();
                if (railDriverInstance.Enabled)
                {
                    settings = game.Settings.RailDriver;
                    byte cutOff = settings.CalibrationSettings[(int)RailDriverCalibrationSetting.CutOffDelta];

                    byte[] calibrationSettings = settings.CalibrationSettings;

                    if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.ReverseReverser]))
                        reverser = (calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserFullForward], calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserNeutral], calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserFullReversed]);
                    else
                        reverser = (calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserFullReversed], calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserNeutral], calibrationSettings[(byte)RailDriverCalibrationSetting.ReverserFullForward]);
                    reverser = UpdateCutOff(reverser, cutOff);

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
                    throttle = UpdateCutOff(throttle, cutOff);
                    dynamicBrake = UpdateCutOff(dynamicBrake, cutOff);

                    if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.ReverseAutoBrake]))
                        autoBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeFull], calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeRelease]);
                    else
                        autoBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeRelease], calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeFull]);
                    if (Convert.ToBoolean(calibrationSettings[(int)RailDriverCalibrationSetting.ReverseIndependentBrake]))
                        independentBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.IndependentBrakeFull], calibrationSettings[(byte)RailDriverCalibrationSetting.IndependentBrakeRelease]);
                    else
                        independentBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.IndependentBrakeRelease], calibrationSettings[(byte)RailDriverCalibrationSetting.IndependentBrakeFull]);
                    autoBrake = UpdateCutOff(autoBrake, cutOff);
                    independentBrake = UpdateCutOff(independentBrake, cutOff);

                    emergencyBrake = (calibrationSettings[(byte)RailDriverCalibrationSetting.AutoBrakeFull], calibrationSettings[(byte)RailDriverCalibrationSetting.EmergencyBrake]);
                    emergencyBrake = UpdateCutOff(emergencyBrake, cutOff);

                    wipers = (calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary1Position1], calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary1Position2], calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary1Position3]);
                    headlight = (calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary2Position1], calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary2Position2], calibrationSettings[(byte)RailDriverCalibrationSetting.Rotary2Position3]);

                    bailoffDisengaged = (calibrationSettings[(byte)RailDriverCalibrationSetting.BailOffDisengagedRelease], calibrationSettings[(byte)RailDriverCalibrationSetting.BailOffDisengagedFull]);
                    bailoffEngaged = (calibrationSettings[(byte)RailDriverCalibrationSetting.BailOffEngagedRelease], calibrationSettings[(byte)RailDriverCalibrationSetting.BailOffEngagedFull]);
                    bailoffDisengaged = UpdateCutOff(bailoffDisengaged, cutOff);
                    bailoffEngaged = UpdateCutOff(bailoffEngaged, cutOff);

                    readBuffer = railDriverInstance.NewReadBuffer;
                    readBufferHistory = railDriverInstance.NewReadBuffer;

                    railDriverInstance.SetLeds(RailDriverDisplaySign.Hyphen, RailDriverDisplaySign.Hyphen, RailDriverDisplaySign.Hyphen);
                }
            }
            catch (Exception error)
            {
                railDriverInstance = null;
                Trace.WriteLine(error);
            }

        }

        public void Update()
        {
            (readBufferHistory, readBuffer) = (readBuffer, readBufferHistory);
            if (railDriverInstance.Enabled && 0 == railDriverInstance.ReadCurrentData(ref readBuffer))
            {
                if (Active)
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

                    if (IsPressed(EmergencyStopCommandUp) || IsPressed(EmergencyStopCommandDown))
                        Emergency = true;
                }
            }
        }

        public void Activate()
        {
            if (railDriverInstance.Enabled)
            {
                Active = !Active;
                railDriverInstance.EnableSpeaker(Active);
                if (Active)
                {
                    railDriverInstance.SetLeds(0x39, 0x09, 0x0F);
                }
                else
                {
                    railDriverInstance.SetLeds(RailDriverDisplaySign.Hyphen, RailDriverDisplaySign.Hyphen, RailDriverDisplaySign.Hyphen);
                }
            }
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
            float p = 100 * (value - range.p0) / (range.p100- range.p0);
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

        private (byte, byte) UpdateCutOff((byte, byte) range, byte cutOff)
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

        private (byte, byte, byte) UpdateCutOff((byte, byte, byte) range, byte cutOff)
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

        public bool Active { get; private set; }

        /// <summary>
        /// Updates speed display on RailDriver LED
        /// </summary>
        /// <param name="speed"></param>
        public void ShowSpeed(float speed)
        {
            if (Active)
                railDriverInstance?.SetLedsNumeric(Math.Abs(speed));
        }

        public void Shutdown()
        {
            if (railDriverInstance.Enabled)
            {
                railDriverInstance?.ClearDisplay();
                railDriverInstance?.Shutdown();
            }
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
            if (!(Active || (railDriverInstance.Enabled && command == UserCommand.GameExternalCabController)))
                return false;
            byte raildriverCommand = settings.UserCommands[(int)command];
            if (raildriverCommand == byte.MaxValue)
                return false;
            if (command == UserCommand.GamePauseMenu || raildriverCommand != 0)
                return 
                    command == UserCommand.ControlHorn ? (IsPressed(raildriverCommand) || IsPressed((byte)(raildriverCommand + 1))) :
                    IsPressed(raildriverCommand);
            else
                return false;
        }

        public bool IsReleased(UserCommand command)
        {
            if (!Active)
                return false;
            byte raildriverCommand = settings.UserCommands[(int)command];
            if (raildriverCommand == byte.MaxValue)
                return false;
            if (command == UserCommand.GamePauseMenu || raildriverCommand != 0)
                return 
                    command == UserCommand.ControlHorn ? (IsReleased(raildriverCommand) || IsReleased((byte)(raildriverCommand + 1))) :
                IsReleased(raildriverCommand);
            else
                return false;
        }

        public bool IsDown(UserCommand command)
        {
            if (!Active)
                return false;
            byte raildriverCommand = settings.UserCommands[(int)command];
            if (raildriverCommand == byte.MaxValue)
                return false;
            if (command == UserCommand.GamePauseMenu || raildriverCommand != 0)
                return ButtonCurrentlyDown(raildriverCommand);
            else
                return false;
        }

    }
}
