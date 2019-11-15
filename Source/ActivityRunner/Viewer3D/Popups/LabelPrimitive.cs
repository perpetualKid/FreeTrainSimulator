// COPYRIGHT 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public class LabelPrimitive : RenderPrimitive
    {
        private readonly Label3DMaterial material;

        private readonly IWorldPosition positionSource;
        private readonly string text;
        private Color color;
        private Color outline;

        private readonly float offsetY;

        public LabelPrimitive(Label3DMaterial material, Color color, Color outline, float offsetY, IWorldPosition positionSource, string text)
        {
            this.material = material;
            this.color = color;
            this.outline = outline;
            this.offsetY = offsetY;
            this.positionSource = positionSource;
            this.text = text;
        }

        public void UpdateAlphaBlendRatio(float alphaBlendRatio)
        {
            color.A = outline.A = (byte)MathHelper.Lerp(255, 0, alphaBlendRatio);
        }

        public override void Draw()
        {
            Camera camera = material.CurrentCamera;
            var lineLocation3D = positionSource.WorldPosition.XNAMatrix.Translation;
            lineLocation3D.X += (positionSource.WorldPosition.TileX - camera.TileX) * 2048;
            lineLocation3D.Y += offsetY;
            lineLocation3D.Z += (camera.TileZ - positionSource.WorldPosition.TileZ) * 2048;

            var lineLocation2DStart = graphicsDevice.Viewport.Project(lineLocation3D, camera.XnaProjection, camera.XnaView, Matrix.Identity);
            if (lineLocation2DStart.Z > 1 || lineLocation2DStart.Z < 0)
                return; // Out of range or behind the camera

            lineLocation3D.Y += 10;
            var lineLocation2DEndY = graphicsDevice.Viewport.Project(lineLocation3D, camera.XnaProjection, camera.XnaView, Matrix.Identity).Y;

            var labelLocation2D = material.GetTextLocation((int)lineLocation2DStart.X, (int)lineLocation2DEndY - material.Font.Height, text);
            lineLocation2DEndY = labelLocation2D.Y + material.Font.Height;

            material.Font.Draw(material.SpriteBatch, labelLocation2D, text, color, outline);
            material.SpriteBatch.Draw(material.Texture, new Vector2(lineLocation2DStart.X - 1, lineLocation2DEndY), null, outline, 0, Vector2.Zero, new Vector2(4, lineLocation2DStart.Y - lineLocation2DEndY), SpriteEffects.None, lineLocation2DStart.Z);
            material.SpriteBatch.Draw(material.Texture, new Vector2(lineLocation2DStart.X, lineLocation2DEndY), null, color, 0, Vector2.Zero, new Vector2(2, lineLocation2DStart.Y - lineLocation2DEndY), SpriteEffects.None, lineLocation2DStart.Z);
        }
    }
}
