using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// One resolver diagnostic of the currently edited train path, flattened into immutable UI-safe data.
    /// </summary>
    internal readonly record struct TrainPathDiagnosticRow
    {
        public TrainPathDiagnosticRow(PathRouteDiagnosticSeverity severity, PathRouteDiagnosticCode code, string message,
            int nodeIndex, int fromNodeIndex, int toNodeIndex, string suggestedAction, bool canRepair)
        {
            Severity = severity;
            Code = code;
            Message = message;
            NodeIndex = nodeIndex;
            FromNodeIndex = fromNodeIndex;
            ToNodeIndex = toNodeIndex;
            SuggestedAction = suggestedAction;
            CanRepair = canRepair;
        }

        /// <summary>Diagnostic severity.</summary>
        public PathRouteDiagnosticSeverity Severity { get; }

        /// <summary>Stable diagnostic code.</summary>
        public PathRouteDiagnosticCode Code { get; }

        /// <summary>Human-readable diagnostic message.</summary>
        public string Message { get; }

        /// <summary>Authored node index associated with the diagnostic, or -1 when not node-specific.</summary>
        public int NodeIndex { get; }

        /// <summary>Source authored node index for span diagnostics, or -1 when not span-specific.</summary>
        public int FromNodeIndex { get; }

        /// <summary>Target authored node index for span diagnostics, or -1 when not span-specific.</summary>
        public int ToNodeIndex { get; }

        /// <summary>Suggested repair or review action.</summary>
        public string SuggestedAction { get; }

        /// <summary>Whether the existing selected-node repair operation can repair this diagnostic target.</summary>
        public bool CanRepair { get; }

        /// <summary>Whether the diagnostic identifies one authored node.</summary>
        public bool HasNodeTarget => NodeIndex >= 0;

        /// <summary>Whether the diagnostic identifies an authored path span.</summary>
        public bool HasSpanTarget => FromNodeIndex >= 0 && ToNodeIndex >= 0;
    }
}
