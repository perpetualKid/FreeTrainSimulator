namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapViewAdapterSet
    {
        public IMapViewStateAdapter ViewStateAdapter { get; }

        public IMapRenderAdapter RenderAdapter { get; }

        public IMapInteractionAdapter InteractionAdapter { get; }

        public MapViewAdapterSet(IMapViewStateAdapter viewStateAdapter, IMapRenderAdapter renderAdapter, IMapInteractionAdapter interactionAdapter)
        {
            ViewStateAdapter = viewStateAdapter;
            RenderAdapter = renderAdapter;
            InteractionAdapter = interactionAdapter;
        }
    }
}
