using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// One available train path in the hosted train-path tool window's path list.
    /// </summary>
    internal readonly record struct TrainPathListRow
    {
        public TrainPathListRow(string id, string name, PathValidationState validationState)
            : this(id, name, validationState, false)
        {
        }

        public TrainPathListRow(string id, string name, PathValidationState validationState, bool hasUnsavedChanges)
        {
            Id = id;
            Name = name;
            ValidationState = validationState;
            HasUnsavedChanges = hasUnsavedChanges;
        }

        /// <summary>Unique id of the path.</summary>
        public string Id { get; }

        /// <summary>Display name of the path.</summary>
        public string Name { get; }

        /// <summary>Persisted validation state of the path (valid, invalid, or not yet validated).</summary>
        public PathValidationState ValidationState { get; }

        /// <summary>Whether the path holds edits that have not been persisted yet.</summary>
        public bool HasUnsavedChanges { get; }
    }
}
