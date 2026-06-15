using System;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;

using FreeTrainSimulator.Toolbox.PopupWindows;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Hosted-mode bridge exposing read-only log file content for a dockable WPF log window.
    /// </summary>
    internal sealed class LogToolWindow : IToolboxToolWindow
    {
        private readonly Func<string> logFilePathAccessor;
        private volatile ToolWindowSnapshot snapshot = ToolWindowSnapshot.Empty;
        private volatile bool active;
        private string previousContent = string.Empty;

        internal LogToolWindow(string logFilePath)
            : this(() => logFilePath)
        {
        }

        internal LogToolWindow(Func<string> logFilePathAccessor)
        {
            this.logFilePathAccessor = logFilePathAccessor ?? throw new ArgumentNullException(nameof(logFilePathAccessor));
        }

        public ToolboxWindowType WindowType => ToolboxWindowType.LogWindow;

        public string Title => "Logging";

        public bool Active
        {
            get => active;
            set => active = value;
        }

        public ToolWindowSnapshot CaptureSnapshot() => snapshot;

        internal void RefreshSnapshot()
        {
            if (!Active)
                return;

            string content = ReadLogContent();
            if (string.Equals(previousContent, content, StringComparison.Ordinal))
                return;

            previousContent = content;
            ImmutableArray<ToolWindowRow>.Builder rows = ImmutableArray.CreateBuilder<ToolWindowRow>(1);
            rows.Add(new ToolWindowRow("LogText", content, Color.White, false));
            snapshot = new ToolWindowSnapshot(rows.ToImmutable());
        }

        private string ReadLogContent()
        {
            string logFilePath = logFilePathAccessor();
            if (string.IsNullOrWhiteSpace(logFilePath) || !File.Exists(logFilePath))
                return string.Empty;

            try
            {
                using FileStream stream = File.Open(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException)
            {
                return previousContent;
            }
            catch (UnauthorizedAccessException)
            {
                return previousContent;
            }
        }
    }
}
