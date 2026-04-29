using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapViewport
    {
        bool InsideScreenArea(PointPrimitive pointPrimitive);

        bool InsideScreenArea(VectorPrimitive vectorPrimitive);

        void SetTrackingPosition(in WorldLocation location);

        void SetTrackingPosition(in PointD location);

        void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight);
    }
}
