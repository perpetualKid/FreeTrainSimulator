// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using FreeTrainSimulator.Common.Native;

namespace Orts.ActivityRunner.Viewer3D
{
    /// <summary>
    /// Wrapper class for the externals of library OpenAL
    /// </summary>
    public static class OpenAL
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const int AL_NONE = 0;
        public const int AL_FALSE = 0;
        public const int AL_TRUE = 1;

        public const int AL_BUFFER = 0x1009;
        public const int AL_BUFFERS_QUEUED = 0x1015;
        public const int AL_BUFFERS_PROCESSED = 0x1016;
        public const int AL_PLAYING = 0x1012;
        public const int AL_SOURCE_STATE = 0x1010;
        public const int AL_SOURCE_TYPE = 0x1027;
        public const int AL_LOOPING = 0x1007;
        public const int AL_GAIN = 0x100a;
        public const int AL_VELOCITY = 0x1006;
        public const int AL_ORIENTATION = 0x100f;
        public const int AL_DISTANCE_MODEL = 0xd000;
        public const int AL_INVERSE_DISTANCE = 0xd001;
        public const int AL_INVERSE_DISTANCE_CLAMPED = 0xd002;
        public const int AL_LINEAR_DISTANCE = 0xd003;
        public const int AL_LINEAR_DISTANCE_CLAMPED = 0xd004;
        public const int AL_EXPONENT_DISTANCE = 0xd005;
        public const int AL_EXPONENT_DISTANCE_CLAMPED = 0xd006;
        public const int AL_MAX_DISTANCE = 0x1023;
        public const int AL_REFERENCE_DISTANCE = 0x1020;
        public const int AL_ROLLOFF_FACTOR = 0x1021;
        public const int AL_PITCH = 0x1003;
        public const int AL_POSITION = 0x1004;
        public const int AL_DIRECTION = 0x1005;
        public const int AL_SOURCE_RELATIVE = 0x0202;
        public const int AL_FREQUENCY = 0x2001;
        public const int AL_BITS = 0x2002;
        public const int AL_CHANNELS = 0x2003;
        public const int AL_BYTE_OFFSET = 0x1026;
        public const int AL_MIN_GAIN = 0x100d;
        public const int AL_MAX_GAIN = 0x100e;
        public const int AL_VENDOR = 0xb001;
        public const int AL_VERSION = 0xb002;
        public const int AL_RENDERER = 0xb003;
        public const int AL_DOPPLER_FACTOR = 0xc000;
        public const int AL_LOOP_POINTS_SOFT = 0x2015;
        public const int AL_STATIC = 0x1028;
        public const int AL_STREAMING = 0x1029;
        public const int AL_UNDETERMINED = 0x1030;

        public const int AL_FORMAT_MONO8 = 0x1100;
        public const int AL_FORMAT_MONO16 = 0x1101;
        public const int AL_FORMAT_STEREO8 = 0x1102;
        public const int AL_FORMAT_STEREO16 = 0x1103;

        public const int ALC_DEFAULT_DEVICE_SPECIFIER = 0x1004;
        public const int ALC_DEVICE_SPECIFIER = 0x1005;

        public const int AL_NO_ERROR = 0;
        public const int AL_INVALID = -1;
        public const int AL_INVALID_NAME = 0xa001; // 40961
        public const int AL_INVALID_ENUM = 0xa002; // 40962
        public const int AL_INVALID_VALUE = 0xa003; // 40963
        public const int AL_INVALID_OPERATION = 0xa004; // 40964
        public const int AL_OUT_OF_MEMORY = 0xa005; // 40965

        public const int AL_AUXILIARY_SEND_FILTER = 0x20006;

        public const int AL_FILTER_NULL = 0x0000;

        public const int AL_EFFECTSLOT_NULL = 0x0000;
        public const int AL_EFFECTSLOT_EFFECT = 0x0001;
        public const int AL_EFFECTSLOT_GAIN = 0x0002;
        public const int AL_EFFECTSLOT_AUXILIARY_SEND_AUTO = 0x0003;

        public const int AL_EFFECT_TYPE = 0x8001;
        public const int AL_EFFECT_REVERB = 0x0001;
        public const int AL_EFFECT_ECHO = 0x0004;
        public const int AL_EFFECT_PITCH_SHIFTER = 0x0008;
        public const int AL_EFFECT_EAXREVERB = 0x8000;

        public const int AL_ECHO_DELAY = 0x0001;
        public const int AL_ECHO_LRDELAY = 0x0002;
        public const int AL_ECHO_DAMPING = 0x0003;
        public const int AL_ECHO_FEEDBACK = 0x0004;
        public const int AL_ECHO_SPREAD = 0x0005;

        public const int AL_REVERB_DENSITY = 0x0001;
        public const int AL_REVERB_DIFFUSION = 0x0002;
        public const int AL_REVERB_GAIN = 0x0003;
        public const int AL_REVERB_GAINHF = 0x0004;
        public const int AL_REVERB_DECAY_TIME = 0x0005;
        public const int AL_REVERB_DECAY_HFRATIO = 0x0006;
        public const int AL_REVERB_REFLECTIONS_GAIN = 0x0007;
        public const int AL_REVERB_REFLECTIONS_DELAY = 0x0008;
        public const int AL_REVERB_LATE_REVERB_GAIN = 0x0009;
        public const int AL_REVERB_LATE_REVERB_DELAY = 0x000a;
        public const int AL_REVERB_AIR_ABSORPTION_GAINHF = 0x000b;
        public const int AL_REVERB_ROOM_ROLLOFF_FACTOR = 0x000c;
        public const int AL_REVERB_DECAY_HFLIMIT = 0x000d;

