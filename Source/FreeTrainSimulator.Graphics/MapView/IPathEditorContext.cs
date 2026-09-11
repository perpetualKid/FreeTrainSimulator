using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IPathEditorContext
    {
        IMapRenderer Renderer { get; }

        IMapViewport Viewport { get; }

        ToolboxContentMode ContentMode { get; set; }

        PathEditorBase PathEditor { get; set; }

        /// <summary>
        /// Requests an immediate map redraw. The Toolbox map renders on demand (dirty-flagged), so state
        /// changes that are only reflected while drawing - such as moving the selected-node highlight - must
        /// request a redraw explicitly, otherwise the change is not shown until the next incidental repaint.
        /// </summary>
        void RequestRedraw();
    }
}
