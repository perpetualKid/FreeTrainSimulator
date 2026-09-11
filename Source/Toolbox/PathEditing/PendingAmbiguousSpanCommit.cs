using System.Collections.Generic;
using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    internal sealed class PendingAmbiguousSpanCommit
    {
        public PathModel SourceModel { get; }
        public PathModel TentativeModel { get; }
        public ImmutableArray<int> ChangedNodeIndexes { get; }
        public ImmutableArray<ResolvedPathSpan> AmbiguousSpans { get; }
        public Dictionary<int, int> CandidateSelections { get; }
        public bool ResumeRouteBuilding { get; }

        public PendingAmbiguousSpanCommit(PathModel sourceModel, PathModel tentativeModel,
            ImmutableArray<int> changedNodeIndexes, ImmutableArray<ResolvedPathSpan> ambiguousSpans,
            bool resumeRouteBuilding = false)
        {
            SourceModel = sourceModel;
            TentativeModel = tentativeModel;
            ChangedNodeIndexes = changedNodeIndexes;
            AmbiguousSpans = ambiguousSpans;
            CandidateSelections = new Dictionary<int, int>();
            ResumeRouteBuilding = resumeRouteBuilding;
        }
    }
}
