using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Toolbox.PopupWindows;
using FreeTrainSimulator.Toolbox.Settings;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Hosted-mode bridge exposing read-only help command/key bindings for a dockable WPF help tool window.
    /// Supports filtering by command or key text, mirroring the legacy Help popup behavior.
    /// </summary>
    internal sealed class HelpToolWindow : IToolboxToolWindow
    {
        internal enum HelpSearchColumn
        {
            Command = 1,
            Key = 2,
        }

        private volatile ToolWindowSnapshot snapshot = ToolWindowSnapshot.Empty;
        private volatile bool active;
        private ImmutableArray<HelpRow> allRows = ImmutableArray<HelpRow>.Empty;
        private string searchText = string.Empty;
        private HelpSearchColumn searchColumn = HelpSearchColumn.Command;
        private bool updateRequired = true;

        public ToolboxWindowType WindowType => ToolboxWindowType.HelpWindow;

        public string Title => "Help";

        public bool Active
        {
            get => active;
            set => active = value;
        }

        public ToolWindowSnapshot CaptureSnapshot() => snapshot;

        internal void SetSearch(string text, HelpSearchColumn column)
        {
            text ??= string.Empty;
            if (string.Equals(searchText, text, StringComparison.Ordinal) && searchColumn == column)
                return;

            searchText = text;
            searchColumn = column;
            updateRequired = true;
        }

        internal void RefreshSnapshot()
        {
            if (!Active)
                return;

            if (!updateRequired)
                return;

            if (allRows.IsDefaultOrEmpty)
                allRows = BuildRows();

            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            foreach (HelpRow row in allRows)
            {
                if (!MatchesFilter(row))
                    continue;

                builder.Add(new ToolWindowRow(row.Command, row.Key, null, false));
            }

            snapshot = new ToolWindowSnapshot(builder.ToImmutable());
            updateRequired = false;
        }

        private bool MatchesFilter(HelpRow row)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            return searchColumn switch
            {
                HelpSearchColumn.Command => row.Command?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false,
                HelpSearchColumn.Key => row.Key?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false,
                _ => true,
            };
        }

        private static ImmutableArray<HelpRow> BuildRows()
        {
            ImmutableArray<HelpRow>.Builder rows = ImmutableArray.CreateBuilder<HelpRow>();

            foreach (UserCommand command in EnumExtension.GetValues<UserCommand>())
            {
                string commandText = command.GetLocalizedDescription();
                string keyText = InputSettings.UserCommands[command]?.ToString() ?? string.Empty;
                rows.Add(new HelpRow(commandText, keyText));
            }

            return rows.ToImmutable();
        }

        private readonly record struct HelpRow(string Command, string Key);
    }
}
