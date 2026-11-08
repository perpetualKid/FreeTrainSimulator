using System;

using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    public class PathEditorChangedEventArgs : EventArgs
    {
        /// <summary>The current rendered path associated with this change.</summary>
        public TrainPathBase Path { get; }

        /// <summary>The completed preview outcome, or null for a change without command feedback.</summary>
        internal PathEditorCommandResult PreviewResult { get; }

        /// <summary>Reports a path change without preview command feedback.</summary>
        public PathEditorChangedEventArgs(TrainPathBase path) : this(path, null)
        {
        }

        /// <summary>Reports the actual completed preview result along with the unchanged committed path.</summary>
        internal PathEditorChangedEventArgs(TrainPathBase path, PathEditorCommandResult previewResult)
        {
            Path = path;
            PreviewResult = previewResult;
        }
    }
}
