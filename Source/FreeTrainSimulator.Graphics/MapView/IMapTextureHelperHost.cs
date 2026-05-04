using FreeTrainSimulator.Graphics.Xna;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapTextureHelperHost
    {
        void Enable(IMapBaseOverlayContext overlayContext);

        void Disable();
    }
}
