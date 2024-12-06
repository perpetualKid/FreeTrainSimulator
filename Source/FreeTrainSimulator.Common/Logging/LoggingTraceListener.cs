using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FreeTrainSimulator.Common.Logging
{
    public sealed class LoggingTraceListener : TraceListener
    {
        private class LoggingTraceFilter : TraceFilter
        {
            internal readonly TraceSettings traceLevel;

            internal LoggingTraceFilter(TraceSettings traceLevel)
            {
                this.traceLevel = traceLevel;
            }

            public override bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id, [StringSyntax("CompositeFormat")] string formatOrMessage, object[] args, object data1, object[] data)
            {
                return traceLevel.HasFlag(TraceSettings.Trace) && eventType > TraceEventType.Error
                    || traceLevel.HasFlag(TraceSettings.Errors) && eventType <= TraceEventType.Error;
            }
        }

        private readonly TextWriter writer;
        private bool lastWrittenFormatted;
        private readonly Dictionary<TraceEventType, int> eventCounts = new Dictionary<TraceEventType, int>(EnumExtension.GetValues<TraceEventType>().ToDictionary(t => t, t => 0));
        private TraceSettings traceLevel;

        public int EventCount(TraceEventType eventType) => eventCounts[eventType];

        public TraceSettings LogLevel
        {
            get => traceLevel; 
            set
            {
                traceLevel = value;
                Filter = new LoggingTraceFilter(value);
            }
        }

        public LoggingTraceListener(TextWriter writer)
            : this(writer, TraceSettings.Errors | TraceSettings.Trace)
        {
        }

        public LoggingTraceListener(TextWriter writer, TraceSettings traceLevel)
        {
            this.writer = writer;
            Filter = new LoggingTraceFilter(this.traceLevel = traceLevel);
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
            if (null == eventCache || !Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
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
                output.Append(format);
            else
                output.AppendFormat(CultureInfo.InvariantCulture, format, args);

            // Log exception details if it is an exception.
            if (eventCache.LogicalOperationStack.Contains(LogicalOperationWriteException) && args?[0] is Exception error)
            {
                // Attempt to clean up the stacks; the problem is that the exception stack only goes as far back as the call made inside the try block. We also have access to the
                // full stack to this trace call, which goes via the catch block at the same level as the try block. We'd prefer to have the whole stack, so we need to find the
                // join and stitch the stacks together.

                string[] errorStack = error.ToString().Split('\n', '\n', StringSplitOptions.RemoveEmptyEntries);
                string[] catchStack = new StackTrace(true).ToString().Split('\n', '\n', StringSplitOptions.RemoveEmptyEntries);
                int catchIndex = Array.IndexOf(catchStack, errorStack[^1]);

                output.AppendLine(error.ToString());
                if (catchIndex >= 0)
                    output.AppendLine(string.Join(Environment.NewLine, catchStack, catchIndex + 1, catchStack.Length - catchIndex - 1));
            }
            else
            {
                // Only log a stack trace for critical and error levels.
                if (eventType < TraceEventType.Warning && (TraceOutputOptions & TraceOptions.Callstack) != 0)
                {
                    output.AppendLine();
                    output.AppendLine(new StackTrace(true).ToString());
                }
            }

            output.AppendLine();
            writer.Write(output.ToString());
            lastWrittenFormatted = true;
        }

        public override void Write(string message)
        {
            if (traceLevel > TraceSettings.Errors)
            {
                writer.Write(message);
                lastWrittenFormatted = false;
            }
        }

        public override void WriteLine(string message)
        {
            if (traceLevel > TraceSettings.Errors)
            {
                writer.WriteLine(message);
                lastWrittenFormatted = false;
            }
        }

        public override void WriteLine(object o)
        {
            if (o is Exception)
            {
                Trace.CorrelationManager.StartLogicalOperation(LogicalOperationWriteException);
                if (o is FatalException)
                    Trace.TraceError("{0}", (o as FatalException).InnerException);
                else
                    Trace.TraceWarning("{0}", o);
                Trace.CorrelationManager.StopLogicalOperation();
            }
            else if (traceLevel > TraceSettings.Errors)
            {
                base.WriteLine(o);
                lastWrittenFormatted = false;
            }
        }

        private static readonly LogicalOperation LogicalOperationWriteException = new LogicalOperation();

        private class LogicalOperation
        {
        }

        protected override void Dispose(bool disposing)
        {
            writer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
