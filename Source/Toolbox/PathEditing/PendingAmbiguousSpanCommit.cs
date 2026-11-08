using System.Collections.Generic;
using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    internal sealed class PendingAmbiguousSpanCommit
    {
        /// <summary>The original model restored by undo after accepting the tentative mutation.</summary>
        public PathModel SourceModel { get; }
        /// <summary>The active model instance that owns this pending interaction, including a provisional via insertion.</summary>
        public PathModel OwnershipModel { get; }
        public PathModel TentativeModel { get; }
        public ImmutableArray<int> ChangedNodeIndexes { get; }
        public ImmutableArray<ResolvedPathSpan> AmbiguousSpans { get; }
        public Dictionary<int, int> CandidateSelections { get; }
        public bool ResumeRouteBuilding { get; }

        public PendingAmbiguousSpanCommit(PathModel sourceModel, PathModel ownershipModel, PathModel tentativeModel,
            ImmutableArray<int> changedNodeIndexes, ImmutableArray<ResolvedPathSpan> ambiguousSpans,
            bool resumeRouteBuilding)
        {
            SourceModel = sourceModel;
            OwnershipModel = ownershipModel;
            TentativeModel = tentativeModel;
            ChangedNodeIndexes = changedNodeIndexes;
            AmbiguousSpans = ambiguousSpans;
            CandidateSelections = new Dictionary<int, int>();
            ResumeRouteBuilding = resumeRouteBuilding;
        }
    }
}
