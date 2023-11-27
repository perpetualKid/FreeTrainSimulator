using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using Orts.Common.Info;

namespace Orts.Common.Logging
{
    public class LoggedBinaryWriter : BinaryWriter
    {
        private readonly StringBuilder builder = new StringBuilder();
        private string outputFile;

        public LoggedBinaryWriter(Stream output) : base(output)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{outputFile = (BaseStream is FileStream fileStream ? fileStream.Name : BaseStream.ToString())}");
        }

        public LoggedBinaryWriter(Stream output, Encoding encoding) : base(output, encoding)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{outputFile = (BaseStream is FileStream fileStream ? fileStream.Name : BaseStream.ToString())}");
        }

        public LoggedBinaryWriter(Stream output, Encoding encoding, bool leaveOpen) : base(output, encoding, leaveOpen)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{outputFile = (BaseStream is FileStream fileStream ? fileStream.Name : BaseStream.ToString())}");
        }

        protected override void Dispose(bool disposing)
        {
            outputFile = Path.ChangeExtension(outputFile, "logged-write.log");
            if (!Path.IsPathFullyQualified(outputFile))
                outputFile = Path.Combine(RuntimeInfo.UserDataFolder, outputFile);
            using (TextWriter writer = new StreamWriter(outputFile, new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.OpenOrCreate }))
                writer.Write(builder.ToString());
            base.Dispose(disposing);
        }

        public override void Write(bool value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(byte value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(byte[] buffer)
        {
            base.Write(buffer);
            Log(buffer);
        }

        public override void Write(byte[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            Log(buffer);
        }

        public override void Write(char ch)
        {
            base.Write(ch);
            Log(ch);
        }

        public override void Write(char[] chars)
        {
            base.Write(chars);
            Log(chars);
        }

        public override void Write(char[] chars, int index, int count)
        {
            base.Write(chars, index, count);
            Log(chars);
        }

        public override void Write(decimal value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(double value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(Half value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(short value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(int value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(long value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            Log(buffer);
        }

        public override void Write(ReadOnlySpan<char> chars)
        {
            base.Write(chars);
            Log(chars);
        }

        public override void Write(sbyte value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(float value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(string value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(ushort value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(uint value)
        {
            base.Write(value);
            Log(value);
        }

        public override void Write(ulong value)
        {
            base.Write(value);
            Log(value);
        }

        private void Log<T>(T value)
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame frame = stackTrace.GetFrames()[2];
            MethodBase method = frame.GetMethod();
            if (method.DeclaringType.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                return;
            //_ = builder.AppendLine($"{value?.GetType().Name}\t{value}\t({method.DeclaringType}\t{method.Name})");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{value?.GetType().Name}\t{value}\t({method.DeclaringType})");
        }

        private void Log<T>(ReadOnlySpan<T> value)
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame frame = stackTrace.GetFrames()[2];
            MethodBase method = frame.GetMethod();
            if (method.DeclaringType.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                return;
            //_ = builder.AppendLine($"{typeof(T).Name}\t{value.ToString()}\t({method.DeclaringType}\t{method.Name}:)");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{typeof(T).Name}\t{value.ToString()}\t({method.DeclaringType}\t{method.Name}:)");
        }

    }
}
