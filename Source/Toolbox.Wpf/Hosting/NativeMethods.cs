using System;
using System.Runtime.InteropServices;

namespace FreeTrainSimulator.Toolbox.Wpf.Hosting
{
    internal static class NativeMethods
    {
        internal const int GwlStyle = -16;
        internal const uint WsVisible = 0x10000000;
        internal const uint WsChild = 0x40000000;
        internal const uint WsPopup = 0x80000000;
        internal const uint WsCaption = 0x00C00000;
        internal const uint WsThickFrame = 0x00040000;
        internal const uint WsMinimize = 0x20000000;
        internal const uint WsMaximize = 0x01000000;
        internal const uint WsSysMenu = 0x00080000;

        internal const uint SwpNoZOrder = 0x0004;
        internal const uint SwpNoActivate = 0x0010;
        internal const uint SwpFrameChanged = 0x0020;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        internal static IntPtr GetWindowStyle(IntPtr handle)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(handle, GwlStyle)
                : new IntPtr(GetWindowLong32(handle, GwlStyle));
        }

        internal static void SetWindowStyle(IntPtr handle, IntPtr style)
        {
            if (IntPtr.Size == 8)
                _ = SetWindowLongPtr64(handle, GwlStyle, style);
            else
                _ = SetWindowLong32(handle, GwlStyle, style.ToInt32());
        }
    }
}
