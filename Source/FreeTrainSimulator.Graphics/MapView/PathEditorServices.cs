using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class PathEditorServices : IPathEditorServices
    {
        public TrackWorld TrackWorld { get; }

        public PathEditorServices(TrackWorld trackWorld)
        {
            TrackWorld = trackWorld ?? throw new System.ArgumentNullException(nameof(trackWorld));
        }

        public EditorTrainPath CreateEditorTrainPath(PathModel pathModel)
        {
            return pathModel == null ? null : new EditorTrainPath(pathModel, TrackWorld);
        }
    }
}
