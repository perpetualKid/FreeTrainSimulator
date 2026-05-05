using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed record MapSessionRequest(
        ContentBase Content,
        IMapInsetHost InsetHost,
        IMapTextureHelperHost TextureHelperHost);
}
