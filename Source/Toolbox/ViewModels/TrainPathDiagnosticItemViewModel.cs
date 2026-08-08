using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>Bindable row for one resolver diagnostic.</summary>
    internal sealed class TrainPathDiagnosticItemViewModel : ObservableObject
    {
        private PathRouteDiagnosticSeverity severity;
        private PathRouteDiagnosticCode code;
        private string message;
        private int nodeIndex;
        private int fromNodeIndex;
        private int toNodeIndex;
        private string suggestedAction;
        private bool canRepair;

        public TrainPathDiagnosticItemViewModel(TrainPathDiagnosticRow row)
        {
            Update(row);
        }

        public PathRouteDiagnosticSeverity Severity
        {
            get => severity;
            private set => SetProperty(ref severity, value);
        }

        public PathRouteDiagnosticCode Code
        {
            get => code;
            private set => SetProperty(ref code, value);
        }

        public string Message
        {
            get => message;
            private set => SetProperty(ref message, value);
        }

        public int NodeIndex
        {
            get => nodeIndex;
            private set
            {
                if (SetProperty(ref nodeIndex, value))
                    OnPropertyChanged(nameof(Target));
            }
        }

        public int FromNodeIndex
        {
            get => fromNodeIndex;
            private set
            {
                if (SetProperty(ref fromNodeIndex, value))
                    OnPropertyChanged(nameof(Target));
            }
        }

        public int ToNodeIndex
        {
            get => toNodeIndex;
            private set
            {
                if (SetProperty(ref toNodeIndex, value))
                    OnPropertyChanged(nameof(Target));
            }
        }

        public string SuggestedAction
        {
            get => suggestedAction;
            private set => SetProperty(ref suggestedAction, value);
        }

        public bool CanRepair
        {
            get => canRepair;
            private set => SetProperty(ref canRepair, value);
        }

        public string Target => NodeIndex >= 0
            ? $"Node {NodeIndex}"
            : FromNodeIndex >= 0 && ToNodeIndex >= 0
                ? $"Nodes {FromNodeIndex}-{ToNodeIndex}"
                : string.Empty;

        public bool IsAmbiguousRoute => Code == PathRouteDiagnosticCode.AmbiguousRoute;

        public void Update(TrainPathDiagnosticRow row)
        {
            Severity = row.Severity;
            Code = row.Code;
            Message = row.Message;
            NodeIndex = row.NodeIndex;
            FromNodeIndex = row.FromNodeIndex;
            ToNodeIndex = row.ToNodeIndex;
            SuggestedAction = row.SuggestedAction;
            CanRepair = row.CanRepair;
        }
    }
}
