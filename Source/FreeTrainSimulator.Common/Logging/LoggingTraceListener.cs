using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeTrainSimulator.Common.Logging
{
    public sealed class LoggingTraceListener : TraceListener
    {
        private readonly TextWriter writer;
        private readonly Dictionary<TraceEventType, int> eventCounts = new Dictionary<TraceEventType, int>(EnumExtension.GetValues<TraceEventType>().ToDictionary(t => t, t => 0));
        private readonly TraceEventType eventType;
        private bool lastWrittenFormatted;

        public int EventCount(TraceEventType eventType) => eventCounts[eventType];

        public LoggingTraceListener(TextWriter writer)
            : this(writer, TraceEventType.Verbose)
        {
        }

        public LoggingTraceListener(TextWriter writer, TraceEventType eventType)
        {
            this.writer = writer;
            this.eventType = eventType;
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            TraceEventInternal(eventCache, source, eventType, id, string.Empty, null);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            TraceEventInternal(eventCache, source, eventType, id, message, null);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            TraceEventInternal(eventCache, source, eventType, id, format, args);
        }

        private void TraceEventInternal(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, object[] args)
        {
            if (null == eventCache || eventType > this.eventType)
                return;

            _ = source;
            _ = id;
            eventCounts[eventType]++;

            StringBuilder output = new StringBuilder();
            if (!lastWrittenFormatted)
            {
                output.AppendLine();
            }
            output.Append(eventType);
            output.Append(": ");

            if (args == null || args.Length == 0)
            {
                output.Append(format);
            }
            else
            {
                output.AppendFormat(CultureInfo.InvariantCulture, format, args);
            }

            // Log exception details if it is an exception.
            if (args?[0] is Exception error)
            {
                output.AppendLine();
                output.Append(new StackTrace(6, true).ToString());
            }
            else
            {
                if (eventType < TraceEventType.Warning)
                {
                    output.AppendLine();
                    output.Append(new StackTrace(3, true).ToString());
                }
            }

            output.AppendLine();
            writer.Write(output.ToString());
            lastWrittenFormatted = true;
        }

        public override void Write(string message)
        {
            if (eventType > TraceEventType.Warning)
            {
                writer.Write(message);
                lastWrittenFormatted = false;
            }
        }

        public override void WriteLine(string message)
        {
            if (eventType > TraceEventType.Warning)
            {
                writer.WriteLine(message);
                lastWrittenFormatted = true;
            }
        }

        public override void WriteLine(object o)
        {
            if (o is Exception)
            {
                Trace.TraceError("{0}", o);
            }
            else if (eventType > TraceEventType.Warning)
            {
                base.WriteLine(o);
                lastWrittenFormatted = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            writer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
