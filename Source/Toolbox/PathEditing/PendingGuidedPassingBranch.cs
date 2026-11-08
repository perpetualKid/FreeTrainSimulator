using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>A private passing-branch draft; it is never the editor's committed or transient model.</summary>
    internal sealed class PendingGuidedPassingBranch
    {
        /// <summary>The committed path which owns this interaction.</summary>
        public PathModel SourceModel { get; }
        /// <summary>The load/edit witness at the beginning of the draft.</summary>
        public long SourceRevision { get; }
        /// <summary>The unique rejoin-to-via interaction witness.</summary>
        public long InteractionRevision { get; }
        /// <summary>The unchanged main-route departure index.</summary>
        public int StartNodeIndex { get; }
        /// <summary>The selected main-route rejoin before via insertion.</summary>
        public int RejoinNodeIndex { get; }
        /// <summary>The private authored branch, including its one forced via after preview.</summary>
        public PathModel TentativeModel { get; set; }
        /// <summary>The snapped via last successfully previewed.</summary>
        public PathNode ViaAnchor { get; set; }
        /// <summary>The complete correlated branch resolution, not independently joined legs.</summary>
        public PathRouteResolution Resolution { get; set; }
        /// <summary>Every initially ambiguous leg still requiring explicit acceptance.</summary>
        public ImmutableHashSet<int> RequiredSpanSources { get; set; }
        /// <summary>Explicitly accepted passing-span choices, never main-route keys.</summary>
        public Dictionary<int, ResolvedRouteCandidate> CandidateSelections { get; }

        /// <summary>Creates a draft owned by a committed source and a unique interaction witness.</summary>
        public PendingGuidedPassingBranch(PathModel sourceModel, long sourceRevision, long interactionRevision,
            int startNodeIndex, int rejoinNodeIndex)
        {
            ArgumentNullException.ThrowIfNull(sourceModel);
            SourceModel = sourceModel;
            SourceRevision = sourceRevision;
            InteractionRevision = interactionRevision;
            StartNodeIndex = startNodeIndex;
            RejoinNodeIndex = rejoinNodeIndex;
            RequiredSpanSources = ImmutableHashSet<int>.Empty;
            CandidateSelections = new Dictionary<int, ResolvedRouteCandidate>();
        }
    }
}
