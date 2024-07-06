namespace FreeTrainSimulator.Common.Position
{
    public interface ITileCoordinate
    {
        ref readonly Tile Tile { get; }
    }

    public interface ITileCoordinateVector : ITileCoordinate
    {
        ref readonly Tile OtherTile { get; }
    }
}
