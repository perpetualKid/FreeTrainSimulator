// COPYRIGHT 2017 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Orts.Common.Logging;

[assembly: CLSCompliant(false)]

namespace Orts.DataValidator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            bool verbose = args.Contains("/verbose", StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> files = args.Where(arg => !arg.StartsWith('/'));
            if (files.Any())
                Validate(verbose, files);
            else
                ShowHelp();
        }

        private static void ShowHelp()
        {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            Console.WriteLine("Open Rails Data Validator utility");
            Console.WriteLine();
            Console.WriteLine("{0} [/verbose] PATH [...]", Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName));
            Console.WriteLine();
            //                "1234567890123456789012345678901234567890123456789012345678901234567890123456789"
            Console.WriteLine("    /verbose  Displays all expected/valid values in addition to any errors.");
        }

        private static void Validate(bool verbose, IEnumerable<string> files)
        {
            ORTraceListener traceListener = SetUpTracing(verbose);

            foreach (string file in files)
                Validate(file);

            ShowTracingReport(traceListener);
        }

        private static ORTraceListener SetUpTracing(bool verbose)
        {
            // Captures Trace.Trace* calls and others and formats.
            ORTraceListener traceListener = new ORTraceListener(Console.Out)
            {
                TraceOutputOptions = TraceOptions.Callstack
            };
            if (verbose)
                traceListener.Filter = new EventTypeFilter(SourceLevels.Critical | SourceLevels.Error | SourceLevels.Warning | SourceLevels.Information);
            else
                traceListener.Filter = new EventTypeFilter(SourceLevels.Critical | SourceLevels.Error | SourceLevels.Warning);

            // Trace.Listeners and Debug.Listeners are the same list.
            Trace.Listeners.Add(traceListener);

            return traceListener;
        }

        private static void ShowTracingReport(ORTraceListener traceListener)
        {
            Console.WriteLine();
            Console.WriteLine("Validator summary");
            Console.WriteLine("  Errors:        {0}", traceListener.EventCount(TraceEventType.Critical) + traceListener.EventCount(TraceEventType.Error));
            Console.WriteLine("  Warnings:      {0}", traceListener.EventCount(TraceEventType.Warning));
            Console.WriteLine("  Informations:  {0}", traceListener.EventCount(TraceEventType.Information));
            Console.WriteLine();
        }

        private static void Validate(string file)
        {
            Console.WriteLine("{0}: Begin", file);

            if (file.Contains('*', StringComparison.Ordinal))
            {
                string path = Path.GetDirectoryName(file);
                string searchPattern = Path.GetFileName(file);
                foreach (string foundFile in Directory.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories))
                    Validate(foundFile);
            }
            else
            {

                if (!File.Exists(file))
                {
                    Trace.TraceError("Error: File does not exist");
                    return;
                }

                if (Path.GetExtension(file).Equals(".t", StringComparison.OrdinalIgnoreCase))
                        _ = new TerrainValidator(file);
            }

            Console.WriteLine("{0}: End", file);
        }
#pragma warning restore CA1303 // Do not pass literals as localized parameters
    }
}
