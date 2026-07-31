using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>Bindable row for the route candidate list. Observable so it can be updated in place.</summary>
    internal sealed class TrainPathRouteCandidateItemViewModel : ObservableObject
    {
        private int fromNodeIndex;
        private int toNodeIndex;
        private int candidateIndex;
        private string description;

        public TrainPathRouteCandidateItemViewModel(TrainPathRouteCandidateRow row)
        {
            fromNodeIndex = row.FromNodeIndex;
            toNodeIndex = row.ToNodeIndex;
            candidateIndex = row.CandidateIndex;
            description = row.Description;
        }

        public int FromNodeIndex
        {
            get => fromNodeIndex;
            private set => SetProperty(ref fromNodeIndex, value);
        }

        public int ToNodeIndex
        {
            get => toNodeIndex;
            private set => SetProperty(ref toNodeIndex, value);
        }

        public int CandidateIndex
        {
            get => candidateIndex;
            private set => SetProperty(ref candidateIndex, value);
        }

        public string Description
        {
            get => description;
            private set => SetProperty(ref description, value);
        }

        public void Update(TrainPathRouteCandidateRow row)
        {
            FromNodeIndex = row.FromNodeIndex;
            ToNodeIndex = row.ToNodeIndex;
            CandidateIndex = row.CandidateIndex;
            Description = row.Description;
        }
    }
}
