using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapRulerOverlayContext
    {
        bool UseMetricUnits { get; }

        double Scale { get; }

        Point WindowSize { get; }
    }
}
