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
        { return SetProcessDpiAwarenessNative(awareness); }
        [DllImport("SHCore.dll", EntryPoint = "SetProcessDpiAwareness", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessNative(PROCESS_DPI_AWARENESS awareness);

        public static void GetProcessDpiAwareness(IntPtr hprocess, out PROCESS_DPI_AWARENESS awareness)
        { GetProcessDpiAwarenessNative(hprocess, out awareness); }
        [DllImport("SHCore.dll", EntryPoint = "GetProcessDpiAwareness", SetLastError = true)]
        private static extern void GetProcessDpiAwarenessNative(IntPtr hprocess, out PROCESS_DPI_AWARENESS awareness);



    }
}
