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
    public delegate void RailDriverDataRead(byte[] data, RailDriverBase sourceDevice);

    public abstract class RailDriverBase
    {
        private static RailDriverBase instance;

        public static RailDriverBase GetInstance32()
        {
            if (null == instance)
            {
                instance = new RailDriver32();
            }
            return instance.WriteBufferSize == 0 ? null : instance;
        }

        public static RailDriverBase GetInstance64()
        {
            if (null == instance)
            {
                instance = new RailDriver64();
            }
            return instance.WriteBufferSize == 0 ? null : instance;
        }

        public abstract int WriteBufferSize { get; }

        public abstract int WriteData(byte[] writeBuffer);

        public abstract int ReadData(ref byte[] readBuffer);

        public abstract event RailDriverDataRead OnDataRead;

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

        public override int WriteData(byte[] writeBuffer)
        {
            return device?.WriteData(writeBuffer) ?? -1;
        }

        public override int ReadData(ref byte[] readBuffer)
        {
            return device?.ReadData(ref readBuffer) ?? -1;
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

        public override int WriteData(byte[] writeBuffer)
        {
            return device?.WriteData(writeBuffer) ?? -1;
        }

        public override int ReadData(ref byte[] readBuffer)
        {
            return device?.ReadData(ref readBuffer) ?? -1;
        }
    }
}
