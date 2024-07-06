using System;

using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class TrainPathItemControl : WindowTextureControl
    {
        private readonly BasicTextureType textureType;

        public TrainPathItemControl(FormBase window, PathNodeType nodeType) :
            base(window ?? throw new ArgumentNullException(nameof(window)), 0, 0, window.Owner.TextFontDefault.Height, window.Owner.TextFontDefault.Height)
        {
            textureType = nodeType switch
            {
                PathNodeType.Start => BasicTextureType.PathStart,
                PathNodeType.End => BasicTextureType.PathEnd,
                PathNodeType.Junction => BasicTextureType.PathNormal,
                PathNodeType.Intermediate => BasicTextureType.PathNormal,
                PathNodeType.Wait => BasicTextureType.PathWait,
                PathNodeType.Reversal => BasicTextureType.PathReverse,
                PathNodeType.Temporary => BasicTextureType.RingCrossed,
                _ => throw new NotImplementedException(),
            };
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Rectangle destination = Bounds;
            destination.Offset(offset);
            Window.Owner.BasicShapes.DrawTexture(textureType, destination, Color.White, spriteBatch);
            base.Draw(spriteBatch, offset);
        }
    }
}
