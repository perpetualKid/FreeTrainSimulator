namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapHostAdapterBundle
    {
        IMapViewStateAdapter ViewStateAdapter { get; }

        IMapRenderAdapter RenderAdapter { get; }

        IMapInteractionAdapter InteractionAdapter { get; }

        IMapOverlayShapeAdapter OverlayShapeAdapter { get; }

        IMapHostResources HostResources { get; }
    }
}
