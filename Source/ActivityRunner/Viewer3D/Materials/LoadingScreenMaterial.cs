using System.IO;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Processes;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Materials
{
    internal class LoadingScreenMaterial : LoadingMaterial
    {
        public LoadingScreenMaterial(GameHost game)
            : base(game, LoadingTexturePath(game))
        {
        }

        private static string LoadingTexturePath(Game game)
        {
            return Path.Combine(Simulator.Instance.RouteFolder.CurrentFolder,
                ((game.GraphicsDevice.Adapter.IsWideScreen && !string.IsNullOrEmpty(Simulator.Instance.Route.LoadingScreenWide)) ?
                Simulator.Instance.Route.LoadingScreenWide : Simulator.Instance.Route.LoadingScreen) ?? (string.IsNullOrEmpty(Simulator.Instance.Route.Thumbnail) ? "load.ace" : Simulator.Instance.Route.Thumbnail));
        }
    }
}
