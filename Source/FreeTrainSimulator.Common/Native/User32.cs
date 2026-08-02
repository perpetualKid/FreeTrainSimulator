using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FreeTrainSimulator.Common.Native
{
    public partial class NativeMethods
    {
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
        /// <summary>
        /// Lock or relase a window for updating.
        /// </summary>
        public static int LockWindowUpdate(IntPtr hwnd)
        { return LockWindowUpdateNative(hwnd); }
        [DllImport("user32", EntryPoint = "LockWindowUpdate", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
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
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int MapVirtualKeyNative(int code, MapVirtualKeyType type);

        public static int GetKeyNameText(int scanCode, [Out] StringBuilder name, int length)
        { return GetKeyNameTextNative(scanCode, name, length); }
        [DllImport("user32.dll", EntryPoint = "GetKeyNameText", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
        private static extern int GetKeyNameTextNative(int scanCode, [Out] StringBuilder name, int length);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes

        public static IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref CharFormat2 lParam)
        { return SendMessageNative(hwnd, msg, wParam, ref lParam); }
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr SendMessageNative(IntPtr hwnd, int msg, IntPtr wParam, ref CharFormat2 lParam);

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
        public struct CharFormat2
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible
        {
            public int Size;
            public int Mask;
            public int Effects;
            public int Height;
            public int Offset;
            public int TextColor;
            public byte CharSet;
            public byte PitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FaceName;
            public short Weight;
            public short Spacing;
            public int BackColor;
            public int Lcid;
            public int Reserved;
            public short Style;
            public short Kerning;
            public byte UnderlineType;
            public byte Animation;
            public byte RevAuthor;
            public byte Reserved1;
        }

        public static IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam)
        { return SendMessageNative(hwnd, msg, wParam, lParam); }
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr SendMessageNative(IntPtr hWnd, int msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        public delegate IntPtr KeyboardProcedure(int nCode, IntPtr wParam, IntPtr lParam);

        public static IntPtr SetWindowsHookEx(int idHook, KeyboardProcedure lpfn, IntPtr hMod, uint dwThreadId)
        { return SetWindowsHookExNative(idHook, lpfn, hMod, dwThreadId); }
        [DllImport("user32.dll", EntryPoint = "SetWindowsHookEx", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr SetWindowsHookExNative(int idHook, KeyboardProcedure lpfn, IntPtr hMod, uint dwThreadId);

        public static bool UnhookWindowsHookEx(IntPtr hhk)
        { return UnhookWindowsHookExNative(hhk); }
        [DllImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool UnhookWindowsHookExNative(IntPtr hhk);

        public static IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam)
        { return CallNextHookExNative(hhk, nCode, wParam, lParam); }
        [DllImport("user32.dll", EntryPoint = "CallNextHookEx", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr CallNextHookExNative(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        public static uint GetDpiForWindow([In] IntPtr hwnd)
        { return GetDpiForWindowNative(hwnd); }
        [DllImport("User32.dll", EntryPoint = "GetDpiForWindow", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetDpiForWindowNative([In] IntPtr hwnd);

        public const int GwlStyle = -16;
        public const uint WsVisible = 0x10000000;
        public const uint WsChild = 0x40000000;
        public const uint WsPopup = 0x80000000;
        public const uint WsCaption = 0x00C00000;
        public const uint WsThickFrame = 0x00040000;
        public const uint WsMinimize = 0x20000000;
        public const uint WsMaximize = 0x01000000;
        public const uint WsSysMenu = 0x00080000;

        public const uint SwpNoZOrder = 0x0004;
        public const uint SwpNoActivate = 0x0010;
        public const uint SwpFrameChanged = 0x0020;

        public static IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent)
        { return SetParentNative(hWndChild, hWndNewParent); }
        [DllImport("user32.dll", EntryPoint = "SetParent", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr SetParentNative(IntPtr hWndChild, IntPtr hWndNewParent);

        public static int GetWindowLong32(IntPtr hWnd, int nIndex)
        { return GetWindowLong32Native(hWnd, nIndex); }
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32Native(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex)
        { return GetWindowLongPtr64Native(hWnd, nIndex); }
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64Native(IntPtr hWnd, int nIndex);

        public static int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong)
        { return SetWindowLong32Native(hWnd, nIndex, dwNewLong); }
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32Native(IntPtr hWnd, int nIndex, int dwNewLong);

        public static IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        { return SetWindowLongPtr64Native(hWnd, nIndex, dwNewLong); }
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64Native(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint)
        { return MoveWindowNative(hWnd, x, y, nWidth, nHeight, bRepaint); }
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", EntryPoint = "MoveWindow", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveWindowNative(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        public static bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags)
        { return SetWindowPosNative(hWnd, hWndInsertAfter, x, y, cx, cy, uFlags); }
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPosNative(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        public static IntPtr GetWindowStyle(IntPtr handle)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(handle, GwlStyle)
                : new IntPtr(GetWindowLong32(handle, GwlStyle));
        }

        public static void SetWindowStyle(IntPtr handle, IntPtr style)
        {
            _ = IntPtr.Size == 8 ? SetWindowLongPtr64(handle, GwlStyle, style) : SetWindowLong32(handle, GwlStyle, style.ToInt32());
        }

        public const int SwShowNormal = 1;
        public const int SwShowMinimized = 2;
        public const int SwShowMaximized = 3;

        /// <summary>
        /// Managed projection of the native WINDOWPLACEMENT structure. The POINT and RECT members are flattened
        /// into individual integer fields so the layout matches the native structure exactly without depending
        /// on any external point/rectangle type, which also keeps it trivially serializable.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1815 // Override equals and operator equals on value types
        public struct WindowPlacement
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1034 // Nested types should not be visible
        {
            public int Length;
            public int Flags;
            public int ShowCommand;
            public int MinPositionX;
            public int MinPositionY;
            public int MaxPositionX;
            public int MaxPositionY;
            public int NormalPositionLeft;
            public int NormalPositionTop;
            public int NormalPositionRight;
            public int NormalPositionBottom;
        }

        public static bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement placement)
        {
            placement.Length = Marshal.SizeOf<WindowPlacement>();
            return SetWindowPlacementNative(hWnd, ref placement);
        }
        [DllImport("user32.dll", EntryPoint = "SetWindowPlacement", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPlacementNative(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

        public static bool GetWindowPlacement(IntPtr hWnd, out WindowPlacement placement)
        {
            placement = default;
            placement.Length = Marshal.SizeOf<WindowPlacement>();
            return GetWindowPlacementNative(hWnd, ref placement);
        }
        [DllImport("user32.dll", EntryPoint = "GetWindowPlacement", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacementNative(IntPtr hWnd, ref WindowPlacement lpwndpl);

        /// <summary>
        /// Returns whether the foreground window belongs to the current process.
        /// </summary>
        public static bool IsForegroundWindowOwnedByCurrentProcess()
        {
            IntPtr foregroundWindow = GetForegroundWindowNative();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            _ = GetWindowThreadProcessIdNative(foregroundWindow, out int processId);
            return processId == Environment.ProcessId;
        }

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr GetForegroundWindowNative();

        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int GetWindowThreadProcessIdNative(IntPtr hWnd, out int processId);
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    }
}
