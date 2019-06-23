using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ORTS.Common
{
    // TODO: This class and its methods should be internal visibility.
    /// <summary>
    /// Native methods for interacting with INI files.
    /// </summary>
	public static class NativeMethods
    {
        /// <summary>
        /// Retrieves all the keys and values for the specified section of an initialization file.
        /// </summary>
        /// <param name="sectionName">The name of the section in the initialization file.</param>
        /// <param name="value">A pointer to a buffer that receives the key name and value pairs associated with the named section. The buffer is filled with one or more null-terminated strings; the last string is followed by a second null character.</param>
        /// <param name="size">The size of the buffer pointed to by the <paramref name="value"/> parameter, in characters. The maximum profile section size is 32,767 characters.</param>
        /// <param name="fileName">The name of the initialization file. If this parameter does not contain a full path to the file, the system searches for the file in the Windows directory.</param>
        /// <returns>The return value specifies the number of characters copied to the buffer, not including the terminating null character. If the buffer is not large enough to contain all the key name and value pairs associated with the named section, the return value is equal to <paramref name="size"/> minus two.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetPrivateProfileSection(string sectionName, string value, int size, string fileName);

        /// <summary>
        /// Retrieves a string from the specified section in an initialization file.
        /// </summary>
        /// <param name="sectionName">The name of the section containing the key name. If this parameter is <c>null</c>, the <see cref="GetPrivateProfileString"/> function copies all section names in the file to the supplied buffer.</param>
        /// <param name="keyName">The name of the key whose associated string is to be retrieved. If this parameter is <c>null</c>, all key names in the section specified by the <paramref name="sectionName"/> parameter are copied to the buffer specified by the <paramref name="value"/> parameter.</param>
        /// <param name="defaultValue">A default string. If the <paramref name="keyName"/> key cannot be found in the initialization file, <see cref="GetPrivateProfileString"/> copies the default string to the <paramref name="value"/> buffer. If this parameter is <c>null</c>, the default is an empty string, <c>""</c>.
        /// Avoid specifying a default string with trailing blank characters. The function inserts a <c>null</c> character in the <paramref name="value"/> buffer to strip any trailing blanks.</param>
        /// <param name="value">A pointer to the buffer that receives the retrieved string. </param>
        /// <param name="size">The size of the buffer pointed to by the <paramref name="value"/> parameter, in characters.</param>
        /// <param name="fileName">The name of the initialization file. If this parameter does not contain a full path to the file, the system searches for the file in the Windows directory.</param>
        /// <returns>The return value is the number of characters copied to the buffer, not including the terminating <c>null</c> character.
        /// If neither <paramref name="sectionName"/> nor <paramref name="keyName"/> is <c>null</c> and the supplied destination buffer is too small to hold the requested string, the string is truncated and followed by a <c>null</c> character, and the return value is equal to <paramref name="size"/> minus one.
        /// If either <paramref name="sectionName"/> or <paramref name="keyName"/> is <c>null</c> and the supplied destination buffer is too small to hold all the strings, the last string is truncated and followed by two <c>null</c> characters. In this case, the return value is equal to <paramref name="size"/> minus two.
        /// In the event the initialization file specified by <paramref name="fileName"/> is not found, or contains invalid values, this function will set errorno with a value of '0x2' (File Not Found). To retrieve extended error information, call GetLastError.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetPrivateProfileString(string sectionName, string keyName, string defaultValue, string value, int size, string fileName);

        /// <summary>
        /// Retrieves a string from the specified section in an initialization file.
        /// </summary>
        /// <param name="sectionName">The name of the section containing the key name. If this parameter is <c>null</c>, the <see cref="GetPrivateProfileString"/> function copies all section names in the file to the supplied buffer.</param>
        /// <param name="keyName">The name of the key whose associated string is to be retrieved. If this parameter is <c>null</c>, all key names in the section specified by the <paramref name="sectionName"/> parameter are copied to the buffer specified by the <paramref name="value"/> parameter.</param>
        /// <param name="defaultValue">A default string. If the <paramref name="keyName"/> key cannot be found in the initialization file, <see cref="GetPrivateProfileString"/> copies the default string to the <paramref name="value"/> buffer. If this parameter is <c>null</c>, the default is an empty string, <c>""</c>.
        /// Avoid specifying a default string with trailing blank characters. The function inserts a <c>null</c> character in the <paramref name="value"/> buffer to strip any trailing blanks.</param>
        /// <param name="value">A pointer to the buffer that receives the retrieved string. </param>
        /// <param name="size">The size of the buffer pointed to by the <paramref name="value"/> parameter, in characters.</param>
        /// <param name="fileName">The name of the initialization file. If this parameter does not contain a full path to the file, the system searches for the file in the Windows directory.</param>
        /// <returns>The return value is the number of characters copied to the buffer, not including the terminating <c>null</c> character.
        /// If neither <paramref name="sectionName"/> nor <paramref name="keyName"/> is <c>null</c> and the supplied destination buffer is too small to hold the requested string, the string is truncated and followed by a <c>null</c> character, and the return value is equal to <paramref name="size"/> minus one.
        /// If either <paramref name="sectionName"/> or <paramref name="keyName"/> is <c>null</c> and the supplied destination buffer is too small to hold all the strings, the last string is truncated and followed by two <c>null</c> characters. In this case, the return value is equal to <paramref name="size"/> minus two.
        /// In the event the initialization file specified by <paramref name="fileName"/> is not found, or contains invalid values, this function will set errorno with a value of '0x2' (File Not Found). To retrieve extended error information, call GetLastError.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetPrivateProfileString(string sectionName, string keyName, string defaultValue, StringBuilder value, int size, string fileName);

        /// <summary>
        /// Copies a string into the specified section of an initialization file.
        /// </summary>
        /// <param name="sectionName">The name of the section to which the string will be copied. If the section does not exist, it is created. The name of the section is case-independent; the string can be any combination of uppercase and lowercase letters.</param>
        /// <param name="keyName">The name of the key to be associated with a string. If the key does not exist in the specified section, it is created. If this parameter is <c>null</c>, the entire section, including all entries within the section, is deleted.</param>
        /// <param name="value">A <c>null</c>-terminated string to be written to the file. If this parameter is <c>null</c>, the key pointed to by the lpKeyName parameter is deleted. </param>
        /// <param name="fileName">The name of the initialization file.
        /// If the file was created using Unicode characters, the function writes Unicode characters to the file. Otherwise, the function writes ANSI characters.</param>
        /// <returns>If the function successfully copies the string to the initialization file, the return value is nonzero.
        /// If the function fails, or if it flushes the cached version of the most recently accessed initialization file, the return value is zero. To get extended error information, call GetLastError.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int WritePrivateProfileString(string sectionName, string keyName, string value, string fileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] NativeStructs.MemoryStatusExtended buffer);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetDllDirectory(string pathName);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [Flags]
        public enum GgiFlags : uint
        {
            None = 0,
            MarkNonexistingGlyphs = 1,
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetGlyphIndices(IntPtr hdc, string text, int textLength, [Out] short[] indices, GgiFlags flags);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool GetCharABCWidthsFloat(IntPtr hdc, uint firstChar, uint lastChar, out NativeStructs.AbcFloatWidth abcFloatWidths);

    }


    public static class NativeStructs
    {
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        public class MemoryStatusExtended
        {
            public uint Size;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

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
