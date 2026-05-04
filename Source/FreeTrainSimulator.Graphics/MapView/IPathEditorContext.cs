using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IPathEditorContext
    {
        Game Game { get; }

        IMapRenderer Renderer { get; }

        IMapViewport Viewport { get; }

        ToolboxContentMode ContentMode { get; set; }

        PathEditorBase PathEditor { get; set; }
    }
}
