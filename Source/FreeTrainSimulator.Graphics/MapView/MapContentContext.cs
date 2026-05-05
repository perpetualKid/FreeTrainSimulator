namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed record MapContentContext(
        IMapRuntimeServices RuntimeServices,
        IMapSessionComposer SessionComposer,
        IMapInsetHost InsetHost,
        IMapTextureHelperHost TextureHelperHost);
}
