
using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class JunctionSegment: PointWidget
    {
        private const int diameter = 3;

        public IJunction Junction { get; }

        public JunctionSegment(TrackJunctionNode junctionNode)
        {
            Size = diameter;
            ref readonly WorldLocation location = ref junctionNode.UiD.Location;
            base.location = PointD.FromWorldLocation(location);
            base.tile = new Tile(location.TileX, location.TileZ);
            Junction = RuntimeData.Instance.RuntimeReferenceResolver?.SwitchById(junctionNode.TrackCircuitCrossReferences[0].Index);
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = contentArea.Scale switch
            {
                double i when i < 0.5 => 30,
                double i when i < 0.75 => 15,
                double i when i < 1 => 10,
                double i when i < 3 => 7,
                double i when i < 5 => 5,
                double i when i < 8 => 4,
                _ => 3,
            };

            Color drawColor = GetColor<JunctionSegment>(colorVariation);
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }
    }
}
