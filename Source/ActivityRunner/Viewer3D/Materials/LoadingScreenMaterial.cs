using System.IO;

using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Processes;
using Orts.Formats.Msts.Files;

namespace Orts.ActivityRunner.Viewer3D.Materials
{
    internal class LoadingScreenMaterial : LoadingMaterial
    {
        public LoadingScreenMaterial(Game game)
            : base(game)
        {
        }

        private bool IsWideScreen(Game game)
        {
            float x = game.RenderProcess.DisplaySize.X;
            float y = game.RenderProcess.DisplaySize.Y;

            return (x / y > 1.5);
        }

        protected override Texture2D GetTexture(Game game)
        {
            Texture2D texture;
            GraphicsDevice gd = game.RenderProcess.GraphicsDevice;
            string defaultScreen = "load.ace";

            string loadingScreen = Program.Simulator.TRK.Route.LoadingScreen;
            if (IsWideScreen(game))
            {
                string loadingScreenWide = Program.Simulator.TRK.Route.LoadingScreenWide;
                loadingScreen = loadingScreenWide ?? loadingScreen;
            }
            loadingScreen = loadingScreen ?? defaultScreen;
            var path = Path.Combine(Program.Simulator.RoutePath, loadingScreen);
            if (Path.GetExtension(path) == ".dds" && File.Exists(path))
            {
                DDSLib.DDSFromFile(path, gd, true, out texture);
            }
            else if (Path.GetExtension(path) == ".ace")
            {
                var alternativeTexture = Path.ChangeExtension(path, ".dds");

                if (File.Exists(alternativeTexture) && game.Settings.PreferDDSTexture)
                {
                    DDSLib.DDSFromFile(alternativeTexture, gd, true, out texture);
                }
                else if (File.Exists(path))
                {
                    texture = AceFile.Texture2DFromFile(gd, path);
                }
                else
                {
                    path = Path.Combine(Program.Simulator.RoutePath, defaultScreen);
                    if (File.Exists(path))
                    {
                        texture = AceFile.Texture2DFromFile(gd, path);
                    }
                    else
                    {
                        texture = null;
                    }
                }

            }
            else
            {
                texture = null;
            }
            return texture;
        }
    }
}
