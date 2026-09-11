namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// One equal-cost route candidate of an ambiguous span of the currently edited train path.
    /// </summary>
    internal readonly record struct TrainPathRouteCandidateRow
    {
        public TrainPathRouteCandidateRow(int fromNodeIndex, int toNodeIndex, int candidateIndex, string description)
        {
            FromNodeIndex = fromNodeIndex;
            ToNodeIndex = toNodeIndex;
            CandidateIndex = candidateIndex;
            Description = description;
        }

        /// <summary>Authored node index the ambiguous span starts at.</summary>
        public int FromNodeIndex { get; }

        /// <summary>Authored node index the ambiguous span ends at.</summary>
        public int ToNodeIndex { get; }

        /// <summary>Index of the candidate within the span, stable across resolutions.</summary>
        public int CandidateIndex { get; }

        /// <summary>Human readable summary of the route the candidate takes.</summary>
        public string Description { get; }
    }
}
