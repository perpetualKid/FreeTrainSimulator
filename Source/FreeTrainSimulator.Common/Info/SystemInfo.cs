using System;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

using FreeTrainSimulator.Common.Native;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Common.Info
{
    public static class SystemInfo
    {
        public static string GraphicAdapterMemoryInformation { get; private set; }
        public static string CpuInformation { get; private set; }

        public static string SetGraphicAdapterInformation(string adapterName)
        {
            if (GraphicAdapterMemoryInformation == null)
                try
                {
                    using (ManagementObjectSearcher objectSearcher = new ManagementObjectSearcher($"Select DeviceID, Description, AdapterRAM, AdapterDACType from Win32_VideoController where description=\"{adapterName}\""))
                        foreach (ManagementBaseObject display in objectSearcher.Get())
                        {
                            GraphicAdapterMemoryInformation = $"{(uint)display["AdapterRAM"] / 1024f / 1024:F0} MB {display["AdapterDACType"]} RAM";
                            break;
                        }
                }
                catch (Exception ex) when (ex is TypeInitializationException || ex is System.ComponentModel.Win32Exception)
                {
                    GraphicAdapterMemoryInformation = "n/a";
                }
            return GraphicAdapterMemoryInformation;
        }

        public static void WriteSystemDetails()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(CultureInfo.InvariantCulture, $"{"Date/Time",-12}= {DateTime.Now} ({DateTime.UtcNow:u})");
            try
            {
                WriteEnvironment(builder);
            }
            catch (Exception ex) when (ex is TypeInitializationException || ex is System.ComponentModel.Win32Exception)
            {
                builder.Append("Hardware information not available on this platform.");
            }
            builder.AppendLine(CultureInfo.InvariantCulture, $"{"Runtime",-12}= {RuntimeInformation.FrameworkDescription} ({(Environment.Is64BitProcess ? "64" : "32")}bit)");
            Trace.Write(builder.ToString());
        }

        private static void WriteEnvironment(StringBuilder output)
        {
            NativeStructs.MemoryStatusExtended buffer = new NativeStructs.MemoryStatusExtended { Size = 64 };
            NativeMethods.GlobalMemoryStatusEx(buffer);
            try
            {
                using (ManagementObjectSearcher objectSearcher = new ManagementObjectSearcher("Select Description, Manufacturer from Win32_BIOS"))
                    foreach (ManagementBaseObject bios in objectSearcher.Get())
                        output.AppendLine(CultureInfo.InvariantCulture, $"{"BIOS",-12}= {bios["Description"]} ({bios["Manufacturer"]})");
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            try
            {
                using (ManagementObjectSearcher objectSearcher = new ManagementObjectSearcher("Select DeviceID, Name, NumberOfLogicalProcessors, NumberOfCores, MaxClockSpeed, L2CacheSize, L3CacheSize from Win32_Processor"))
                    foreach (ManagementBaseObject processor in objectSearcher.Get())
                    {
                        CpuInformation = $"{"Processor",-12}= {processor["Name"]} ({(uint)processor["NumberOfLogicalProcessors"]} threads, {processor["NumberOfCores"]} cores, {(uint)processor["MaxClockSpeed"] / 1000f:F1} GHz, L2 Cache {processor["L2CacheSize"]:F0} KB, L3 Cache {processor["L3CacheSize"]:F0} KB)";
                        output.AppendLine(CpuInformation);
                    }
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            output.AppendLine(CultureInfo.InvariantCulture, $"{"Memory",-12}= {buffer.TotalPhysical / 1024f / 1024 / 1024:F1} GB");
            try
            {
                using (ManagementObjectSearcher objectSearcher = new ManagementObjectSearcher("Select DeviceID, Description, AdapterRAM, AdapterDACType from Win32_VideoController"))
                    foreach (ManagementBaseObject display in objectSearcher.Get())
                        output.AppendLine(CultureInfo.InvariantCulture, $"{"Video",-12}= {display["Description"]} ({(uint)display["AdapterRAM"] / 1024f / 1024 / 1024:F1} GB {display["AdapterDACType"]} RAM){GetPnPDeviceDrivers(display as ManagementObject)}");
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }

            foreach (GraphicsAdapter adapter in GraphicsAdapter.Adapters)
            {
                output.AppendLine(CultureInfo.InvariantCulture, $"{"Display",-12}= {adapter.DeviceName} (resolution {adapter.CurrentDisplayMode.Width} x {adapter.CurrentDisplayMode.Height}, {(adapter.IsDefaultAdapter ? ", primary" : "")} on {adapter.Description})");
                GraphicsAdapter.UseDebugLayers = true;
            }

            try
            {
                using (ManagementObjectSearcher objectSearcher = new ManagementObjectSearcher("Select DeviceID, Description from Win32_SoundDevice"))
                    foreach (ManagementBaseObject sound in objectSearcher.Get())
                        output.AppendLine(CultureInfo.InvariantCulture, $"{"Sound",-12}= {sound["Description"]}{GetPnPDeviceDrivers(sound as ManagementObject)}");
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            try
            {
                using (ManagementObjectSearcher objectSearcher = new ManagementObjectSearcher("Select Name, Description, FileSystem, Size, FreeSpace from Win32_LogicalDisk"))
                    foreach (ManagementBaseObject disk in objectSearcher.Get())
                        if (disk["Size"] != null)
                            output.AppendLine(CultureInfo.InvariantCulture, $"{"Disk",-12}= {disk["Name"]} ({disk["Description"]}, {disk["FileSystem"]}, {(ulong)(disk["Size"] ?? 0ul) / 1024f / 1024 / 1024:F1} GB, {(ulong)(disk["FreeSpace"] ?? 0ul) / 1024f / 1024 / 1024:F1} GB free)");
                        else
                            output.AppendLine(CultureInfo.InvariantCulture, $"{"Disk",-12}= {disk["Name"]} ({disk["Description"]})");
            }
            catch (ManagementException error)
            {
                Trace.WriteLine(error);
            }
            try
            {
                using (ManagementObjectSearcher objectSearcher = new ManagementObjectSearcher("Select Caption, OSArchitecture, Version from Win32_OperatingSystem"))
                    foreach (ManagementBaseObject os in objectSearcher.Get())
                        output.AppendLine(CultureInfo.InvariantCulture, $"{"OS",-12}= {os["Caption"]} {os["OSArchitecture"]} ({os["Version"]})");
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
                foreach (ManagementObject dataFile in pnpDevice.GetRelated("CIM_DataFile"))
                    output.Append(CultureInfo.InvariantCulture, $" ({dataFile["FileName"]} {dataFile["Version"]})");
            return output.ToString();
        }

        public static void OpenFile(string fileName)
        {
            //https://stackoverflow.com/questions/4580263/how-to-open-in-default-browser-in-c-sharp
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = fileName, UseShellExecute = true });
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", fileName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", fileName);
        }

#pragma warning disable CA1054 // URI-like parameters should not be strings
        public static void OpenBrowser(string url)
#pragma warning restore CA1054 // URI-like parameters should not be strings
        {
            //https://stackoverflow.com/questions/4580263/how-to-open-in-default-browser-in-c-sharp
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", url);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
        }
    }
}
