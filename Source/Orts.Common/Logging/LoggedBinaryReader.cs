using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using Orts.Common.Info;

namespace Orts.Common.Logging
{
    public class LoggedBinaryReader : BinaryReader
    {
        private readonly StringBuilder builder = new StringBuilder();
        private string outputFile;

        public LoggedBinaryReader(Stream input) : base(input)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{outputFile = (BaseStream is FileStream fileStream ? fileStream.Name : BaseStream.ToString())}");
        }

        public LoggedBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{outputFile = (BaseStream is FileStream fileStream ? fileStream.Name : BaseStream.ToString())}");
        }

        public LoggedBinaryReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{outputFile = (BaseStream is FileStream fileStream ? fileStream.Name : BaseStream.ToString())}");
        }

        protected override void Dispose(bool disposing)
        {
            outputFile = Path.ChangeExtension(outputFile, "logged-read.log");
            if (!Path.IsPathFullyQualified(outputFile))
                outputFile = Path.Combine(RuntimeInfo.UserDataFolder, outputFile);
            using (TextWriter writer = new StreamWriter(outputFile, new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.OpenOrCreate }))
                writer.Write(builder.ToString());
            base.Dispose(disposing);
        }

        public override int Read()
        {
            int value = base.Read();
            Log(value);
            return value;
        }

        public override int Read(byte[] buffer, int index, int count)
        {
            int value = base.Read(buffer, index, count);
            Log(buffer);
            return value;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            int value = base.Read(buffer, index, count);
            Log(buffer);
            return value;
        }

        public override int Read(Span<byte> buffer)
        {
            int value = base.Read(buffer);
            Log(buffer);
            return value;
        }

        public override int Read(Span<char> buffer)
        {
            int value = base.Read(buffer);
            Log(buffer);
            return value;
        }

        public override bool ReadBoolean()
        {
            bool value = base.ReadBoolean();
            Log(value);
            return value;
        }

        public override byte ReadByte()
        {
            byte value = base.ReadByte();
            Log(value);
            return value;
        }

        public override byte[] ReadBytes(int count)
        {
            byte[] value = base.ReadBytes(count);
            Log(value);
            return value;
        }

        public override char ReadChar()
        {
            char value = base.ReadChar();
            Log(value);
            return value;
        }

        public override char[] ReadChars(int count)
        {
            char[] value = base.ReadChars(count);
            Log(value);
            return value;
        }

        public override decimal ReadDecimal()
        {
            decimal value = base.ReadDecimal();
            Log(value);
            return value;
        }

        public override double ReadDouble()
        {
            double value = base.ReadDouble();
            Log(value);
            return value;
        }

        public override Half ReadHalf()
        {
            Half value = base.ReadHalf();
            Log(value);
            return value;
        }

        public override short ReadInt16()
        {
            short value = base.ReadInt16();
            Log(value);
            return value;
        }

        public override int ReadInt32()
        {
            int value = base.ReadInt32();
            Log(value);
            return value;
        }

        public override long ReadInt64()
        {
            long value = base.ReadInt64();
            Log(value);
            return value;
        }

        public override sbyte ReadSByte()
        {
            sbyte value = base.ReadSByte();
            Log(value);
            return value;
        }

        public override float ReadSingle()
        {
            float value = base.ReadSingle();
            Log(value);
            return value;
        }

        public override string ReadString()
        {
            string value = base.ReadString();
            Log(value);
            return value;
        }

        public override ushort ReadUInt16()
        {
            ushort value = base.ReadUInt16();
            Log(value);
            return value;
        }

        public override uint ReadUInt32()
        {
            uint value = base.ReadUInt32();
            Log(value);
            return value;
        }

        public override ulong ReadUInt64()
        {
            ulong value = base.ReadUInt64();
            Log(value);
            return value;
        }

        private void Log<T>(T value)
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame frame = stackTrace.GetFrames()[2];
            MethodBase method = frame.GetMethod();
            if (method.DeclaringType.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                return;
//            _ = builder.AppendLine($"{value?.GetType().Name}\t{value}\t({method.DeclaringType}\t{method.Name})");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{value?.GetType().Name}\t{value}\t({method.DeclaringType})");
        }

        private void Log<T>(Span<T> value)
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame frame = stackTrace.GetFrames()[2];
            MethodBase method = frame.GetMethod();
            if (method.DeclaringType.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                return;
            //_ = builder.AppendLine($"{typeof(T).Name}\t{value.ToString()}\t({method.DeclaringType}\t{method.Name})");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{typeof(T).Name}\t{value.ToString()}\t({method.DeclaringType})");
        }



    }
}
