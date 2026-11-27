// COPYRIGHT 2018 by the Open Rails project.
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
using System.Text;
using System.Threading;

using FreeTrainSimulator.Common.Logging;

using Microsoft.Xna.Framework.Graphics;

namespace Orts.Formats.ContentChecking
{
    /// <summary>
    /// Preserves the legacy content checker's parser checks and partial relationship traversal.
    /// This is not a native-model validator or a complete content dependency validator.
    /// </summary>
    public sealed class ContentChecker
    {
        private static readonly Lock checkLock = new Lock();
        private readonly List<string> requestedFiles = new List<string>();
        private readonly Dictionary<string, bool> loaded = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> activeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly TextWriter output;
        private readonly GraphicsDevice graphicsDevice;
        private readonly Action<string> waveFileChecker;
        private int filesLoaded;
        private int filesSkipped;
        private int filesFailed;

        private ContentChecker(TextWriter output, GraphicsDevice graphicsDevice, Action<string> waveFileChecker)
        {
            this.output = output;
            this.graphicsDevice = graphicsDevice;
            this.waveFileChecker = waveFileChecker;
        }

        /// <summary>
        /// Checks files or recursive filename wildcard patterns and writes parser diagnostics and a summary.
        /// Scans are serialized because the legacy parsers use process-wide trace listeners; unrelated
        /// trace events emitted during a scan can also appear in its report. Existing listeners are retained.
        /// </summary>
        /// <param name="files">Files or filename wildcard patterns; directory wildcards are not supported.</param>
        /// <param name="verbose">Whether to include informational trace messages.</param>
        /// <param name="additionType">The existing relationship traversal to perform.</param>
        /// <param name="output">The caller-owned diagnostic writer, which is not disposed.</param>
        /// <param name="graphicsDevice">A caller-owned device for ACE checks, or null to report them as skipped.</param>
        /// <param name="waveFileChecker">A runtime WAV checker, or null to report WAV files as skipped.
        /// It runs synchronously on the calling thread and must have its audio prerequisites initialized.</param>
        public static void CheckFiles(IEnumerable<string> files, bool verbose, AdditionType additionType,
            TextWriter output, GraphicsDevice graphicsDevice, Action<string> waveFileChecker)
        {
            ArgumentNullException.ThrowIfNull(files);
            ArgumentNullException.ThrowIfNull(output);
            if (!Enum.IsDefined(additionType))
                throw new ArgumentOutOfRangeException(nameof(additionType));

            lock (checkLock)
            {
                using (LoggingTraceListener traceListener = new LoggingTraceListener(new BorrowedTextWriter(output),
                    verbose ? TraceEventType.Information : TraceEventType.Warning))
                {
                    Trace.Listeners.Add(traceListener);
                    try
                    {
                        ContentChecker checker = new ContentChecker(output, graphicsDevice, waveFileChecker);
                        checker.SetRequestedFiles(files);
                        checker.LoadRequestedFiles(additionType);
                        checker.ShowTracingReport(traceListener);
                    }
                    finally
                    {
                        Trace.Listeners.Remove(traceListener);
                        output.Flush();
                    }
                }
            }
        }

        private void SetRequestedFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                try
                {
                    ArgumentException.ThrowIfNullOrEmpty(file);
                    if (file.Contains('*', StringComparison.Ordinal))
                    {
                        string path = Path.GetDirectoryName(file);
                        if (string.IsNullOrEmpty(path))
                            path = Directory.GetCurrentDirectory();

                        SetRequestedFiles(Directory.GetFiles(path, Path.GetFileName(file), SearchOption.AllDirectories));
                    }
                    else
                    {
                        string fullPath = Path.GetFullPath(file);
                        if (!File.Exists(fullPath))
                            throw new FileNotFoundException("File does not exist.", fullPath);

                        if (loaded.TryAdd(fullPath, false))
                            requestedFiles.Add(fullPath);
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    filesFailed++;
                    Trace.TraceError("Could not select content file '{0}': {1}", file, error);
                }
            }
        }

