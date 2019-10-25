﻿// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
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

namespace Orts.ExternalDevices
{
    public enum RailDriverDisplaySign : byte
    {
        Blank = 0x0,
        Digit_0 = 0x3f,
        Digit_1 = 0x06,
        Digit_2 = 0x5b,
        Digit_3 = 0x4f,
        Digit_4 = 0x66,
        Digit_5 = 0x6d,
        Digit_6 = 0x7d,
        Digit_7 = 0x07,
        Digit_8 = 0x7f,
        Digit_9 = 0x6f,
        Decimal = 0x80,
        Hyphen = 0x40,
        Char_A = 0x77,
        Char_b = 0x7c,
        Char_C = 0x39,
        Char_c = 0x58,
        Char_d = 0x58,
        Char_E = 0x79,
        Char_F = 0x71,
        Char_H = 0x76,
        Char_h = 0x74,
        Char_L = 0x38,
        Char_l = 0x30,
        Char_n = 0x54,
        Char_o = 0x5c,
        Char_P = 0x73,
        Char_r = 0x50,
        Char_t = 0x78,
        Char_U = 0x3E,
        Char_u = 0x1c,
    }

    public delegate void RailDriverDataRead(byte[] data, RailDriverBase sourceDevice);

    public abstract class RailDriverBase
    {
        public static readonly byte[] LedDigits = { (byte)RailDriverDisplaySign.Digit_0, (byte)RailDriverDisplaySign.Digit_1, (byte)RailDriverDisplaySign.Digit_2,
            (byte)RailDriverDisplaySign.Digit_3, (byte)RailDriverDisplaySign.Digit_4, (byte)RailDriverDisplaySign.Digit_5, (byte)RailDriverDisplaySign.Digit_6,
            (byte)RailDriverDisplaySign.Digit_7, (byte)RailDriverDisplaySign.Digit_8, (byte)RailDriverDisplaySign.Digit_9};

        public byte[] NewWriteBuffer => new byte[WriteBufferSize];

        private static RailDriverBase instance;
        private static byte[] writeBuffer;

        public static RailDriverBase GetInstance32()
        {
            if (null == instance)
            {
                instance = new RailDriver32();
            }
            if (instance.WriteBufferSize == 0) return null;
            else
            {
                writeBuffer = instance.NewWriteBuffer;
                return null;
            }
        }

        public static RailDriverBase GetInstance64()
        {
            if (null == instance)
            {
                instance = new RailDriver64();
            }
            if (instance.WriteBufferSize == 0) return null;
            else
            {
                writeBuffer = instance.NewWriteBuffer;
                return null;
            }
        }

        public abstract int WriteBufferSize { get; }

        public abstract int ReadBufferSize { get; }

        public abstract int WriteData(byte[] writeBuffer);

        public byte[] NewReadBuffer => new byte[ReadBufferSize];

        public abstract int ReadData(ref byte[] readBuffer);

        public abstract int ReadCurrentData(ref byte[] data);

        public abstract event RailDriverDataRead OnDataRead;


        /// <summary>
        /// Set the RailDriver LEDs to the specified values
        /// </summary>
        /// <param name="led1"></param>
        /// <param name="led2"></param>
        /// <param name="led3"></param>
        public void SetLeds(byte led1, byte led2, byte led3)
        {
            writeBuffer.Initialize();
            writeBuffer[1] = 134;
            writeBuffer[2] = led3;
            writeBuffer[3] = led2;
            writeBuffer[4] = led1;
            instance?.WriteData(writeBuffer);
        }

        /// <summary>
        /// Set the RailDriver LEDs to the specified values
        /// </summary>
        /// <param name="led1"></param>
        /// <param name="led2"></param>
        /// <param name="led3"></param>
        public void SetLeds(RailDriverDisplaySign led1, RailDriverDisplaySign led2, RailDriverDisplaySign led3)
        {
            writeBuffer.Initialize();
            writeBuffer[1] = 134;
            writeBuffer[2] = (byte)led3;
            writeBuffer[3] = (byte)led2;
            writeBuffer[4] = (byte)led1;
            instance?.WriteData(writeBuffer);
        }


