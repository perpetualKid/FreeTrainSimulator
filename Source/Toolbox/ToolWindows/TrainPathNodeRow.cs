using FreeTrainSimulator.Common;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// One node row of the currently edited train path (index, node type, validity).
    /// </summary>
    internal readonly record struct TrainPathNodeRow
    {
        public TrainPathNodeRow(int index, PathNodeType nodeType, bool valid)
            : this(index, nodeType, valid, 0, -1, -1, null, null, null, null, null)
        {
        }

        public TrainPathNodeRow(int index, PathNodeType nodeType, bool valid, int trackNodeIndex, int nextMainNode, int nextSidingNode, int? waitTime, string validation)
            : this(index, nodeType, valid, trackNodeIndex, nextMainNode, nextSidingNode, waitTime, validation, null, null, null)
        {
        }

        public TrainPathNodeRow(int index, PathNodeType nodeType, bool valid, int trackNodeIndex, int nextMainNode, int nextSidingNode, int? waitTime,
            string validation, int? nearestTrackNodeIndex, int? nearestTrackSectionIndex, double? nearestTrackDistanceMeters)
        {
            Index = index;
            NodeType = nodeType;
            Valid = valid;
            TrackNodeIndex = trackNodeIndex;
            NextMainNode = nextMainNode;
            NextSidingNode = nextSidingNode;
            WaitTime = waitTime;
            Validation = validation;
            NearestTrackNodeIndex = nearestTrackNodeIndex;
            NearestTrackSectionIndex = nearestTrackSectionIndex;
            NearestTrackDistanceMeters = nearestTrackDistanceMeters;
        }

        public int Index { get; }

        public PathNodeType NodeType { get; }

        public bool Valid { get; }

        public int TrackNodeIndex { get; }

        public int NextMainNode { get; }

        public int NextSidingNode { get; }

        public int? WaitTime { get; }

        public string Validation { get; }

        public int? NearestTrackNodeIndex { get; }

        public int? NearestTrackSectionIndex { get; }

        public double? NearestTrackDistanceMeters { get; }
    }
}
