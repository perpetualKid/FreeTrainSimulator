using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IPathEditorServices
    {
        TrackWorld TrackWorld { get; }

        EditorTrainPath CreateEditorTrainPath(PathModel pathModel);
    }
}
