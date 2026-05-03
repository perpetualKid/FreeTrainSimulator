using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapCoordinateOverlayContext
    {
        PointD ScreenToWorldCoordinates(in Point screenLocation);
    }
}
