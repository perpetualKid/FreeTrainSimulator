using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

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

        public EditorTrainPath CreateEditorTrainPath(PathModelHeader pathModelHeader)
        {
            return pathModelHeader == null
                ? null
                : new EditorTrainPath(Task.Run(async () => await pathModelHeader.GetExtended(CancellationToken.None).ConfigureAwait(false)).Result, TrackWorld);
        }
    }
}
