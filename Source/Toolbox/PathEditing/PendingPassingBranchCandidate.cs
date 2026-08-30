using System;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    internal sealed class PendingPassingBranchCandidate
    {
        public PendingPassingBranchCandidate(PathModel sourceModel, int startNodeIndex, int rejoinNodeIndex, ResolvedPathSpan span)
        {
            SourceModel = sourceModel ?? throw new ArgumentNullException(nameof(sourceModel));
            StartNodeIndex = startNodeIndex;
            RejoinNodeIndex = rejoinNodeIndex;
            Span = span ?? throw new ArgumentNullException(nameof(span));
        }

        public PathModel SourceModel { get; }

        public int StartNodeIndex { get; }

        public int RejoinNodeIndex { get; }

        public ResolvedPathSpan Span { get; }
    }
}
