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

    public abstract class RailDriverBase
    {
        public static readonly byte[] LedDigits = { (byte)RailDriverDisplaySign.Digit_0, (byte)RailDriverDisplaySign.Digit_1, (byte)RailDriverDisplaySign.Digit_2,
            (byte)RailDriverDisplaySign.Digit_3, (byte)RailDriverDisplaySign.Digit_4, (byte)RailDriverDisplaySign.Digit_5, (byte)RailDriverDisplaySign.Digit_6,
            (byte)RailDriverDisplaySign.Digit_7, (byte)RailDriverDisplaySign.Digit_8, (byte)RailDriverDisplaySign.Digit_9};
        public static readonly byte[] LedDecimalDigits = { (byte)RailDriverDisplaySign.Digit_0 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_1 | (byte)RailDriverDisplaySign.Decimal,
            (byte)RailDriverDisplaySign.Digit_2 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_3 | (byte)RailDriverDisplaySign.Decimal,
            (byte)RailDriverDisplaySign.Digit_4 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_5 | (byte)RailDriverDisplaySign.Decimal,
            (byte)RailDriverDisplaySign.Digit_6 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_7 | (byte)RailDriverDisplaySign.Decimal,
            (byte)RailDriverDisplaySign.Digit_8 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_9 | (byte)RailDriverDisplaySign.Decimal};

        public byte[] NewWriteBuffer => new byte[WriteBufferSize];

        private static RailDriverBase instance;
        private static byte[] writeBuffer;

        public static RailDriverBase GetInstance32()
        {
            if (null == instance)
            {
                instance = new RailDriver32();
                writeBuffer = instance.NewWriteBuffer;
            }
            return instance;
        }

        public static RailDriverBase GetInstance64()
        {
            if (null == instance)
            {
                instance = new RailDriver64();
                writeBuffer = instance.NewWriteBuffer;
            }
            return instance;
        }

        public abstract int WriteBufferSize { get; }

        public abstract int ReadBufferSize { get; }

        public abstract int WriteData(byte[] writeBuffer);
        public byte[] NewReadBuffer => new byte[ReadBufferSize];

        public abstract int ReadCurrentData(ref byte[] data);

        public abstract int BlockingReadCurrentData(ref byte[] data, int timeout);

        public abstract void Shutdown();

        public abstract bool Enabled { get; }


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
//            if (writeBuffer.Length == 0) return;
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

        /// <summary>
        /// Displays the given numeric value on RailDriver LED display
        /// </summary>
        public void SetLedsNumeric(float value)
        {
            if (value < 0 || value > 999.9)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Display Value needs to be between 0.0 and 999.9");
            value *= 10;    //simplify display setting for fractional part
            int s = (int)(value >= 0 ? value + .5 : -value + .5);
            if (s < 100)
                SetLeds(0, LedDecimalDigits[s / 10], LedDigits[s % 10]);
            else if (s < 1000)
                SetLeds(LedDigits[(s / 100) % 10], LedDecimalDigits[(s / 10) % 10], LedDigits[s % 10]);
            else if (s < 10000)
                SetLeds(LedDigits[(s / 1000) % 10], LedDigits[(s / 100) % 10], LedDecimalDigits[(s / 10) % 10]);
        }

        public void ClearDisplay()
        {
            SetLeds(RailDriverDisplaySign.Blank, RailDriverDisplaySign.Blank, RailDriverDisplaySign.Blank);
        }

        /// <summary>
        /// Turns raildriver speaker on or off
        /// </summary>
        /// <param name="on"></param>
        public void EnableSpeaker(bool state)
        {
            writeBuffer.Initialize();
            writeBuffer[1] = 133;
            writeBuffer[7] = (byte)(state ? 1 : 0);
            instance.WriteData(writeBuffer);
        }


    }

    internal class RailDriver32 : RailDriverBase
    {
        private readonly PIEHid32Net.PIEDevice device;                   // Our RailDriver

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

        public override int BlockingReadCurrentData(ref byte[] data, int timeout)
        {
            return device?.BlockingReadData(ref data, timeout) ?? -1;
        }

        public override int ReadCurrentData(ref byte[] data)
        {
            return device?.ReadLast(ref data) ?? -1;
        }

        public override void Shutdown()
        {
            device?.CloseInterface();
        }

        public override int WriteData(byte[] writeBuffer)
        {
            return device?.WriteData(writeBuffer) ?? -1;
        }
    }

    internal class RailDriver64 : RailDriverBase
    {
        private readonly PIEHid64Net.PIEDevice device;                   // Our RailDriver

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

        public override int BlockingReadCurrentData(ref byte[] data, int timeout)
        {
            return device?.BlockingReadData(ref data, timeout) ?? -1;
        }

        public override int ReadCurrentData(ref byte[] data)
        {
            return device?.ReadLast(ref data) ?? -1;
        }

        public override void Shutdown()
        {
            device?.CloseInterface();
        }

        public override int WriteData(byte[] writeBuffer)
        {
            return device?.WriteData(writeBuffer) ?? -1;
        }
    }

}
