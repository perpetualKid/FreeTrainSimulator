namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapHostAdapterBundle
    {
        IMapViewStateAdapter ViewStateAdapter { get; }

        IMapRenderAdapter RenderAdapter { get; }

        IMapInteractionAdapter InteractionAdapter { get; }

        IMapOverlayShapeAdapter OverlayShapeAdapter { get; }

        IMapHostResources HostResources { get; }
    }
}
