using System;

namespace Orts.Common.Input
{
#pragma warning disable CA1708 // Identifiers should differ by more than case
    public enum RailDriverDisplaySign
#pragma warning restore CA1708 // Identifiers should differ by more than case
    {
        Blank = 0x0,
        Digit0 = 0x3f,
        Digit1 = 0x06,
        Digit2 = 0x5b,
        Digit3 = 0x4f,
        Digit4 = 0x66,
        Digit5 = 0x6d,
        Digit6 = 0x7d,
        Digit7 = 0x07,
        Digit8 = 0x7f,
        Digit9 = 0x6f,
        Dot = 0x80,
        Hyphen = 0x40,
        A = 0x77,
        b = 0x7c,
        C = 0x39,
        c = 0x58,
        d = 0x5e,
        E = 0x79,
        F = 0x71,
        H = 0x76,
        h = 0x74,
        L = 0x38,
        l = 0x30,
        n = 0x54,
        o = 0x5c,
        P = 0x73,
        r = 0x50,
        t = 0x78,
        U = 0x3E,
        u = 0x1c,
    }

    public abstract class RailDriverBase
    {
        public static readonly byte[] LedDigits = { (byte)RailDriverDisplaySign.Digit0, (byte)RailDriverDisplaySign.Digit1, (byte)RailDriverDisplaySign.Digit2,
            (byte)RailDriverDisplaySign.Digit3, (byte)RailDriverDisplaySign.Digit4, (byte)RailDriverDisplaySign.Digit5, (byte)RailDriverDisplaySign.Digit6,
            (byte)RailDriverDisplaySign.Digit7, (byte)RailDriverDisplaySign.Digit8, (byte)RailDriverDisplaySign.Digit9};
        public static readonly byte[] LedDecimalDigits = { (byte)RailDriverDisplaySign.Digit0 | (byte)RailDriverDisplaySign.Dot, (byte)RailDriverDisplaySign.Digit1 | (byte)RailDriverDisplaySign.Dot,
            (byte)RailDriverDisplaySign.Digit2 | (byte)RailDriverDisplaySign.Dot, (byte)RailDriverDisplaySign.Digit3 | (byte)RailDriverDisplaySign.Dot,
            (byte)RailDriverDisplaySign.Digit4 | (byte)RailDriverDisplaySign.Dot, (byte)RailDriverDisplaySign.Digit5 | (byte)RailDriverDisplaySign.Dot,
            (byte)RailDriverDisplaySign.Digit6 | (byte)RailDriverDisplaySign.Dot, (byte)RailDriverDisplaySign.Digit7 | (byte)RailDriverDisplaySign.Dot,
            (byte)RailDriverDisplaySign.Digit8 | (byte)RailDriverDisplaySign.Dot, (byte)RailDriverDisplaySign.Digit9 | (byte)RailDriverDisplaySign.Dot};

        //default calibration settings from another developer's PC, they are as good as random numbers...
        public static readonly EnumArray<byte, RailDriverCalibrationSetting> DefaultCalibrationSettings =
            new EnumArray<byte, RailDriverCalibrationSetting>(new byte[] { 225, 116, 60, 229, 176, 42, 119, 216, 79, 58, 213, 179, 30, 209, 109, 121, 73, 135, 180, 86, 145, 189, 0, 0, 0, 0, 0, 1 });

        public abstract int WriteBufferSize { get; }

        public abstract int ReadBufferSize { get; }

        public abstract int WriteData(byte[] writeBuffer);

        public byte[] GetReadBuffer()
        {
            return new byte[ReadBufferSize];
        }

        public byte[] GetWriteBuffer()
        {
            return new byte[WriteBufferSize];
        }

        public abstract int ReadCurrentData(ref byte[] data);

        public abstract int BlockingReadCurrentData(ref byte[] data, int timeout);

        public abstract void Shutdown();

        public abstract bool Enabled { get; }

        private static RailDriverBase instance;
        private byte[] writeBuffer;

        public static RailDriverBase GetInstance()
        {
            if (null == instance)
            {
                if (Environment.Is64BitProcess)
                {
                    instance = new RailDriver64();
                }
                else
                {
                    instance = new RailDriver32();
                }
                instance.writeBuffer = instance.GetWriteBuffer();
            }
            return instance;
        }

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

        /// <summary>
        /// Displays the given numeric value on RailDriver LED display
        /// </summary>
        public void SetLedsNumeric(double value)
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
                System.Diagnostics.Trace.WriteLine(error);
                throw;
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
                System.Diagnostics.Trace.WriteLine(error);
                throw;
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
