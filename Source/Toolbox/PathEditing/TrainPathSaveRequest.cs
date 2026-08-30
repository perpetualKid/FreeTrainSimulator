using System;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>Immutable metadata and identity captured from the active editor for persistence.</summary>
    internal sealed class TrainPathSaveState
    {
        public TrainPathSaveState(PathModelHeader pathDetails, string sourcePathId)
        {
            PathDetails = pathDetails ?? throw new ArgumentNullException(nameof(pathDetails));
            SourcePathId = sourcePathId;
        }

        public PathModelHeader PathDetails { get; }

        public string SourcePathId { get; }
    }

    /// <summary>Captures the explicit identity intent selected in the train-path save dialog.</summary>
    internal sealed class TrainPathSaveRequest
    {
        public TrainPathSaveRequest(PathModelHeader pathDetails, string sourcePathId, bool overwriteConfirmed)
        {
            PathDetails = pathDetails ?? throw new ArgumentNullException(nameof(pathDetails));
            SourcePathId = sourcePathId;
            OverwriteConfirmed = overwriteConfirmed;
        }

        public PathModelHeader PathDetails { get; }

        public string SourcePathId { get; }

        public bool OverwriteConfirmed { get; }

        public bool IsSaveAs => !string.Equals(SourcePathId, PathDetails.Id, StringComparison.OrdinalIgnoreCase);

        public bool CanSubmit(bool targetPathExists) => !IsSaveAs || !targetPathExists || OverwriteConfirmed;

        public static PathModelHeader PreparePathDetails(PathModelHeader pathDetails, string sourcePathId)
        {
            ArgumentNullException.ThrowIfNull(pathDetails);

            return string.Equals(sourcePathId, PathEditor.NewPathId, StringComparison.Ordinal)
                ? pathDetails with { Id = pathDetails.Name?.Trim() }
                : pathDetails;
        }
    }
}
