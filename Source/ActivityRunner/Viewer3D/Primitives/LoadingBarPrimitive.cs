
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

        protected override VertexPositionTexture[] GetVerticies(Game game)
        {
            int w = game.RenderProcess.DisplaySize.X;
            int h = game.RenderProcess.DisplaySize.Y / 80;
            float x = -w / 2 - 0.5f;
            float y = game.RenderProcess.DisplaySize.Y / 2 - h - 0.5f;
            return new[] {
                    new VertexPositionTexture(new Vector3(x + 0, -y - 0, -1), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(x + w, -y - 0, -1), new Vector2(1, 0)),
                    new VertexPositionTexture(new Vector3(x + 0, -y - h, -1), new Vector2(0, 1)),
                    new VertexPositionTexture(new Vector3(x + w, -y - h, -1), new Vector2(1, 1)),
                };
        }
    }

}
