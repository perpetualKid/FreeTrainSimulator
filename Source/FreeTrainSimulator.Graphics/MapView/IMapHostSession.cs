namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapHostSession
    {
        IMapViewStateAdapter ViewStateAdapter { get; }

        IMapRenderAdapter RenderAdapter { get; }

        IMapInteractionAdapter InteractionAdapter { get; }

        IMapOverlayShapeAdapter OverlayShapeAdapter { get; }

        void Dispose();
    }
}
