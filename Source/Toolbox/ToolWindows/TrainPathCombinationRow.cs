using GetText;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>Immutable whole-path orientation choice and its session-owned request witness.</summary>
    internal sealed record TrainPathCombinationRow
    {
        /// <summary>Monotonic preparation request; never reused after cancellation or source replacement.</summary>
        public long RequestVersion { get; init; }
        /// <summary>Index within this request's immutable safe orientations.</summary>
        public int CandidateIndex { get; init; }
        /// <summary>Name of the first independently saved input.</summary>
        public string FirstName { get; init; }
        /// <summary>Name of the second independently saved input.</summary>
        public string SecondName { get; init; }
        /// <summary>Whether the first input is reversed.</summary>
        public bool FirstReversed { get; init; }
        /// <summary>Whether the second input is reversed.</summary>
        public bool SecondReversed { get; init; }
        /// <summary>Starting endpoint label after orientation.</summary>
        public string Start { get; init; }
        /// <summary>Ending endpoint label after orientation.</summary>
        public string End { get; init; }

        /// <summary>Localized direction and endpoint summary, evaluated at the presentation boundary.</summary>
        public string Description => CatalogManager.Catalog.GetString("{0} ({1}); then {2} ({3}). Start: {4}. End: {5}.",
            FirstName, Direction(FirstReversed), SecondName, Direction(SecondReversed), Start, End);

        public TrainPathCombinationRow(long requestVersion, int candidateIndex, string firstName, string secondName,
            bool firstReversed, bool secondReversed, string start, string end)
        {
            RequestVersion = requestVersion;
            CandidateIndex = candidateIndex;
            FirstName = firstName;
            SecondName = secondName;
            FirstReversed = firstReversed;
            SecondReversed = secondReversed;
            Start = start;
            End = end;
        }

        private static string Direction(bool reversed) => reversed
            ? CatalogManager.Catalog.GetString("reversed") : CatalogManager.Catalog.GetString("original direction");
    }
}