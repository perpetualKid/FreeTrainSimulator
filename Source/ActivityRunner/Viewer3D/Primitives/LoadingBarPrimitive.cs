
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Materials;

using Game = Orts.ActivityRunner.Viewer3D.Processes.Game;

namespace Orts.ActivityRunner.Viewer3D.Primitives
{
    internal class LoadingBarPrimitive : LoadingPrimitive
    {
        public LoadingBarPrimitive(Game game)
            : base(game)
        {
        }

        protected override LoadingMaterial GetMaterial(Game game)
        {
            return new LoadingBarMaterial(game);
        }

        protected override VertexPositionTexture[] GetVertices(Game game)
        {
            GetLoadingBarSize(game, out int w, out int h, out float x, out float y);
            return GetLoadingBarCoords(w, h, x, y);
        }

        protected static VertexPositionTexture[] GetLoadingBarCoords(int w, int h, float x, float y)
        {
            return new[] {
                    new VertexPositionTexture(new Vector3(x + 0, -y - 0, -1), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(x + w, -y - 0, -1), new Vector2(1, 0)),
                    new VertexPositionTexture(new Vector3(x + 0, -y - h, -1), new Vector2(0, 1)),
                    new VertexPositionTexture(new Vector3(x + w, -y - h, -1), new Vector2(1, 1)),
                };
        }

        protected static void GetLoadingBarSize(Game game, out int w, out int h, out float x, out float y)
        {
            //int w = game.RenderProcess.DisplaySize.X;
            //int h = game.RenderProcess.DisplaySize.Y / 80;
            //float x = -w / 2 - 0.5f;
            //float y = game.RenderProcess.DisplaySize.Y / 2 - h - 0.5f;
            w = game.RenderProcess.DisplaySize.X;
            h = game.RenderProcess.DisplaySize.Y / 60;//10;
            x = -w / 2 - 0.5f;
            y = game.RenderProcess.DisplaySize.Y / 2 - h - 0.5f;
        }

    }

    class TimetableLoadingBarPrimitive : LoadingBarPrimitive
    {
        public TimetableLoadingBarPrimitive(Game game)
            : base(game)
        {
        }

        protected override VertexPositionTexture[] GetVertices(Game game)
        {
            GetLoadingBarSize(game, out int w, out int h, out float x, out float y);
            y -= h + 1; // Allow for second bar and 1 pixel gap between
            return GetLoadingBarCoords(w, h, x, y);
        }
    }
}
