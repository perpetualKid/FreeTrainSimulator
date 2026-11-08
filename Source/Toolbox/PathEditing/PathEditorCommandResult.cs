using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>
    /// Standard result for path editor commands, carrying success/failure feedback and the model produced by the command when available.
    /// </summary>
    internal sealed record PathEditorCommandResult
    {
        public PathEditorCommandResult(bool success, string message, PathEditorMessageKind messageKind, Severity severity, PathModel pathModel)
        {
            Success = success;
            Message = message;
            MessageKind = messageKind;
            Severity = severity;
            PathModel = pathModel;
        }

        public bool Success { get; }

        public string Message { get; }

        /// <summary>
        /// Identifies <see cref="Message"/> among the subset of outcomes the UI localizes and displays verbatim.
        /// <see cref="PathEditorMessageKind.None"/> for outcomes that are not surfaced through dedicated UI strings.
        /// </summary>
        public PathEditorMessageKind MessageKind { get; }

        /// <summary>Original passing-branch template and arguments, without parsing the English message.</summary>
        public FormattableString MessageFormat { get; init; }

        public Severity Severity { get; }

        public PathModel PathModel { get; }

        public static PathEditorCommandResult Succeeded(string message, PathModel pathModel)
        {
            return new PathEditorCommandResult(true, message, PathEditorMessageKind.None, Severity.Information, pathModel);
        }

        public static PathEditorCommandResult Succeeded(string message, PathEditorMessageKind messageKind, PathModel pathModel)
        {
            return new PathEditorCommandResult(true, message, messageKind, Severity.Information, pathModel);
        }

        public static PathEditorCommandResult Failed(string message, PathModel pathModel)
        {
            return new PathEditorCommandResult(false, message, PathEditorMessageKind.None, Severity.Warning, pathModel);
        }

        public static PathEditorCommandResult Failed(string message, PathEditorMessageKind messageKind, PathModel pathModel)
        {
            return new PathEditorCommandResult(false, message, messageKind, Severity.Warning, pathModel);
        }

        public static PathEditorCommandResult Failed(string message, PathModel pathModel, Severity severity)
        {
            return new PathEditorCommandResult(false, message, PathEditorMessageKind.None, severity, pathModel);
        }

        public static PathEditorCommandResult FromPathEditResult(PathEditResult result)
        {
            PathEditorCommandResult commandResult = result.Success
                ? Succeeded(result.Message, result.MessageKind, result.PathModel)
                : Failed(result.Message, result.MessageKind, result.PathModel);
            return commandResult with { MessageFormat = result.MessageFormat };
        }
    }
}
