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
using System.IO;
using Orts.Parsers.Msts;
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

            public override int ReadCurrentData(ref byte[] data)
            {
                return device.ReadLast(ref data);
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

            public override int ReadCurrentData(ref byte[] data)
            {
                return device.ReadLast(ref data);
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
        private RailDriverState state;              // Interpreted data from RailDriver passed to UserInput

        private byte[] readBuffer;
        private byte[] readBufferHistory;

        // calibration values, defaults for the developer's RailDriver
        float FullReversed = 225;
        float Neutral = 116;
        float FullForward = 60;
        float FullThrottle = 229;
        float ThrottleIdle = 176;
        float DynamicBrake = 42;
        float DynamicBrakeSetup = 119;
        float AutoBrakeRelease = 216;
        float FullAutoBrake = 79;
        float EmergencyBrake = 58;
        float IndependentBrakeRelease = 213;
        float BailOffEngagedRelease = 179;
        float IndependentBrakeFull = 30;
        float BailOffEngagedFull = 209;
        float BailOffDisengagedRelease = 109;
        float BailOffDisengagedFull = 121;
        float Rotary1Position1 = 73;
        float Rotary1Position2 = 135;
        float Rotary1Position3 = 180;
        float Rotary2Position1 = 86;
        float Rotary2Position2 = 145;
        float Rotary2Position3 = 189;

        private readonly RailDriverBase railDriverInstance;

        /// <summary>
        /// Tries to find a RailDriver and initialize it
        /// </summary>
        /// <param name="basePath"></param>
        public UserInputRailDriver(string path)
        {
            state = new RailDriverState();

            if (Environment.Is64BitProcess)
            {
                railDriverInstance = new RailDriver64(this);
            }
            else
            {
                railDriverInstance = new RailDriver32(this);
            }
            writeBuffer = new byte[railDriverInstance.WriteBufferSize];
            readBuffer = new byte[railDriverInstance.ReadBufferSize];
            readBufferHistory = new byte[railDriverInstance.ReadBufferSize];
            ReadCalibrationData(path);
            SetLEDs(0x40, 0x40, 0x40);
        }

        public void Update()
        {
            if (0 == railDriverInstance.ReadCurrentData(ref readBuffer))
            {
                state.DirectionPercent = Percentage(readBuffer[1], FullReversed, Neutral, FullForward);
                state.ThrottlePercent = Percentage(readBuffer[2], ThrottleIdle, FullThrottle);

                state.DynamicBrakePercent = Percentage(readBuffer[2], ThrottleIdle, DynamicBrakeSetup, DynamicBrake);
                state.TrainBrakePercent = Percentage(readBuffer[3], AutoBrakeRelease, FullAutoBrake);
                state.EngineBrakePercent = Percentage(readBuffer[4], IndependentBrakeRelease, IndependentBrakeFull);
                float a = .01f * state.EngineBrakePercent;
                float calOff = (1 - a) * BailOffDisengagedRelease + a * BailOffDisengagedFull;
                float calOn = (1 - a) * BailOffEngagedRelease + a * BailOffEngagedFull;
                state.BailOff = Percentage(readBuffer[5], calOff, calOn) > 80;
                if (state.TrainBrakePercent >= 100)
                    state.Emergency = Percentage(readBuffer[3], FullAutoBrake, EmergencyBrake) > 50;

                state.Wipers = (int)(.01 * Percentage(readBuffer[6], Rotary1Position1, Rotary1Position2, Rotary1Position3) + 2.5);
                state.Lights = (int)(.01 * Percentage(readBuffer[7], Rotary2Position1, Rotary2Position2, Rotary2Position3) + 2.5);
                state.SaveButtonData();
                state.AddButtonData(readBuffer);

                if (state.IsPressed(4, 0x30))
                    state.Emergency = true;
                if (state.IsPressed(1, 0x40))
                {
                    active = !active;
                    EnableSpeaker(active);
                    if (active)
                    {
                        SetLEDs(0x80, 0x80, 0x80);
                        displayedSpeed = -1;
                        UserInput.RDState = state;
                    }
                    else
                    {
                        SetLEDs(0x40, 0x40, 0x40);
                        UserInput.RDState = null;
                    }
                }
                state.Changed = true;
            }
        }

        /// <summary>
        /// Error callback
        /// </summary>
        /// <param name="error"></param>
        /// <param name="sourceDevice"></param>
        private void HandlePIEHidError(long error, string message)
        {
            Trace.TraceWarning($"RailDriver Error: {error} : {message}");
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
        
        private static float Percentage(float x, float xminus100, float x0, float xplus100)
        {
            float p = 100 * (x - x0) / (xplus100 - x0);
            if (p < 0)
                p = 100 * (x - x0) / (x0 - xminus100);
            if (p < -95)
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

        private int displayedSpeed = -1;      // speed in tenths displayed on RailDriver LED

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
            if (s != displayedSpeed)
            {
                if (s < 100)
                    SetLEDs(LEDDigits[s % 10], LEDDigitsPoint[s / 10], 0);
                else if (s < 1000)
                    SetLEDs(LEDDigits[s % 10], LEDDigitsPoint[(s / 10) % 10], LEDDigits[(s / 100) % 10]);
                else if (s < 10000)
                    SetLEDs(LEDDigitsPoint[(s / 10) % 10], LEDDigits[(s / 100) % 10], LEDDigits[(s / 1000) % 10]);
                displayedSpeed = s;
            }
        }

        /// <summary>
        /// Reads RailDriver calibration data from a ModernCalibration.rdm file
        /// This file is not in the usual STF format, but the STFReader can handle it okay.
        /// </summary>
        /// <param name="basePath"></param>
        void ReadCalibrationData(string basePath)
        {
            basePath = @"C:\Storage\OR\Train Simulator";
            string file = Path.Combine(basePath, "ModernCalibration.rdm");
            if (!File.Exists(file))
            {
                SetLEDs(0, 0, 0);
                Trace.TraceWarning("Cannot find RailDriver calibration file {0}", file);
                return;
            }
			// TODO: This is... kinda weird and cool at the same time. STF parsing being used on RailDriver's calebration file. Probably should be a dedicated parser, though.
            STFReader reader = new STFReader(file, false);
            while (!reader.Eof)
            {
                string token = reader.ReadItem();
                if (token == "Position")
                {
                    string name = reader.ReadItem();
                    int min= -1;
                    int max= -1;
                    while (token != "}")
                    {
                        token = reader.ReadItem();
                        if (token == "Min")
                            min = reader.ReadInt(-1);
                        else if (token == "Max")
                            max = reader.ReadInt(-1);
                    }
                    if (min >= 0 && max >= 0)
                    {
                        float v = .5f * (min + max);
                        switch (name)
                        {
                            case "Full Reversed": FullReversed = v; break;
                            case "Neutral": Neutral = v; break;
                            case "Full Forward": FullForward = v; break;
                            case "Full Throttle": FullThrottle = v; break;
                            case "Throttle Idle": ThrottleIdle = v; break;
                            case "Dynamic Brake": DynamicBrake = v; break;
                            case "Dynamic Brake Setup": DynamicBrakeSetup = v; break;
                            case "Auto Brake Released": AutoBrakeRelease = v; break;
                            case "Full Auto Brake (CS)": FullAutoBrake = v; break;
                            case "Emergency Brake (EMG)": EmergencyBrake = v; break;
                            case "Independent Brake Released": IndependentBrakeRelease = v; break;
                            case "Bail Off Engaged (in Released position)": BailOffEngagedRelease = v; break;
                            case "Independent Brake Full": IndependentBrakeFull = v; break;
                            case "Bail Off Engaged (in Full position)": BailOffEngagedFull = v; break;
                            case "Bail Off Disengaged (in Released position)": BailOffDisengagedRelease = v; break;
                            case "Bail Off Disengaged (in Full position)": BailOffDisengagedFull = v; break;
                            case "Rotary Switch 1-Position 1(OFF)": Rotary1Position1 = v; break;
                            case "Rotary Switch 1-Position 2(SLOW)": Rotary1Position2 = v; break;
                            case "Rotary Switch 1-Position 3(FULL)": Rotary1Position3 = v; break;
                            case "Rotary Switch 2-Position 1(OFF)": Rotary2Position1 = v; break;
                            case "Rotary Switch 2-Position 2(DIM)": Rotary2Position2 = v; break;
                            case "Rotary Switch 2-Position 3(FULL)": Rotary2Position3 = v; break;
                            default: STFException.TraceInformation(reader, "Skipped unknown calibration value " + name); break;
                        }
                    }
                }
            }
        }

        public void Shutdown()
        {
            SetLEDs(0, 0, 0);
            railDriverInstance?.Shutdown();
        }
    }

    /// <summary>
    /// Processed RailDriver data sent to UserInput class
    /// </summary>
    public class RailDriverState
    {
        public bool Changed;                // true when data has been changed but not processed by HandleUserInput
        public float DirectionPercent;      // -100 (reverse) to 100 (forward)
        public float ThrottlePercent;       // 0 to 100
        public float DynamicBrakePercent;   // 0 to 100 if active otherwise less than 0
        public float TrainBrakePercent;     // 0 (release) to 100 (CS), does not include emergency
        public float EngineBrakePercent;    // 0 to 100
        public bool BailOff;                // true when bail off pressed
        public bool Emergency;              // true when train brake handle in emergency or E-stop button pressed
        public int Wipers;                  // wiper rotary, 1 off, 2 slow, 3 full
        public int Lights;                  // lights rotary, 1 off, 2 dim, 3 full
        private readonly byte[] buttonData;                  // latest button data, one bit per button
        private readonly byte[] previousButtonData;
        private RailDriverUserCommand[] Commands;

        public RailDriverState()
        {
            buttonData = new byte[6];
            previousButtonData = new byte[6];
            Commands = new RailDriverUserCommand[Enum.GetNames(typeof(UserCommands)).Length];

            // top row of blue buttons left to right

            Commands[(int)UserCommands.GamePauseMenu] = new RailDriverUserCommand(0, 0x01);
            Commands[(int)UserCommands.GameSave] = new RailDriverUserCommand(0, 0x02);

            Commands[(int)UserCommands.DisplayTrackMonitorWindow] = new RailDriverUserCommand(0, 0x08);

            Commands[(int)UserCommands.DisplaySwitchWindow] = new RailDriverUserCommand(0, 0x40);
            Commands[(int)UserCommands.DisplayTrainOperationsWindow] = new RailDriverUserCommand(0, 0x80);
            Commands[(int)UserCommands.DisplayNextStationWindow] = new RailDriverUserCommand(1, 0x01);

            Commands[(int)UserCommands.DisplayCompassWindow] = new RailDriverUserCommand(1, 0x08);
            Commands[(int)UserCommands.GameSwitchAhead] = new RailDriverUserCommand(1, 0x10);
            Commands[(int)UserCommands.GameSwitchBehind] = new RailDriverUserCommand(1, 0x20);

            // bottom row of blue buttons left to right

            //Commands[(int)UserCommands.RailDriverOnOff] = new RailDriverUserCommand(1, 0x40);         // Btn 15 Default Legend RailDriver Run/Stophandled elsewhere
            Commands[(int)UserCommands.CameraToggleShowCab] = new RailDriverUserCommand(1, 0x80);       // Btn 16 Default Legend Hide Cab Panel

            Commands[(int)UserCommands.CameraCab] = new RailDriverUserCommand(2, 0x01);                 // Btn 17 Default Legend Frnt Cab View
            Commands[(int)UserCommands.CameraOutsideFront] = new RailDriverUserCommand(2, 0x02);        // Btn 18 Default Legend Ext View 1
            Commands[(int)UserCommands.CameraOutsideRear] = new RailDriverUserCommand(2, 0x04);         // Btn 19 Default Legend Ext.View 2
            Commands[(int)UserCommands.CameraCarPrevious] = new RailDriverUserCommand(2, 0x08);         // Btn 20 Default Legend FrontCoupler

            Commands[(int)UserCommands.CameraCarNext] = new RailDriverUserCommand(2, 0x10);             // Btn 21 Default Legend Rear Coupler
            Commands[(int)UserCommands.CameraTrackside] = new RailDriverUserCommand(2, 0x20);           // Btn 22 Default Legend Track View      
            Commands[(int)UserCommands.CameraPassenger] = new RailDriverUserCommand(2, 0x40);           // Btn 23 Default Legend Passgr View      
            Commands[(int)UserCommands.CameraBrakeman] = new RailDriverUserCommand(2, 0x80);            // Btn 24 Default Legend Coupler View

            Commands[(int)UserCommands.CameraFree] = new RailDriverUserCommand(3, 0x01);                // Btn 25 Default Legend Yard View
            Commands[(int)UserCommands.GameClearSignalForward] = new RailDriverUserCommand(3, 0x02);    // Btn 26 Default Legend Request Pass
            //Commands[(int)UserCommands. load passengers] = new RailDriverUserCommand(3, 0x04);        // Btn 27 Default Legend Load/Unload
            //Commands[(int)UserCommands. ok] = new RailDriverUserCommand(3, 0x08);                     // Btn 28 Default Legend OK

            // controls to right of blue buttons

            Commands[(int)UserCommands.CameraZoomIn] = new RailDriverUserCommand(3, 0x10);
            Commands[(int)UserCommands.CameraZoomOut] = new RailDriverUserCommand(3, 0x20);
            Commands[(int)UserCommands.CameraPanUp] = new RailDriverUserCommand(3, 0x40);
            Commands[(int)UserCommands.CameraPanRight] = new RailDriverUserCommand(3, 0x80);
            Commands[(int)UserCommands.CameraPanDown] = new RailDriverUserCommand(4, 0x01);
            Commands[(int)UserCommands.CameraPanLeft] = new RailDriverUserCommand(4, 0x02);

            // buttons on top left

            //Commands[(int)UserCommands. gear shift] = new RailDriverUserCommand(4, 0x04);
            Commands[(int)UserCommands.ControlGearUp] = new RailDriverUserCommand(4, 0x04);
            //Commands[(int)UserCommands. gear shift] = new RailDriverUserCommand(4, 0x08);
            Commands[(int)UserCommands.ControlGearDown] = new RailDriverUserCommand(4, 0x08);
            //Commands[(int)UserCommands.ControlEmergency] = new RailDriverUserCommand(4, 0x30); handled elsewhere
            Commands[(int)UserCommands. ControlAlerter] = new RailDriverUserCommand(4, 0x40);
            Commands[(int)UserCommands.ControlSander] = new RailDriverUserCommand(4, 0x80);
            Commands[(int)UserCommands.ControlPantograph1] = new RailDriverUserCommand(5, 0x01);
            Commands[(int)UserCommands.ControlBellToggle] = new RailDriverUserCommand(5, 0x02);
            Commands[(int)UserCommands.ControlHorn] = new RailDriverUserCommand(5, 0x0c);//either of two bits
        }

        /// <summary>
        /// Saves the latest button data and prepares to get new data
        /// </summary>
        public void SaveButtonData()
        {
            for (int i = 0; i < buttonData.Length; i++)
            {
                previousButtonData[i] = buttonData[i];
                buttonData[i] = 0;
            }
        }

        /// <summary>
        /// Ors in new button data
        /// </summary>
        /// <param name="data"></param>
        public void AddButtonData(byte[] data)
        {
            for (int i = 0; i < buttonData.Length; i++)
                //buttonData[i] |= data[i + 8];
                buttonData[i] = data[i + 8];
        }

        public override string ToString()
        {
            string s= String.Format("{0} {1} {2} {3} {4} {5} {6}", DirectionPercent, ThrottlePercent, DynamicBrakePercent, TrainBrakePercent, EngineBrakePercent, BailOff, Emergency);
            for (int i = 0; i < 6; i++)
                s += " " + buttonData[i];
            return s;
        }

        public void Handled()
        {
//            SaveButtonData();
            Changed = false;
        }

        public bool IsPressed(int index, byte mask)
        {
            return (buttonData[index] & mask) != 0 && (previousButtonData[index] & mask) == 0;
        }

		public bool IsPressed(UserCommands command)
		{
			RailDriverUserCommand c = Commands[(int)command];
            if (c == null || Changed == false)
                return false;
			return c.IsButtonDown(buttonData) && !c.IsButtonDown(previousButtonData);
		}

		public bool IsReleased(UserCommands command)
		{
            RailDriverUserCommand c = Commands[(int)command];
            if (c == null || Changed == false)
                return false;
            return !c.IsButtonDown(buttonData) && c.IsButtonDown(previousButtonData);
		}

		public bool IsDown(UserCommands command)
		{
            RailDriverUserCommand c = Commands[(int)command];
            if (c == null)
                return false;
            return c.IsButtonDown(buttonData);
		}
    }

    public class RailDriverUserCommand
    {
        private readonly int index;
        private readonly byte mask;

        public RailDriverUserCommand(int index, byte mask)
        {
            this.index = index;
            this.mask = mask;
        }

        public bool IsButtonDown(byte[] data)
        {
            return (data[index] & mask) != 0;
        }
    }

}
