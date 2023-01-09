using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Formats.Msts;
using Orts.Graphics.MapView.Shapes;

using SharpDX.DirectWrite;

namespace Orts.Graphics.Window.Controls
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
                PathNodeType.Normal => BasicTextureType.PathNormal,
                PathNodeType.Intermediate => BasicTextureType.PathNormal,
                PathNodeType.Wait => BasicTextureType.PathWait,
                PathNodeType.SidingStart => BasicTextureType.PathNormal,
                PathNodeType.SidingEnd => BasicTextureType.PathNormal,
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
