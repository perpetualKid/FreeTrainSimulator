using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using FreeTrainSimulator.Common.Info;

namespace FreeTrainSimulator.Common.Logging
{
    public static partial class LoggingUtil
    {
        [GeneratedRegex(@"\{(\w*?)\}")]
        private static partial Regex paramReplacement();

        public static readonly string SeparatorLine = new string('-', 80);

        public const string BugTrackerUrl = "https://github.com/perpetualKid/FreeTrainSimulator/issues";

        static LoggingUtil()
        {
            if (Debugger.IsLogging())
            {
                if (!Trace.Listeners.OfType<ConsoleTraceListener>().Any())
                    Trace.Listeners.Add(new ConsoleTraceListener());
            }
        }

        public static string CustomizeLogFileName(string fileNameTemplate)
        {
            Dictionary<string, string> replacementValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "application", RuntimeInfo.ProductApplication },
                { "product", RuntimeInfo.ProductName },
                { "version", VersionInfo.Version},
                { "date", DateTime.Now.Date.ToString("d", CultureInfo.CurrentCulture) },
                { "time", TimeSpan.FromSeconds((int)DateTime.Now.TimeOfDay.TotalSeconds).ToString("t", CultureInfo.CurrentCulture) },
            };

            string result = paramReplacement().Replace(fileNameTemplate, delegate (Match match)
            {
                string key = match.Groups[1].Value;
                return replacementValues[key];
            });

            Regex invalidCharReplacement = new Regex(string.Format(CultureInfo.CurrentCulture, "[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars()))));
            return invalidCharReplacement.Replace(result, "_");
        }

        public static void InitLogging(string logFileName, TraceEventType eventType, bool systemDetails, bool appendLog)
        {
            if (string.IsNullOrEmpty(logFileName))
                return;

            for (int i = Trace.Listeners.Count -1; i >= 0; i--)
            {
                if (Trace.Listeners[i] is LoggingTraceListener loggingTraceListener)
                {
                    Trace.Listeners.RemoveAt(i);
                    loggingTraceListener.Flush();
                    loggingTraceListener.Close();
                    loggingTraceListener.Dispose();
                }
            }

            try
            {
                if (!appendLog)
                    File.Delete(logFileName);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is ArgumentException || ex is IOException)
            {
            }

            try
            {
                StreamWriter writer = new StreamWriter(logFileName, true, Encoding.Default, 512)
                {
                    AutoFlush = true
                };
                // Captures Trace.Trace* calls and others and formats.
                LoggingTraceListener traceListener = new LoggingTraceListener(writer, TraceEventType.Information);
                Trace.Listeners.Add(traceListener);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is ArgumentException || ex is IOException)
            {
            }

            Trace.WriteLine($"This is a log file for {RuntimeInfo.ProductName} {RuntimeInfo.ProductApplication}. Please include this file in bug reports.");
            Trace.WriteLine(SeparatorLine);
            if (eventType <= TraceEventType.Error)
            {
                Trace.WriteLine("Logging is disabled, only fatal errors will appear here.");
                Trace.WriteLine(SeparatorLine);
            }
            else
            {
                if (systemDetails)
                {
                    SystemInfo.WriteSystemDetails();
                    Trace.WriteLine(SeparatorLine);
                }
                Trace.WriteLine($"{"Date/Time",-12}= {DateTime.Now} ({DateTime.UtcNow:u})");
                Trace.WriteLine($"{"Version",-12}= {VersionInfo.Version}");
                Trace.WriteLine($"{"Code Version",-12}= {VersionInfo.CodeVersion}");
                Trace.WriteLine($"{"OS",-12}= {RuntimeInformation.OSDescription} {RuntimeInformation.RuntimeIdentifier}");
                Trace.WriteLine($"{"Runtime",-12}= {RuntimeInformation.FrameworkDescription} ({(Environment.Is64BitProcess ? "64" : "32")}bit)");
                if (logFileName.Length > 0)
                    Trace.WriteLine($"{"Logfile",-12}= {logFileName.Replace(Environment.UserName, "********", StringComparison.OrdinalIgnoreCase)}");
                Trace.WriteLine($"{"Logging",-12}= {eventType}");
                foreach (string arg in Environment.GetCommandLineArgs())
                    Trace.WriteLine($"{"Argument",-12}= {arg.Replace(Environment.UserName, "********", StringComparison.OrdinalIgnoreCase)}");
                Trace.WriteLine(SeparatorLine);
            }
        }
    }
}
