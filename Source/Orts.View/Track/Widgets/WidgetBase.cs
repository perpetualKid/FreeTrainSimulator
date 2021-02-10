using Microsoft.Xna.Framework;

using Orts.Common.Position;

using SharpDX.Direct3D11;

namespace Orts.View.Track.Widgets
{
    internal abstract class WidgetBase
    {
        internal float Size;
    }

    internal abstract class PointWidget: WidgetBase, ITileCoordinate<Tile>
    {
        private protected PointD location;

        private protected Tile tile;

        public ref readonly Tile Tile => ref tile;

        internal ref readonly PointD Location => ref location;

        internal abstract void Draw(ContentArea contentArea);
    }

    internal abstract class VectorWidget : PointWidget
    {
        private protected PointD vector;

        internal ref readonly PointD Vector => ref vector;
    }
}
