namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapSessionComposer
    {
        IMapSession Compose(MapSessionRequest request);
    }
}
