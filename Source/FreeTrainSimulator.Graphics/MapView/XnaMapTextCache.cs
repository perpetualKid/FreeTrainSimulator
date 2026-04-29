using System;

using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    [Obsolete("Use MapTextTextureCache for the current MapView path.")]
    internal sealed class XnaMapTextCache : IMapTextCache
    {
        private readonly DrawableComponents.TextShape textShape;

        public XnaMapTextCache(DrawableComponents.TextShape textShape)
        {
            this.textShape = textShape ?? throw new ArgumentNullException(nameof(textShape));
        }

        public Texture2D GetTextTexture(string message, System.Drawing.Font font, OutlineRenderOptions outlineRenderOptions = null)
        {
            return textShape.GetTextTexture(message, font, outlineRenderOptions);
        }
    }
}
