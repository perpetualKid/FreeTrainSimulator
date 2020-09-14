// COPYRIGHT 2015 by the Open Rails project.
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
using System.Management;
using System.Text;
using System.Windows.Forms;

using Orts.Common.Native;

namespace Orts.Common.Info
{
    public static class SystemInfo
    {
        public static void WriteSystemDetails(TextWriter output)
        {
            if (null == output)
                throw new ArgumentNullException(nameof(output));

            output.WriteLine($"Date/Time  = {DateTime.Now} ({DateTime.UtcNow:u})");
            WriteEnvironment(output);
            output.WriteLine($"Runtime    = {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} ({(Environment.Is64BitProcess ? "64" : "32")}bit)");
        }

        private static void WriteEnvironment(TextWriter output)
        {
            NativeStructs.MemoryStatusExtended buffer = new NativeStructs.MemoryStatusExtended { Size = 64 };
            NativeMethods.GlobalMemoryStatusEx(buffer);
            try
            {
                using (ManagementClass managementClass = new ManagementClass("Win32_BIOS"))
                {
                    foreach (ManagementObject bios in managementClass.GetInstances())
                    {
                        output.WriteLine($"BIOS       = {bios["Description"]} ({bios["Manufacturer"]})");
                    }
                }
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            try
            {
                using (ManagementClass managementClass = new ManagementClass("Win32_Processor"))
                {
                    foreach (ManagementObject processor in managementClass.GetInstances())
                    {
                        output.Write($"Processor  = {processor["Name"]} ({(uint)processor["NumberOfLogicalProcessors"]} threads, {processor["NumberOfCores"]} cores, {(uint)processor["MaxClockSpeed"] / 1000f:F1} GHz)");
                        foreach (ManagementObject cpuCache in processor.GetRelated("Win32_CacheMemory"))
                        {
                            output.Write($" ({cpuCache["Purpose"]} {cpuCache["InstalledSize"]:F0} KB)");
                        }
                        output.WriteLine();
                    }
                }
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            output.WriteLine($"Memory     = {buffer.TotalPhysical / 1024f / 1024 / 1024:F1} GB");
            try
            {
                using (ManagementClass managementClass = new ManagementClass("Win32_VideoController"))
                {
                    foreach (ManagementObject display in managementClass.GetInstances())
                    {
                        output.WriteLine($"Video      = {display["Description"]} ({(uint)display["AdapterRAM"] / 1024f / 1024 / 1024:F1} GB RAM){GetPnPDeviceDrivers(display)}");
                    }
                }
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }

            foreach (Screen screen in Screen.AllScreens)
            {
                output.WriteLine($"Display    = {screen.DeviceName} (resolution {screen.Bounds.Width} x {screen.Bounds.Height}, {screen.BitsPerPixel}-bit{(screen.Primary ? ", primary" : "")}, location {screen.Bounds.X} x {screen.Bounds.Y})");
            }

            try
            {
                using (ManagementClass managementClass = new ManagementClass("Win32_SoundDevice"))
                {
                    foreach (ManagementObject sound in managementClass.GetInstances())
                    {
                        Console.WriteLine($"Sound      = {sound["Description"]}{GetPnPDeviceDrivers(sound)}");
                    }
                }
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            try
            {
                using (ManagementClass managementClass = new ManagementClass("Win32_LogicalDisk"))
                {
                    foreach (ManagementObject disk in managementClass.GetInstances())
                    {
                        output.Write($"Disk       = {disk["Name"]} ({disk["Description"]}, {disk["FileSystem"]}");
                        if (disk["Size"] != null && disk["FreeSpace"] != null)
                            output.WriteLine($", {(ulong)disk["Size"] / 1024f / 1024 / 1024:F1} GB, {(ulong)disk["FreeSpace"] / 1024f / 1024 / 1024:F1} GB free)");
                        else
                            output.WriteLine(")");
                    }
                }
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            try
            {
                using (ManagementClass managementClass = new ManagementClass("Win32_OperatingSystem"))
                {
                    foreach (ManagementObject os in managementClass.GetInstances())
                    {
                        output.WriteLine($"OS         = {os["Caption"]} {os["OSArchitecture"]} ({os["Version"]})");
                    }
                }
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
        }

        private static string GetPnPDeviceDrivers(ManagementObject device)
        {
            StringBuilder output = new StringBuilder();
            foreach (ManagementObject pnpDevice in device.GetRelated("Win32_PnPEntity"))
            {
                foreach (ManagementObject dataFile in pnpDevice.GetRelated("CIM_DataFile"))
                {
                    output.Append($" ({dataFile["FileName"]} {dataFile["Version"]})");
                }
            }
            return output.ToString();
        }
    }
}