        private void LoadRequestedFiles(AdditionType additionType)
        {
            int passes = additionType == AdditionType.None ? 1 : 2;
            for (int pass = 0; pass < passes; pass++)
            {
                foreach (string file in requestedFiles)
                {
                    if (!loaded[file])
                        LoadFileAndAdditions(file, null, additionType, 0, passes > 1 && pass == 0);
                }
            }
        }

        private void LoadFileAndAdditions(string file, Loader loader, AdditionType additionType,
            int indentLevel, bool postponeDependent)
        {
            List<FileAndLoader> additionalFiles = new List<FileAndLoader>();
            bool active = false;
            try
            {
                file = Path.GetFullPath(file);
                if (!File.Exists(file))
                    throw new FileNotFoundException("File does not exist.", file);

                loader ??= LoaderFactory.GetLoader(file);
                if (postponeDependent && loader.IsDependent)
                    return;

                if (!activeFiles.Add(file))
                {
                    Trace.TraceWarning("Skipped cyclic content reference: {0}", file);
                    return;
                }
                active = true;
                loaded[file] = true;

                if (indentLevel > 0)
                    output.Write(".{0}", new string(' ', indentLevel));
                output.Write("{0}: ", file);

                loader.Configure(output, graphicsDevice, waveFileChecker);
                loader.TryLoading(file);
                if (loader.FilesLoaded > 0)
                {
                    loader.AddAdditionalFiles(additionType,
                        (additionalFile, additionalLoader) => additionalFiles.Add(new FileAndLoader(additionalFile, additionalLoader)));
                    filesLoaded += loader.FilesLoaded;
                    output.WriteLine("OK");
                    foreach (FileAndLoader additionalFile in additionalFiles)
                        LoadFileAndAdditions(additionalFile.File, additionalFile.Loader, additionType, indentLevel + 1, false);
                }
                filesSkipped += loader.FilesSkipped;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // A batch check reports a failed file and continues with the remaining files.
            catch (Exception error)
#pragma warning restore CA1031
            {
                filesFailed++;
                if (file != null)
                    loaded[file] = true;
                Trace.TraceError("Could not check content file '{0}': {1}", file, error);
            }
            finally
            {
                if (active)
                    activeFiles.Remove(file);
            }

        }

        private void ShowTracingReport(LoggingTraceListener traceListener)
        {
            output.WriteLine();
            output.WriteLine("Loader summary");
            output.WriteLine("  Files loaded:  {0}", filesLoaded);
            output.WriteLine("  Files skipped: {0}", filesSkipped);
            output.WriteLine("  Files failed:  {0}", filesFailed);
            output.WriteLine();
            output.WriteLine("  Errors:        {0}", traceListener.EventCount(TraceEventType.Critical) + traceListener.EventCount(TraceEventType.Error));
            output.WriteLine("  Warnings:      {0}", traceListener.EventCount(TraceEventType.Warning));
            output.WriteLine("  Informations:  {0}", traceListener.EventCount(TraceEventType.Information));
            output.WriteLine();
        }

        private readonly struct FileAndLoader
        {
            /// <summary>The related file's serialized or resolved path.</summary>
            public string File { get; }
            /// <summary>The loader retaining the owning file's context.</summary>
            public Loader Loader { get; }

            public FileAndLoader(string file, Loader loader)
            {
                File = file;
                Loader = loader;
            }
        }

        private sealed class BorrowedTextWriter : TextWriter
        {
            private readonly TextWriter writer;

            public override Encoding Encoding => writer.Encoding;

            public BorrowedTextWriter(TextWriter writer)
            {
                this.writer = writer;
            }

            public override void Write(string value)
            {
                writer.Write(value);
            }

            public override void WriteLine(string value)
            {
                writer.WriteLine(value);
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    writer?.Flush();
                    writer?.Close();
                    writer?.Dispose();
                }
            }
        }
    }
}