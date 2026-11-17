using System.Collections.Immutable;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>Safe whole-path orientations prepared without changing either source or committing a result.</summary>
    public sealed record PathCombinationResult
    {
        /// <summary>Viable orientations in stable first-direction, then second-direction order.</summary>
        public ImmutableArray<PathCombinationCandidate> Candidates { get; init; }

        /// <summary>Admission or routing refusal when no orientation is available; otherwise null.</summary>
        public PathEditResult Refusal { get; init; }

        internal PathCombinationResult(ImmutableArray<PathCombinationCandidate> candidates)
        {
            Candidates = candidates;
        }

        internal PathCombinationResult(PathEditResult refusal)
        {
            Candidates = ImmutableArray<PathCombinationCandidate>.Empty;
            Refusal = refusal;
        }
    }

    /// <summary>One safe direction pair, distinct from alternatives within its connecting span.</summary>
    public sealed record PathCombinationCandidate
    {
        /// <summary>Prepared edit to commit only after the caller validates ownership and accepts this orientation.</summary>
        public PathEditResult Combination { get; init; }

        /// <summary>Whether the first saved path is traversed in reverse.</summary>
        public bool FirstReversed { get; init; }

        /// <summary>Whether the second saved path is traversed in reverse.</summary>
        public bool SecondReversed { get; init; }

        internal PathCombinationCandidate(PathEditResult combination, bool firstReversed, bool secondReversed)
        {
            Combination = combination;
            FirstReversed = firstReversed;
            SecondReversed = secondReversed;
        }
    }
}