        public const int AL_EAXREVERB_DENSITY = 0x0001;
        public const int AL_EAXREVERB_DIFFUSION = 0x0002;
        public const int AL_EAXREVERB_GAIN = 0x0003;
        public const int AL_EAXREVERB_GAINHF = 0x0004;
        public const int AL_EAXREVERB_GAINLF = 0x0005;
        public const int AL_EAXREVERB_DECAY_TIME = 0x0006;
        public const int AL_EAXREVERB_DECAY_HFRATIO = 0x0007;
        public const int AL_EAXREVERB_DECAY_LFRATIO = 0x0008;
        public const int AL_EAXREVERB_REFLECTIONS_GAIN = 0x0009;
        public const int AL_EAXREVERB_REFLECTIONS_DELAY = 0x000a;
        public const int AL_EAXREVERB_REFLECTIONS_PAN = 0x000b;
        public const int AL_EAXREVERB_LATE_REVERB_GAIN = 0x000c;
        public const int AL_EAXREVERB_LATE_REVERB_DELAY = 0x000d;
        public const int AL_EAXREVERB_LATE_REVERB_PAN = 0x000e;
        public const int AL_EAXREVERB_ECHO_TIME = 0x000f;
        public const int AL_EAXREVERB_ECHO_DEPTH = 0x0010;
        public const int AL_EAXREVERB_MODULATION_TIME = 0x0011;
        public const int AL_EAXREVERB_MODULATION_DEPTH = 0x0012;
        public const int AL_EAXREVERB_AIR_ABSORPTION_GAINHF = 0x0013;
        public const int AL_EAXREVERB_HFREFERENCE = 0x0014;
        public const int AL_EAXREVERB_LFREFERENCE = 0x0015;
        public const int AL_EAXREVERB_ROOM_ROLLOFF_FACTOR = 0x0016;
        public const int AL_EAXREVERB_DECAY_HFLIMIT = 0x0017;
#pragma warning restore CA1707 // Identifiers should not contain underscores

