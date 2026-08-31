using FreeTrainSimulator.Common;
using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>Bindable row for the path-node list. Observable so it can be updated in place.</summary>
    internal sealed class TrainPathNodeItemViewModel : ObservableObject
    {
        private int index;
        private PathNodeType nodeType;
        private bool valid;
        private int trackNodeIndex;
        private int nextMainNode;
        private int nextSidingNode;
        private int? waitTime;
        private string validation;
        private int? nearestTrackNodeIndex;
        private int? nearestTrackSectionIndex;
        private double? nearestTrackDistanceMeters;

        public TrainPathNodeItemViewModel(int index, PathNodeType nodeType, bool valid)
        {
            this.index = index;
            this.nodeType = nodeType;
            this.valid = valid;
            nextMainNode = -1;
            nextSidingNode = -1;
        }

        public TrainPathNodeItemViewModel(TrainPathNodeRow row) : this(row.Index, row.NodeType, row.Valid)
        {
            Update(row);
        }

        public int Index
        {
            get => index;
            private set => SetProperty(ref index, value);
        }

        public PathNodeType NodeType
        {
            get => nodeType;
            private set
            {
                if (SetProperty(ref nodeType, value))
                {
                    OnPropertyChanged(nameof(HasWaitPoint));
                    OnPropertyChanged(nameof(HasReversalPoint));
                }
            }
        }

        /// <summary><see langword="true"/> when the node carries a wait/stop marker.</summary>
        public bool HasWaitPoint => nodeType.Includes(PathNodeType.Wait);

        /// <summary><see langword="true"/> when the node carries a reversal marker.</summary>
        public bool HasReversalPoint => nodeType.Includes(PathNodeType.Reversal);

        public bool Valid
        {
            get => valid;
            private set => SetProperty(ref valid, value);
        }

        public int TrackNodeIndex
        {
            get => trackNodeIndex;
            private set => SetProperty(ref trackNodeIndex, value);
        }

        public int NextMainNode
        {
            get => nextMainNode;
            private set => SetProperty(ref nextMainNode, value);
        }

        public int NextSidingNode
        {
            get => nextSidingNode;
            private set => SetProperty(ref nextSidingNode, value);
        }

        public int? WaitTime
        {
            get => waitTime;
            private set => SetProperty(ref waitTime, value);
        }

        public string Validation
        {
            get => validation;
            private set => SetProperty(ref validation, value);
        }

        public int? NearestTrackNodeIndex
        {
            get => nearestTrackNodeIndex;
            private set => SetProperty(ref nearestTrackNodeIndex, value);
        }

        public int? NearestTrackSectionIndex
        {
            get => nearestTrackSectionIndex;
            private set => SetProperty(ref nearestTrackSectionIndex, value);
        }

        public double? NearestTrackDistanceMeters
        {
            get => nearestTrackDistanceMeters;
            private set => SetProperty(ref nearestTrackDistanceMeters, value);
        }

        public void Update(int index, PathNodeType nodeType, bool valid)
        {
            Index = index;
            NodeType = nodeType;
            Valid = valid;
        }

        public void Update(TrainPathNodeRow row)
        {
            Update(row.Index, row.NodeType, row.Valid);
            TrackNodeIndex = row.TrackNodeIndex;
            NextMainNode = row.NextMainNode;
            NextSidingNode = row.NextSidingNode;
            WaitTime = row.WaitTime;
            Validation = row.Validation;
            NearestTrackNodeIndex = row.NearestTrackNodeIndex;
            NearestTrackSectionIndex = row.NearestTrackSectionIndex;
            NearestTrackDistanceMeters = row.NearestTrackDistanceMeters;
        }
    }
}
