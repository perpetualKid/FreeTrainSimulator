using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapOverlayContext
    {
        Rectangle ContentBounds { get; }

        bool UseMetricUnits { get; }

        BasicShapes BasicShapes { get; }

        double Scale { get; }

        Point WindowSize { get; }

        PointD TopLeftBound { get; }

        PointD BottomRightBound { get; }

        PointD ScreenToWorldCoordinates(in Point screenLocation);
    }
}
