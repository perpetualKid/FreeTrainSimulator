namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostSession : IMapHostSession
    {
        private readonly IMapHostResources hostResources;

        public IMapViewStateAdapter ViewStateAdapter { get; }

        public IMapRenderAdapter RenderAdapter { get; }

        public IMapInteractionAdapter InteractionAdapter { get; }

        public IMapOverlayShapeAdapter OverlayShapeAdapter { get; }

        public XnaMapHostSession(IMapHostAdapterBundle adapterBundle)
        {
            hostResources = adapterBundle.HostResources;
            ViewStateAdapter = adapterBundle.ViewStateAdapter;
            RenderAdapter = adapterBundle.RenderAdapter;
            InteractionAdapter = adapterBundle.InteractionAdapter;
            OverlayShapeAdapter = adapterBundle.OverlayShapeAdapter;
        }

        public void Dispose()
        {
            hostResources?.Dispose();
        }
    }
}
