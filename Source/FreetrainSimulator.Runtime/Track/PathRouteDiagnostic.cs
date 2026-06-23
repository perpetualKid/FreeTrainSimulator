using System;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Diagnostic emitted while validating or resolving a path route.
    /// </summary>
    public sealed record PathRouteDiagnostic
    {
        /// <summary>Diagnostic severity.</summary>
        public PathRouteDiagnosticSeverity Severity { get; init; }

        /// <summary>Stable diagnostic code.</summary>
        public PathRouteDiagnosticCode Code { get; init; }

        /// <summary>Authored node index associated with the diagnostic, or -1 when not node-specific.</summary>
        public int NodeIndex { get; init; }

        /// <summary>Source authored node index for span diagnostics, or -1 when not span-specific.</summary>
        public int FromNodeIndex { get; init; }

        /// <summary>Target authored node index for span diagnostics, or -1 when not span-specific.</summary>
        public int ToNodeIndex { get; init; }

        /// <summary>Human-readable diagnostic message.</summary>
        public string Message { get; init; }

        /// <summary>Suggested repair or review action.</summary>
        public string SuggestedAction { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathRouteDiagnostic"/> record.
        /// </summary>
        public PathRouteDiagnostic(PathRouteDiagnosticSeverity severity,PathRouteDiagnosticCode code,
            string message,int nodeIndex = -1, int fromNodeIndex = -1,int toNodeIndex = -1,string suggestedAction = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            Severity = severity;
            Code = code;
            Message = message;
            NodeIndex = nodeIndex;
            FromNodeIndex = fromNodeIndex;
            ToNodeIndex = toNodeIndex;
            SuggestedAction = suggestedAction;
        }
    }
}
