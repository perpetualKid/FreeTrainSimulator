using System;
using System.Runtime.InteropServices;

namespace Orts.Common.Native
{
    public partial class NativeMethods
    {
        /// <summary>
        /// Lock or relase a window for updating.
        /// </summary>
        public static int LockWindowUpdate(IntPtr hwnd)
        { return LockWindowUpdateNative(hwnd); }
        [DllImport("user32", EntryPoint = "LockWindowUpdate", SetLastError = true)]
        private static extern int LockWindowUpdateNative(IntPtr hwnd);

        public enum MapVirtualKeyType
        {
            VirtualToCharacter = 2,
            VirtualToScan = 0,
            VirtualToScanEx = 4,
            ScanToVirtual = 1,
            ScanToVirtualEx = 3,
        }

        public static int MapVirtualKey(int code, MapVirtualKeyType type)
        { return MapVirtualKeyNative(code, type); }
        [DllImport("user32.dll", EntryPoint = "MapVirtualKey", SetLastError = true)]
        private static extern int MapVirtualKeyNative(int code, MapVirtualKeyType type);

        public static int GetKeyNameText(int scanCode, [Out] string name, int length)
        { return GetKeyNameTextNative(scanCode, name, length); }
        [DllImport("user32.dll", EntryPoint = "GetKeyNameText", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetKeyNameTextNative(int scanCode, [Out] string name, int length);


    }
}