        /// <summary>
        /// Displays the given numeric value on RailDriver LED display
        /// </summary>
        public void SetLedsNumeric(uint value)
        {
            if (value > 999)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Display Value needs to be between 0 and 999");
            if (value < 10)
                SetLeds(0, 0, LedDigits[value]);
            else if (value < 100)
                SetLeds(0, LedDigits[(value / 10) % 10], LedDigits[(value) % 10]);
            else if (value < 1000)
                SetLeds(LedDigits[(value / 100) % 10], LedDigits[(value / 10) % 10], LedDigits[(value) % 10]);
        }

        public void ClearDisplay()
        {
            SetLeds(RailDriverDisplaySign.Blank, RailDriverDisplaySign.Blank, RailDriverDisplaySign.Blank);
        }
    }

    internal class RailDriver32 : RailDriverBase, PIEHid32Net.PIEDataHandler, PIEHid32Net.PIEErrorHandler
    {
        private readonly PIEHid32Net.PIEDevice device;                   // Our RailDriver

        public override event RailDriverDataRead OnDataRead;

        public RailDriver32()
        {
            try
            {
                foreach (PIEHid32Net.PIEDevice currentDevice in PIEHid32Net.PIEDevice.EnumeratePIE())
                {
                    if (currentDevice.HidUsagePage == 0xc && currentDevice.Pid == 210)
                    {
                        device = currentDevice;
                        device.SetupInterface();
                        device.SetErrorCallback(this);
                        device.SetDataCallback(this);
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

        public void HandlePIEHidData(byte[] data, PIEHid32Net.PIEDevice sourceDevice, int error)
        {
            OnDataRead?.Invoke(data, this);
        }

        public void HandlePIEHidError(PIEHid32Net.PIEDevice sourceDevices, int error)
        {
            Trace.TraceWarning("RailDriver Error: {0}", error);
        }

        public override int WriteBufferSize => device?.WriteLength ?? 0;

        public override int ReadBufferSize => device?.ReadLength ?? 0;

        public override int WriteData(byte[] writeBuffer)
        {
            return device?.WriteData(writeBuffer) ?? -1;
        }

        public override int ReadData(ref byte[] readBuffer)
        {
            return device?.ReadData(ref readBuffer) ?? -1;
        }

        public override int ReadCurrentData(ref byte[] data)
        {
            return device?.ReadLast(ref data) ?? -1;
        }
    }

    internal class RailDriver64 : RailDriverBase, PIEHid64Net.PIEDataHandler, PIEHid64Net.PIEErrorHandler
    {
        private readonly PIEHid64Net.PIEDevice device;                   // Our RailDriver

        public override event RailDriverDataRead OnDataRead;

        public RailDriver64()
        {
            try
            {
                foreach (PIEHid64Net.PIEDevice currentDevice in PIEHid64Net.PIEDevice.EnumeratePIE())
                {
                    if (currentDevice.HidUsagePage == 0xc && currentDevice.Pid == 210)
                    {
                        device = currentDevice;
                        device.SetupInterface();
                        device.SetErrorCallback(this);
                        device.SetDataCallback(this);
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

        public void HandlePIEHidData(byte[] data, PIEHid64Net.PIEDevice sourceDevice, int error)
        {
            OnDataRead?.Invoke(data, this);
        }

        public void HandlePIEHidError(PIEHid64Net.PIEDevice sourceDevices, long error)
        {
            Trace.TraceWarning("RailDriver Error: {0}", error);
        }

        public override int WriteBufferSize => (int)(device?.WriteLength ?? 0);

        public override int ReadBufferSize => (int)(device?.ReadLength ?? 0);

        public override int WriteData(byte[] writeBuffer)
        {
            return device?.WriteData(writeBuffer) ?? -1;
        }

        public override int ReadData(ref byte[] readBuffer)
        {
            return device?.ReadData(ref readBuffer) ?? -1;
        }

        public override int ReadCurrentData(ref byte[] data)
        {
            return device?.ReadLast(ref data) ?? -1;
        }
    }
}
