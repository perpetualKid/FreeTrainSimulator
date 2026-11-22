using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>One localized operation choice for the selected path node's existing wait inspector.</summary>
    internal sealed record TrainPathWaitOperationOption
    {
        public WaitOperationType OperationType { get; }
        public string DisplayName { get; }

        public TrainPathWaitOperationOption(WaitOperationType operationType, string displayName)
        {
            OperationType = operationType;
            DisplayName = displayName;
        }
    }
}
