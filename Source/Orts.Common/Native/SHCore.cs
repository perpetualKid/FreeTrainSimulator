using System;
using System.Runtime.InteropServices;

namespace Orts.Common.Native
{
    public static partial class NativeMethods
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public enum PROCESS_DPI_AWARENESS
        {
            Process_DPI_Unaware = 0,
            Process_System_DPI_Aware = 1,
            Process_Per_Monitor_DPI_Aware = 2
        }
#pragma warning restore CA1707 // Identifiers should not contain underscores


        public static bool SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness)
        { try { return SetProcessDpiAwarenessNative(awareness); } catch (DllNotFoundException) { return false; } }
        [DllImport("SHCore.dll", EntryPoint = "SetProcessDpiAwareness", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool SetProcessDpiAwarenessNative(PROCESS_DPI_AWARENESS awareness);

        public static void GetProcessDpiAwareness(IntPtr hprocess, out PROCESS_DPI_AWARENESS awareness)
        { try { GetProcessDpiAwarenessNative(hprocess, out awareness); } catch (DllNotFoundException) { awareness = PROCESS_DPI_AWARENESS.Process_DPI_Unaware; } }
        [DllImport("SHCore.dll", EntryPoint = "GetProcessDpiAwareness", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern void GetProcessDpiAwarenessNative(IntPtr hprocess, out PROCESS_DPI_AWARENESS awareness);



    }
}
