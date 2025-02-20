using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
                PathNodeType _ when (nodeType & PathNodeType.Start) == PathNodeType.Start => BasicTextureType.PathStart,
                PathNodeType _ when (nodeType & PathNodeType.End) == PathNodeType.End => BasicTextureType.PathEnd,
                PathNodeType _ when (nodeType & PathNodeType.Junction) == PathNodeType.Junction => BasicTextureType.PathNormal,
                PathNodeType _ when (nodeType & PathNodeType.Intermediate) == PathNodeType.Intermediate => BasicTextureType.PathNormal,
                PathNodeType _ when (nodeType & PathNodeType.Wait) == PathNodeType.Wait => BasicTextureType.PathWait,
                PathNodeType _ when (nodeType & PathNodeType.Reversal) == PathNodeType.Reversal => BasicTextureType.PathReverse,
                PathNodeType _ when (nodeType & PathNodeType.None) == PathNodeType.None => BasicTextureType.RingCrossed,
                PathNodeType _ when (nodeType & PathNodeType.Invalid) == PathNodeType.Invalid => BasicTextureType.RingCrossed,
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
