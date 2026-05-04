using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapViewStateAdapter
    {
        double Scale { get; }

        PointD CenterPoint { get; }

        Point WindowSize { get; }

        PointD TopLeftBound { get; }

        PointD BottomRightBound { get; }

        PointD WorldPosition { get; }

        System.Drawing.Font CurrentFont { get; }

        System.Drawing.Font ConstantSizeFont { get; }

        PointD ScreenToWorldCoordinates(in Point screenLocation);

        Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation);

        Vector2 WorldToScreenCoordinates(in PointD location);

        float WorldToScreenSize(double worldSize, int minScreenSize = 1);

        bool InsideScreenArea(PointPrimitive pointPrimitive);

        bool InsideScreenArea(VectorPrimitive vectorPrimitive);
    }
}
