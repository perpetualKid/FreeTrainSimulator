using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapLocationContext
    {
        PointD WorldPosition { get; }
    }
}
