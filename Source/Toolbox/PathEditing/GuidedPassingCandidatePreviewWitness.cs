using System;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>Freezes guided candidate ordering without making preview selection part of draft ownership.</summary>
    internal sealed class GuidedPassingCandidatePreviewWitness
    {
        /// <summary>The unique, fixed interaction revision assigned when the guided draft began.</summary>
        public long InteractionRevision { get; }

        /// <summary>The exact private authored model whose candidate ordinals were captured.</summary>
        public PathModel TentativeModel { get; }

        /// <summary>The immutable correlated resolution defining candidate ordinals before any leg acceptance.</summary>
        public PathRouteResolution Resolution { get; }

        /// <summary>The workflow phase in which candidate previews were admitted.</summary>
        public PassingBranchAuthoringPhase Phase { get; }

        /// <summary>Captures structural ownership independently of the full command and menu revision.</summary>
        public GuidedPassingCandidatePreviewWitness(long interactionRevision, PathModel tentativeModel,
            PathRouteResolution resolution, PassingBranchAuthoringPhase phase)
        {
            ArgumentNullException.ThrowIfNull(tentativeModel);
            ArgumentNullException.ThrowIfNull(resolution);
            InteractionRevision = interactionRevision;
            TentativeModel = tentativeModel;
            Resolution = resolution;
            Phase = phase;
        }
    }
}
