using System;

using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapTextCache : IMapTextCache
    {
        private readonly TextShape textShape;

        public XnaMapTextCache(TextShape textShape)
        {
            this.textShape = textShape ?? throw new ArgumentNullException(nameof(textShape));
        }

        public Texture2D GetTextTexture(string message, System.Drawing.Font font, OutlineRenderOptions outlineRenderOptions = null)
        {
            return textShape.GetTextTexture(message, font, outlineRenderOptions);
        }
    }
}