        [DllImport("soft_oal.dll", EntryPoint = "alcOpenDevice", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern IntPtr OpenDevice(string deviceName);
        [DllImport("soft_oal.dll", EntryPoint = "alcCreateContext", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern IntPtr CreateContext(IntPtr device, int[] attribute);
        [DllImport("soft_oal.dll", EntryPoint = "alcMakeContextCurrent", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int MakeContextCurrent(IntPtr context);
        [DllImport("soft_oal.dll", EntryPoint = "alcGetString", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern string GetString(IntPtr device, int attribute);
        [DllImport("soft_oal.dll", EntryPoint = "alcIsExtensionPresent", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int IsExtensionPresent(IntPtr device, string extensionName);

        [DllImport("soft_oal.dll", EntryPoint = "AlInitialize", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern string Initialize(string devName);
        [DllImport("soft_oal.dll", EntryPoint = "alIsExtensionPresent", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int IsExtensionPresent(string extensionName);
        [DllImport("soft_oal.dll", EntryPoint = "alGetBufferi", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GetBufferi(int buffer, int attribute, out int val);
        [DllImport("soft_oal.dll", EntryPoint = "alGetString", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern IntPtr GetString(int state);
        [DllImport("soft_oal.dll", EntryPoint = "alGetError", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int GetError();
        [DllImport("soft_oal.dll", EntryPoint = "alDeleteBuffers", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void DeleteBuffers(int number, [In] ref int buffer);
        [DllImport("soft_oal.dll", EntryPoint = "alDeleteBuffers", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void DeleteBuffers(int number, int[] buffers);
        [DllImport("soft_oal.dll", EntryPoint = "alDeleteSources", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void DeleteSources(int number, [In] int[] sources);
        [DllImport("soft_oal.dll", EntryPoint = "alDeleteSources", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void DeleteSources(int number, [In] ref int sources);
        [DllImport("soft_oal.dll", EntryPoint = "alDistanceModel", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void DistanceModel(int model);
        [DllImport("soft_oal.dll", EntryPoint = "alGenSources", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GenSources(int number, out int source);
        [DllImport("soft_oal.dll", EntryPoint = "alGetSourcei", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GetSourcei(int source, int attribute, out int val);
        [DllImport("soft_oal.dll", EntryPoint = "alGetSourcef", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GetSourcef(int source, int attribute, out float val);
        [DllImport("soft_oal.dll", EntryPoint = "alGetSource3f", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GetSource3f(int source, int attribute, out float value1, out float value2, out float value3);
        [DllImport("soft_oal.dll", EntryPoint = "alListener3f", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Listener3f(int attribute, float value1, float value2, float value3);
        [DllImport("soft_oal.dll", EntryPoint = "alListenerfv", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Listenerfv(int attribute, [In] float[] values);
        [DllImport("soft_oal.dll", EntryPoint = "alListenerf", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Listenerf(int attribute, float value);
        [DllImport("soft_oal.dll", EntryPoint = "alGetListener3f", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GetListener3f(int attribute, out float value1, out float value2, out float value3);
        [DllImport("soft_oal.dll", EntryPoint = "alSourcePlay", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void SourcePlay(int source);
        [DllImport("soft_oal.dll", EntryPoint = "alSourceRewind", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void SourceRewind(int source);
        [DllImport("soft_oal.dll", EntryPoint = "alSourceQueueBuffers", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void SourceQueueBuffers(int source, int number, [In] ref int buffer);
        [DllImport("soft_oal.dll", EntryPoint = "alSourcei", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Sourcei(int source, int attribute, int val);
        [DllImport("soft_oal.dll", EntryPoint = "alSource3i", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Source3i(int source, int attribute, int value1, int value2, int value3);
        [DllImport("soft_oal.dll", EntryPoint = "alSourcef", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Sourcef(int source, int attribute, float val);
        [DllImport("soft_oal.dll", EntryPoint = "alSource3f", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Source3f(int source, int attribute, float value1, float value2, float value3);
        [DllImport("soft_oal.dll", EntryPoint = "alSourcefv", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Sourcefv(int source, int attribute, [In] float[] values);
        [DllImport("soft_oal.dll", EntryPoint = "alSourceStop", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void SourceStop(int source);
        [DllImport("soft_oal.dll", EntryPoint = "alSourceUnqueueBuffers", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void SourceUnqueueBuffers(int source, int number, int[] buffers);
        [DllImport("soft_oal.dll", EntryPoint = "alSourceUnqueueBuffers", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void SourceUnqueueBuffers(int source, int number, ref int buffers);
        [DllImport("soft_oal.dll", EntryPoint = "alGetEnumValue", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern int GetEnumValue(string enumName);
        [DllImport("soft_oal.dll", EntryPoint = "alGenBuffers", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GenBuffers(int number, out int buffer);
        [DllImport("soft_oal.dll", EntryPoint = "alBufferData", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void BufferData(int buffer, int format, [In] byte[] data, int size, int frequency);
        [DllImport("soft_oal.dll", EntryPoint = "alBufferiv", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Bufferiv(int buffer, int attribute, [In] int[] values);
        [DllImport("soft_oal.dll", EntryPoint = "alIsSource", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern bool IsSource(int source);
        [DllImport("soft_oal.dll", EntryPoint = "alGenAuxiliaryEffectSlots", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GenAuxiliaryEffectSlots(int number, out int effectslot);
        [DllImport("soft_oal.dll", EntryPoint = "alAuxiliaryEffectSloti", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void AuxiliaryEffectSloti(int effectslot, int attribute, int val);
        [DllImport("soft_oal.dll", EntryPoint = "alGenEffects", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void GenEffects(int number, out int effect);
        [DllImport("soft_oal.dll", EntryPoint = "alEffecti", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Effecti(int effect, int attribute, int val);
        [DllImport("soft_oal.dll", EntryPoint = "alEffectf", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Effectf(int effect, int attribute, float val);
        [DllImport("soft_oal.dll", EntryPoint = "alEffectfv", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern void Effectfv(int effect, int attribute, [In] float[] values);

        public static int SourceUnqueueBuffer(int SoundSourceID)
        {
            int bufid = 0;
            SourceUnqueueBuffers(SoundSourceID, 1, ref bufid);
            return bufid;
        }

        public static string GetErrorString(int error)
        {
            return error switch
            {
                AL_INVALID_ENUM => "Invalid Enumeration",
                AL_INVALID_NAME => "Invalid Name",
                AL_INVALID_OPERATION => "Invalid Operation",
                AL_INVALID_VALUE => "Invalid Value",
                AL_OUT_OF_MEMORY => "Out Of Memory",
                AL_NO_ERROR => "No Error",
                _ => string.Empty,
            };
        }

        public static void Initialize()
        {
            CheckMaxSourcesConfig();
            //if (alcIsExtensionPresent(IntPtr.Zero, "ALC_ENUMERATION_EXT") == AL_TRUE)
            //{
            //    string deviceList = alcGetString(IntPtr.Zero, ALC_DEVICE_SPECIFIER);
            //    string[] split = deviceList.Split('\0');
            //    Trace.TraceInformation("___devlist {0}",deviceList);
            //}
            int[] attribs = Array.Empty<int>();
            IntPtr device = OpenDevice(null);
            IntPtr context = CreateContext(device, attribs);
            _ = MakeContextCurrent(context);

            // Note: Must use custom marshalling here because the returned strings must NOT be automatically deallocated by runtime.
            Trace.TraceInformation("Initialized OpenAL {0}; device '{1}' by '{2}'", Marshal.PtrToStringAnsi(GetString(AL_VERSION)), Marshal.PtrToStringAnsi(GetString(AL_RENDERER)), Marshal.PtrToStringAnsi(GetString(AL_VENDOR)));
        }

        /// <summary>
        /// checking and if necessary updating the maximum number of sound sources possible with OpenAL to be loaded
        /// OpenAL has a limit of 256 sources in code, but higher values can be configured through alsoft.ini-file read from %AppData%\Roaming folder
        /// As some dense routes in OR can have more than 256 sources, we provide a new default limit of 1024 sources
        /// ini-file format is following standard text based ini-files with sections and key/value pairs
        /// [General]
        /// sources=# of sound source
        /// </summary>
        private static void CheckMaxSourcesConfig()
        {
            string configFile = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "alsoft.ini");
            try
            {
                StringBuilder result = new StringBuilder(255);
                if (NativeMethods.GetPrivateProfileString("General", "sources", string.Empty, result, 255, configFile) > 0)
                {
                    if (int.TryParse(result.ToString(), out int sources))
                    {
                        if (sources < 1024)
                        {
                            NativeMethods.WritePrivateProfileString("General", "sources", "1024", configFile);
                        }
                    }
                }
                else
                {
                    NativeMethods.WritePrivateProfileString("General", "sources", "1024", configFile);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Trace.TraceError("Couldn't check or set OpenAL max sound sources in %AppData%\\Roaming\\alsoft.ini: ", ex.Message);
            }
        }
    }

    ///// <summary>
    ///// WAVEFILEHEADER binary structure
    ///// </summary>
    //[StructLayout(LayoutKind.Explicit, Pack = 1)]
    //public struct WAVEFILEHEADER
    //{
    //    [FieldOffset(0), MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    //    public char[] szRIFF;
    //    [FieldOffset(4), MarshalAs(UnmanagedType.U4, SizeConst = 4)]
    //    public uint ulRIFFSize;
    //    [FieldOffset(8), MarshalAs(UnmanagedType.U4, SizeConst = 4)]
    //    public uint padding;
    //}

    /// <summary>
    /// WAVEFILEHEADER binary structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct WAVEFILEHEADER
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] szRIFF;
        [MarshalAs(UnmanagedType.U4)]
        public uint ulRIFFSize;
        [MarshalAs(UnmanagedType.U4)]
        public uint padding;
    }

    /// <summary>
    /// RIFFCHUNK binary structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct RIFFCHUNK
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] szChunkName;
        [MarshalAs(UnmanagedType.U4)]
        public uint ulChunkSize;
    }

    /// <summary>
    /// WAVEFORMATEX binary structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct WAVEFORMATEX
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    /// <summary>
    /// WAVEFORMATEXTENSIBLE binary structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct WAVEFORMATEXTENSIBLE
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public WAVEFORMATEX Format;
        public ushort wValidBitsPerSample;
        public uint dwChannelMask;
        public Guid SubFormat;
    }

    /// <summary>
    /// CUECHUNK binary structure
    /// Describes the CUE chunk list of a wave file
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct CUECHUNK
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] szChunkName;
        [MarshalAs(UnmanagedType.U4)]
        public uint ulChunkSize;
        [MarshalAs(UnmanagedType.U4)]
        public uint ulNumCuePts;
    }

    /// <summary>
    /// CUEPT binary structure
    /// Describes one CUE point in CUE list
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct CUEPT
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public uint ulID;
        public uint ulPlayPos;
        public uint ulRiffID;
        public uint ulChunkStart;
        public uint ulBlockStart;
        public uint ulByteStart;
    }

    /// <summary>
    /// SMPLCHUNK binary structure
    /// Describes the SMPL chunk list of a wave file
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct SMPLCHUNK
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] ChunkName;
        [MarshalAs(UnmanagedType.U4)]
        public uint ChunkSize;
        [MarshalAs(UnmanagedType.U4)]
        public uint Manufacturer;
        [MarshalAs(UnmanagedType.U4)]
        public uint Product;
        [MarshalAs(UnmanagedType.U4)]
        public uint SmplPeriod;
        [MarshalAs(UnmanagedType.U4)]
        public uint MIDIUnityNote;
        [MarshalAs(UnmanagedType.U4)]
        public uint MIDIPitchFraction;
        [MarshalAs(UnmanagedType.U4)]
        public uint SMPTEFormat;
        [MarshalAs(UnmanagedType.U4)]
        public uint SMPTEOffset;
        [MarshalAs(UnmanagedType.U4)]
        public uint NumSmplLoops;
        [MarshalAs(UnmanagedType.U4)]
        public uint SamplerData;
    }


    /// <summary>
    /// SMPLLOOP binary structure
    /// Describes one SMPL loop in loop list
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct SMPLLOOP
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public uint ID;
        public uint Type;
        public uint ChunkStart;
        public uint ChunkEnd;
        public uint Fraction;
        public uint PlayCount;
    }

    public enum WAVEFORMATTYPE
    {
        UNKNOWN,
        PCM,
        EXT,
    }

    /// <summary>
    /// Helper class to load wave files
    /// </summary>
    public class WaveFileData
    {
#pragma warning disable CA1823 // Avoid unused private fields
#pragma warning disable IDE0051 // Remove unused private members
        // Constants from C header files
        private const ushort WAVE_FORMAT_PCM = 1;
        private const ushort WAVE_FORMAT_EXTENSIBLE = 0xFFFE;

        private const ushort SPEAKER_FRONT_LEFT = 0x1;
        private const ushort SPEAKER_FRONT_RIGHT = 0x2;
        private const ushort SPEAKER_FRONT_CENTER = 0x4;
        private const ushort SPEAKER_LOW_FREQUENCY = 0x8;
        private const ushort SPEAKER_BACK_LEFT = 0x10;
        private const ushort SPEAKER_BACK_RIGHT = 0x20;
        private const ushort SPEAKER_FRONT_LEFT_OF_CENTER = 0x40;
        private const ushort SPEAKER_FRONT_RIGHT_OF_CENTER = 0x80;
        private const ushort SPEAKER_BACK_CENTER = 0x100;
        private const ushort SPEAKER_SIDE_LEFT = 0x200;
        private const ushort SPEAKER_SIDE_RIGHT = 0x400;
        private const ushort SPEAKER_TOP_CENTER = 0x800;
        private const ushort SPEAKER_TOP_FRONT_LEFT = 0x1000;
        private const ushort SPEAKER_TOP_FRONT_CENTER = 0x2000;
        private const ushort SPEAKER_TOP_FRONT_RIGHT = 0x4000;
        private const ushort SPEAKER_TOP_BACK_LEFT = 0x8000;
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore CA1823 // Avoid unused private fields

        // General info about current wave file
        private bool isKnownType;
        private WAVEFORMATEXTENSIBLE wfEXT;
        private WAVEFORMATTYPE wtType;

        private uint ulDataSize;
        private uint ulDataOffset;

        private ushort nChannels;
        private uint nSamplesPerSec;
        private ushort nBitsPerSample;

        private FileStream fileStream;
        private uint[] cuePoints;

        public WaveFileData()
        {
            fileStream = null;
            isKnownType = false;
            wtType = WAVEFORMATTYPE.UNKNOWN;

            ulDataSize = 0;
            ulDataOffset = 0;

            nChannels = 0;
            nSamplesPerSec = 0;
            nBitsPerSample = 0;
        }

        public void Dispose()
        {
            if (fileStream != null)
                fileStream.Close();

            fileStream = null;
        }

        /// <summary>
        /// Tries to read and parse a binary wave file
        /// </summary>
        /// <param name="n">Name of the file</param>
        /// <returns>True if success</returns>
        private bool ParseWAV(string n)
        {
            fileStream = File.Open(n, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fileStream == null)
                return false;

            // Read Wave file header
            GetNextStructureValue(fileStream, out WAVEFILEHEADER waveFileHeader, -1);
            // Check if wave file
            string hdr = new string(waveFileHeader.szRIFF);
            if (hdr != "RIFF" && hdr != "WAVE")
                return false;

            // Read chunks : fmt, data, cue
            while (GetNextStructureValue(fileStream, out RIFFCHUNK riffChunk, -1))
            {
                // Format chunk
                hdr = new string(riffChunk.szChunkName);
                if (hdr == "fmt ")
                {
                    WAVEFORMATEXTENSIBLE waveFmt = new WAVEFORMATEXTENSIBLE();
                    if (riffChunk.ulChunkSize <= Marshal.SizeOf(waveFmt))
                    {
                        GetNextStructureValue(fileStream, out waveFmt, (int)riffChunk.ulChunkSize);

                        // Determine if this is a WAVEFORMATEX or WAVEFORMATEXTENSIBLE wave file
                        if (waveFmt.Format.wFormatTag == WAVE_FORMAT_PCM)
                        {
                            isKnownType = true;
                            wtType = WAVEFORMATTYPE.PCM;
                            waveFmt.wValidBitsPerSample = waveFmt.Format.wBitsPerSample;
                        }
                        else if (waveFmt.Format.wFormatTag == WAVE_FORMAT_EXTENSIBLE)
                        {
                            isKnownType = true;
                            wtType = WAVEFORMATTYPE.EXT;
                        }

                        wfEXT = waveFmt;
                        nBitsPerSample = waveFmt.Format.wBitsPerSample;
                        nChannels = waveFmt.Format.nChannels;
                        nSamplesPerSec = waveFmt.Format.nSamplesPerSec;
                    }
                    // Unexpected length
                    else
                    {
                        fileStream.Seek(riffChunk.ulChunkSize, SeekOrigin.Current);
                    }
                }
                // Data chunk
                else if (hdr == "data")
                {
                    ulDataSize = riffChunk.ulChunkSize;
                    ulDataOffset = (uint)fileStream.Position;
                    fileStream.Seek(riffChunk.ulChunkSize, SeekOrigin.Current);
                }
                // CUE points
                else if (hdr == "cue ")
                {
                    // Seek back and read CUE header
                    fileStream.Seek(Marshal.SizeOf(riffChunk) * -1, SeekOrigin.Current);
                    GetNextStructureValue(fileStream, out CUECHUNK cueChunk, -1);
                    cuePoints = new uint[cueChunk.ulNumCuePts];
                    {
                        uint pos;
                        // Read all CUE points
                        for (uint i = 0; i < cueChunk.ulNumCuePts; i++)
                        {
                            if (GetNextStructureValue(fileStream, out CUEPT cuePt, -1))
                            {
                                pos = 0;
                                pos += cuePt.ulChunkStart;
                                pos += cuePt.ulBlockStart;
                                pos += cuePt.ulByteStart;

                                cuePoints[i] = pos;
                            }
                        }
                    }
                }
                else if (hdr == "smpl")
                {
                    // Seek back and read SMPL header
                    fileStream.Seek(Marshal.SizeOf(riffChunk) * -1, SeekOrigin.Current);
                    GetNextStructureValue(fileStream, out SMPLCHUNK smplChunk, -1);
                    if (smplChunk.NumSmplLoops > 0)
                    {
                        cuePoints = new uint[smplChunk.NumSmplLoops * 2];
                        {
                            for (uint i = 0; i < smplChunk.NumSmplLoops; i++)
                            {
                                if (GetNextStructureValue(fileStream, out SMPLLOOP smplLoop, -1))
                                {
                                    cuePoints[i * 2] = smplLoop.ChunkStart;
                                    cuePoints[i * 2 + 1] = smplLoop.ChunkEnd;
                                }
                            }
                        }
                    }
                }
                else // skip the unknown chunks
                {
                    fileStream.Seek(riffChunk.ulChunkSize, SeekOrigin.Current);
                }

                // Ensure that we are correctly aligned for next chunk
                if ((riffChunk.ulChunkSize & 1) == 1)
                    fileStream.Seek(1, SeekOrigin.Current);
            } //get next chunk

            // If no data found
            if (ulDataSize == 0 || ulDataOffset == 0)
                return false;

            if (cuePoints != null)
                Array.Sort(cuePoints);

            return isKnownType;
        }

        /// <summary>
        /// Gets the wave file's correspondig AL format number
        /// </summary>
        /// <param name="format">Place to put the format number</param>
        /// <returns>True if success</returns>
        private bool GetALFormat(out int format, ref bool mstsMonoTreatment, ushort origNChannels)
        {
            format = 0;

            if (wtType == WAVEFORMATTYPE.PCM)
            {
                if (wfEXT.Format.nChannels == 1)
                {
                    switch (wfEXT.Format.wBitsPerSample)
                    {
                        case 4:
                            format = OpenAL.GetEnumValue("AL_FORMAT_MONO_IMA4");
                            break;
                        case 8:
                            format = OpenAL.AL_FORMAT_MONO8;
                            break;
                        case 16:
                            format = OpenAL.AL_FORMAT_MONO16;
                            if (origNChannels == 1) 
                                mstsMonoTreatment = true;
                            break;
                    }
                }
                else if (wfEXT.Format.nChannels == 2)
                {
                    switch (wfEXT.Format.wBitsPerSample)
                    {
                        case 4:
                            format = OpenAL.GetEnumValue("AL_FORMAT_STEREO_IMA4");
                            break;
                        case 8:
                            format = OpenAL.AL_FORMAT_STEREO8;
                            break;
                        case 16:
                            format = OpenAL.AL_FORMAT_STEREO16;
                            break;
                    }
                }
                else if ((wfEXT.Format.nChannels == 4) && (wfEXT.Format.wBitsPerSample == 16))
                    format = OpenAL.GetEnumValue("AL_FORMAT_QUAD16");
            }
            else if (wtType == WAVEFORMATTYPE.EXT)
            {
                if ((wfEXT.Format.nChannels == 1) && ((wfEXT.dwChannelMask == SPEAKER_FRONT_CENTER) || (wfEXT.dwChannelMask == (SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT)) || (wfEXT.dwChannelMask == 0)))
                {
                    switch (wfEXT.Format.wBitsPerSample)
                    {
                        case 4:
                            format = OpenAL.GetEnumValue("AL_FORMAT_MONO_IMA4");
                            break;
                        case 8:
                            format = OpenAL.AL_FORMAT_MONO8;
                            break;
                        case 16:
                            format = OpenAL.AL_FORMAT_MONO16;
                            if (origNChannels == 1) 
                                mstsMonoTreatment = true;
                            break;
                    }
                }
                else if ((wfEXT.Format.nChannels == 2) && (wfEXT.dwChannelMask == (SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT)))
                {
                    switch (wfEXT.Format.wBitsPerSample)
                    {
                        case 4:
                            format = OpenAL.GetEnumValue("AL_FORMAT_STEREO_IMA4");
                            break;
                        case 8:
                            format = OpenAL.AL_FORMAT_STEREO8;
                            break;
                        case 16:
                            format = OpenAL.AL_FORMAT_STEREO16;
                            break;
                    }
                }
                else if ((wfEXT.Format.nChannels == 2) && (wfEXT.Format.wBitsPerSample == 16) && (wfEXT.dwChannelMask == (SPEAKER_BACK_LEFT | SPEAKER_BACK_RIGHT)))
                    format = OpenAL.GetEnumValue("AL_FORMAT_REAR16");
                else if ((wfEXT.Format.nChannels == 4) && (wfEXT.Format.wBitsPerSample == 16) && (wfEXT.dwChannelMask == (SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT | SPEAKER_BACK_LEFT | SPEAKER_BACK_RIGHT)))
                    format = OpenAL.GetEnumValue("AL_FORMAT_QUAD16");
                else if ((wfEXT.Format.nChannels == 6) && (wfEXT.Format.wBitsPerSample == 16) && (wfEXT.dwChannelMask == (SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT | SPEAKER_FRONT_CENTER | SPEAKER_LOW_FREQUENCY | SPEAKER_BACK_LEFT | SPEAKER_BACK_RIGHT)))
                    format = OpenAL.GetEnumValue("AL_FORMAT_51CHN16");
                else if ((wfEXT.Format.nChannels == 7) && (wfEXT.Format.wBitsPerSample == 16) && (wfEXT.dwChannelMask == (SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT | SPEAKER_FRONT_CENTER | SPEAKER_LOW_FREQUENCY | SPEAKER_BACK_LEFT | SPEAKER_BACK_RIGHT | SPEAKER_BACK_CENTER)))
                    format = OpenAL.GetEnumValue("AL_FORMAT_61CHN16");
                else if ((wfEXT.Format.nChannels == 8) && (wfEXT.Format.wBitsPerSample == 16) && (wfEXT.dwChannelMask == (SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT | SPEAKER_FRONT_CENTER | SPEAKER_LOW_FREQUENCY | SPEAKER_BACK_LEFT | SPEAKER_BACK_RIGHT | SPEAKER_SIDE_LEFT | SPEAKER_SIDE_RIGHT)))
                    format = OpenAL.GetEnumValue("AL_FORMAT_71CHN16");
            }

            return format != 0;
        }

        /// <summary>
        /// Reads the wave contents of a wave file
        /// </summary>
        /// <param name="toMono">True if must convert to mono before return</param>
        /// <returns>Read wave data</returns>
        private byte[] ReadData(bool toMono)
        {
            byte[] buffer = null;
            if (fileStream == null || ulDataOffset == 0 || ulDataSize == 0)
            {
                return buffer;
            }

            buffer = new byte[ulDataSize];

            fileStream.Seek(ulDataOffset, SeekOrigin.Begin);
            int size = (int)ulDataSize;
            if (fileStream.Read(buffer, 0, size) != size)
            {
                buffer = null;
            }

            if (toMono)
            {
                byte[] newbuffer = ConvertToMono(buffer);
                buffer = newbuffer;
            }

            return buffer;
        }

        /// <summary>
        /// Converts the read wave buffer to mono
        /// </summary>
        /// <param name="buffer">Buffer to convert</param>
        /// <returns>The converted buffer</returns>
        private byte[] ConvertToMono(byte[] buffer)
        {
            if (wfEXT.Format.nChannels == 1)
                return buffer;

            int pos = 0;
            int len = (int)ulDataSize / 2;

            byte[] retval = new byte[len];

            if (wfEXT.Format.wBitsPerSample == 8)
            {
                byte newval;

                while (pos < len)
                {
                    newval = buffer[pos * 2];
                    retval[pos] = newval;
                    pos++;
                }
            }
            else
            {
                using (MemoryStream sms = new MemoryStream(buffer))
                {
                    using (BinaryReader srd = new BinaryReader(sms))
                    {
                        using (MemoryStream dms = new MemoryStream(retval))
                        {
                            using (BinaryWriter drw = new BinaryWriter(dms))
                            {

                                ushort newval;

                                len /= 2;
                                while (pos < len)
                                {
                                    _ = srd.ReadUInt16();
                                    newval = srd.ReadUInt16();
                                    drw.Write(newval);
                                    pos++;
                                }
                                //drw.Flush();
                                //drw.Close();
                                //dms.Flush();
                                //dms.Close();
                                //srd.Close();
                                //sms.Close();
                            }
                        }
                    }
                }
            }

            wfEXT.Format.nChannels = 1;
            ulDataSize = (uint)retval.Length;
            if (cuePoints != null)
                for (int i = 0; i < cuePoints.Length; i++)
                    if (cuePoints[i] != 0xFFFFFFFF)
                        cuePoints[i] /= 2;

            return retval;
        }

        /// <summary>
        /// Opens, reads the given wave file. 
        /// Also creates the AL buffers and fills them with data
        /// </summary>
        /// <param name="name">Name of the wave file to read</param>
        /// <param name="bufferIDs">Array of the buffer IDs to place</param>
        /// <param name="bufferLens">Array of the length data to place</param>
        /// <param name="toMono">Indicates if the wave must be converted to mono</param>
        /// <param name="releasedWithJump">True if sound possibly be released with jump</param>
        /// <returns>True if success</returns>
        public static bool OpenWavFile(string name, ref int[] bufferIDs, ref int[] bufferLens, bool toMono, bool releasedWithJump, ref int numCuePoints, ref bool mstsMonoTreatment)
        {
            WaveFileData wfi = new WaveFileData();

            if (!wfi.ParseWAV(name))
            {
                return false;
            }

            if (wfi.ulDataSize == 0 || ((int)wfi.ulDataSize) == -1)
            {
                Trace.TraceWarning("Skipped wave file with invalid length {0}", name);
                return false;
            }

            ushort origNChannels = wfi.wfEXT.Format.nChannels;

            byte[] buffer = wfi.ReadData(toMono);
            if (buffer == null)
            {
                return false;
            }

            if (!wfi.GetALFormat(out int fmt, ref mstsMonoTreatment, origNChannels))
            {
                return false;
            }

            if (buffer.Length != wfi.ulDataSize)
            {
                Trace.TraceWarning("Invalid wave file length in header; expected {1}, got {2} in {0}", name, buffer.Length, wfi.ulDataSize);
                wfi.ulDataSize = (uint)buffer.Length;
            }

            int[] samplePos = new int[2];

            bool alLoopPointsSoft;
            if (!releasedWithJump && wfi.cuePoints != null && wfi.cuePoints.Length > 1)
            {
                samplePos[0] = (int)(wfi.cuePoints[0]);
                samplePos[1] = (int)(wfi.cuePoints.Last());
                if (samplePos[0] < samplePos[1] && samplePos[1] <= wfi.ulDataSize / (wfi.nBitsPerSample / 8 * wfi.nChannels))
                    alLoopPointsSoft = OpenAL.IsExtensionPresent("AL_SOFT_LOOP_POINTS") == OpenAL.AL_TRUE;
                numCuePoints = wfi.cuePoints.Length;
            }
            // Disable AL_SOFT_LOOP_POINTS OpenAL extension until a more sofisticated detection
            // is implemented for sounds that never need smoothly transiting into another.
            // For utilizing soft loop points a static buffer has to be used, without the ability of
            // continuously buffering, and it is impossible to use it for smooth transition.
            alLoopPointsSoft = false;

            if (wfi.cuePoints == null || wfi.cuePoints.Length == 1 || alLoopPointsSoft)
            {
                bufferIDs = new int[1];
                bufferLens = new int[1];

                bufferLens[0] = (int)wfi.ulDataSize;

                if (bufferLens[0] > 0)
                {
                    OpenAL.GenBuffers(1, out bufferIDs[0]);
                    OpenAL.BufferData(bufferIDs[0], fmt, buffer, (int)wfi.ulDataSize, (int)wfi.nSamplesPerSec);

                    if (alLoopPointsSoft)
                        OpenAL.Bufferiv(bufferIDs[0], OpenAL.AL_LOOP_POINTS_SOFT, samplePos);
                }
                else
                    bufferIDs[0] = 0;

                return true;
            }
            else
            {
                bufferIDs = new int[wfi.cuePoints.Length + 1];
                bufferLens = new int[wfi.cuePoints.Length + 1];
                numCuePoints = wfi.cuePoints.Length;

                uint prevAdjPos = 0;
                for (int i = 0; i < wfi.cuePoints.Length; i++)
                {
                    uint adjPos = wfi.cuePoints[i] * wfi.nBitsPerSample / 8 * wfi.nChannels;
                    if (adjPos > wfi.ulDataSize)
                    {
                        Trace.TraceWarning("Invalid cue point in wave file; Length {1}, CUE {2}, BitsPerSample {3}, Channels {4} in {0}", name, wfi.ulDataSize, adjPos, wfi.nBitsPerSample, wfi.nChannels);
                        wfi.cuePoints[i] = 0xFFFFFFFF;
                        adjPos = prevAdjPos;
                    }

                    bufferLens[i] = (int)adjPos - (int)prevAdjPos;
                    if (bufferLens[i] > 0)
                    {
                        OpenAL.GenBuffers(1, out bufferIDs[i]);
                        OpenAL.BufferData(bufferIDs[i], fmt, GetFromArray(buffer, (int)prevAdjPos, bufferLens[i]), bufferLens[i], (int)wfi.nSamplesPerSec);
                    }
                    else
                    {
                        bufferIDs[i] = 0;
                    }

                    if (i == wfi.cuePoints.Length - 1)
                    {
                        bufferLens[i + 1] = (int)wfi.ulDataSize - (int)adjPos;
                        if (bufferLens[i + 1] > 0)
                        {
                            OpenAL.GenBuffers(1, out bufferIDs[i + 1]);
                            OpenAL.BufferData(bufferIDs[i + 1], fmt, GetFromArray(buffer, (int)adjPos, bufferLens[i + 1]), bufferLens[i + 1], (int)wfi.nSamplesPerSec);
                        }
                        else
                        {
                            bufferIDs[i + 1] = 0;
                        }
                    }
                    prevAdjPos = adjPos;
                }
            }

            return true;
        }

        /// <summary>
        /// Extracts an array of bytes from an another array of bytes
        /// </summary>
        /// <param name="buffer">Initial buffer</param>
        /// <param name="offset">Offset from copy</param>
        /// <param name="len">Number of bytes to copy</param>
        /// <returns>New buffer with the extracted data</returns>
        private static byte[] GetFromArray(byte[] buffer, int offset, int len)
        {
            byte[] retval = new byte[len];
            Buffer.BlockCopy(buffer, offset, retval, 0, len);
            return retval;
        }

        /// <summary>
        /// Reads a given structure from a FileStream
        /// </summary>
        /// <typeparam name="T">Type to read, must be able to Marshal to native</typeparam>
        /// <param name="stream">FileStream from read</param>
        /// <param name="retval">The filled structure</param>
        /// <param name="len">The bytes to read, -1 if the structure size must be filled</param>
        /// <returns>True if success</returns>
        private static bool GetNextStructureValue<T>(FileStream stream, out T retval, int len)
        {
            byte[] buffer;
            retval = default;
            bool result = false;
            if (len == -1)
            {
                buffer = new byte[Marshal.SizeOf(retval.GetType())];
            }
            else
            {
                buffer = new byte[len];
            }

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            if (stream.Read(buffer, 0, buffer.Length) == buffer.Length)
            {
                try
                {
                    retval = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
                    result = true;
                }
                finally
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
            }
            return result;
        }
    }
}

