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
        private readonly bool errorsOnly;
        private bool lastWrittenFormatted;
        private readonly Dictionary<TraceEventType, int> eventCounts = new Dictionary<TraceEventType, int>(EnumExtension.GetValues<TraceEventType>().ToDictionary(t => t, t => 0));

        public int EventCount(TraceEventType eventType) => eventCounts[eventType];

        public LoggingTraceListener(TextWriter writer)
            : this(writer, false)
        {
        }

        public LoggingTraceListener(TextWriter writer, bool errorsOnly)
        {
            this.writer = writer;
            this.errorsOnly = errorsOnly;
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            if (Filter == null || Filter.ShouldTrace(eventCache, source, eventType, id, null, null, null, null))
                TraceEventInternal(eventCache, source, eventType, id, "", Array.Empty<object>());
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (Filter == null || Filter.ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
                TraceEventInternal(eventCache, source, eventType, id, message, Array.Empty<object>());
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            if (Filter == null || Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
                TraceEventInternal(eventCache, source, eventType, id, format, args ?? Array.Empty<object>());
        }

        private void TraceEventInternal(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, object[] args)
        {
            if (null == eventCache)
                return;
            _ = source;
            _ = id;
            eventCounts[eventType]++;

            // Event is less important than error (and critical) and we're logging only errors... bail.
            if (eventType > TraceEventType.Error && errorsOnly)
                return;

            StringBuilder output = new StringBuilder();
            if (!lastWrittenFormatted)
            {
                output.AppendLine();
                output.AppendLine();
            }
            output.Append(eventType);
            output.Append(": ");
            if (args.Length == 0)
                output.Append(format);
            else
                output.AppendFormat(CultureInfo.InvariantCulture, format, args);

            // Log exception details if it is an exception.
            if (eventCache.LogicalOperationStack.Contains(LogicalOperationWriteException))
            {
                // Attempt to clean up the stacks; the problem is that the exception stack only goes as far back as the call made inside the try block. We also have access to the
                // full stack to this trace call, which goes via the catch block at the same level as the try block. We'd prefer to have the whole stack, so we need to find the
                // join and stitch the stacks together.
                Exception error = args[0] as Exception;

                string[] errorStack = error.ToString().Split('\n', '\n', StringSplitOptions.RemoveEmptyEntries);
                string[] catchStack = new StackTrace(true).ToString().Split('\n', '\n', StringSplitOptions.RemoveEmptyEntries);
                int catchIndex = Array.IndexOf(catchStack, errorStack[^1]);

                output.AppendLine(error.ToString());
                if (catchIndex >= 0)
                    output.AppendLine(string.Join(Environment.NewLine, catchStack, catchIndex + 1, catchStack.Length - catchIndex - 1));
            }
            else

                // Only log a stack trace for critical and error levels.
                if (eventType < TraceEventType.Warning && (TraceOutputOptions & TraceOptions.Callstack) != 0)
            {
                output.AppendLine();
                output.AppendLine(new StackTrace(true).ToString());
            }

            output.AppendLine();
            writer.Write(output.ToString());
            lastWrittenFormatted = true;
        }

        public override void Write(string message)
        {
            if (!errorsOnly)
            {
                writer.Write(message);
                lastWrittenFormatted = false;
            }
        }

        public override void WriteLine(string message)
        {
            if (!errorsOnly)
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
            else if (!errorsOnly)
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
