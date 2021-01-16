namespace Orts.View.Track.Widgets
{
    internal abstract class WidgetBase
    {
        internal float Size;
    }

    internal abstract class PointWidget: WidgetBase
    {
        private protected PointD location;

        internal ref readonly PointD Location => ref location;

        internal abstract void Draw(ContentArea contentArea);
    }

    internal abstract class VectorWidget : PointWidget
    {
        private protected PointD vector;

        internal ref readonly PointD Vector => ref vector;
    }
}
