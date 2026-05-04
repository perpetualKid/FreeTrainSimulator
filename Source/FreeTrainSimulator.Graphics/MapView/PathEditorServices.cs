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
        private readonly Game game;

        public TrackWorld TrackWorld { get; }

        public PathEditorServices(Game game)
        {
            this.game = game;
            TrackWorld = TrackWorld.GameInstance(game);
        }

        public EditorTrainPath CreateEditorTrainPath(PathModel pathModel)
        {
            return pathModel == null ? null : new EditorTrainPath(pathModel, game);
        }

        public EditorTrainPath CreateEditorTrainPath(PathModelHeader pathModelHeader)
        {
            return pathModelHeader == null
                ? null
                : new EditorTrainPath(Task.Run(async () => await pathModelHeader.GetExtended(CancellationToken.None).ConfigureAwait(false)).Result, game);
        }
    }
}
