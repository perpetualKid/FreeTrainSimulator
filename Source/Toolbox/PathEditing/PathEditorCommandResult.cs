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
        public PathEditorCommandResult(bool success, string message, Severity severity, PathModel pathModel)
        {
            Success = success;
            Message = message;
            Severity = severity;
            PathModel = pathModel;
        }

        public bool Success { get; }

        public string Message { get; }

        public Severity Severity { get; }

        public PathModel PathModel { get; }

        public static PathEditorCommandResult Succeeded(string message, PathModel pathModel)
        {
            return new PathEditorCommandResult(true, message, Severity.Information, pathModel);
        }

        public static PathEditorCommandResult Failed(string message, PathModel pathModel)
        {
            return new PathEditorCommandResult(false, message, Severity.Warning, pathModel);
        }

        public static PathEditorCommandResult Failed(string message, PathModel pathModel, Severity severity)
        {
            return new PathEditorCommandResult(false, message, severity, pathModel);
        }

        public static PathEditorCommandResult FromPathEditResult(PathEditResult result)
        {
            return result.Success
                ? Succeeded(result.Message, result.PathModel)
                : Failed(result.Message, result.PathModel);
        }
    }
}
