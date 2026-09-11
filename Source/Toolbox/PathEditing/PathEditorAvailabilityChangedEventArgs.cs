using System;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    internal sealed class PathEditorAvailabilityChangedEventArgs : EventArgs
    {
        public PathEditor PathEditor { get; }

        public PathEditorAvailabilityChangedEventArgs(PathEditor pathEditor)
        {
            PathEditor = pathEditor;
        }
    }
}
