
using FreeTrainSimulator.Common.Position;

namespace Orts.Graphics.MapView.Widgets
{
    internal interface IDrawable<T> where T : PointPrimitive
    {
        void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);
    }
}
