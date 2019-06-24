using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ORTS.Common.Native
{
    public static partial class NativeMethods
    {

        public static IntPtr CreateCompatibleDC(IntPtr hdc)
        { return CreateCompatibleDCNative(hdc); }
        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDCNative(IntPtr hdc);

        public static IntPtr SelectObject(IntPtr hdc, IntPtr hObject)
        { return SelectObjectNative(hdc, hObject); }
        [DllImport("gdi32.dll", EntryPoint = "SelectObject", SetLastError = true)]
        private static extern IntPtr SelectObjectNative(IntPtr hdc, IntPtr hObject);

        public static bool DeleteDC(IntPtr hdc)
        { return DeleteDCNative(hdc); }
        [DllImport("gdi32.dll", EntryPoint = "DeleteDC", SetLastError = true)]
        private static extern bool DeleteDCNative(IntPtr hdc);

        [Flags]
        public enum GgiFlags : uint
        {
            None = 0,
            MarkNonexistingGlyphs = 1,
        }

        public static uint GetGlyphIndices(IntPtr hdc, string text, int textLength, [Out] short[] indices, GgiFlags flags)
        { return GetGlyphIndicesNative(hdc, text, textLength, indices, flags); }
        [DllImport("gdi32.dll", EntryPoint = "GetGlyphIndices", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetGlyphIndicesNative(IntPtr hdc, string text, int textLength, [Out] short[] indices, GgiFlags flags);

        public static bool GetCharABCWidthsFloat(IntPtr hdc, uint firstChar, uint lastChar, out NativeStructs.AbcFloatWidth abcFloatWidths)
        { return GetCharABCWidthsFloatNative(hdc, firstChar, lastChar, out abcFloatWidths); }
        [DllImport("gdi32.dll", EntryPoint = "GetCharABCWidthsFloat", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetCharABCWidthsFloatNative(IntPtr hdc, uint firstChar, uint lastChar, out NativeStructs.AbcFloatWidth abcFloatWidths);

    }


    public static partial class NativeStructs
    {
        [DebuggerDisplay("{First} + {Second} = {Amount}")]
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct KerningPair
        {
            public char First;
            public char Second;
            public int Amount;
        }

        [DebuggerDisplay("{A} + {B} + {C}")]
        [StructLayout(LayoutKind.Sequential)]
        public struct AbcFloatWidth
        {
            public float A;
            public float B;
            public float C;
        }
    }

}
