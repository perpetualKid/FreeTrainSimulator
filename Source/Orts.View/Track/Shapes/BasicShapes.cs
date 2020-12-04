using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;

namespace Orts.View.Track.Shapes
{
    public static class BasicShapes
    {
        public static EnumArray<Texture2D, BasicTextureType> BasicTextures { get; } = new EnumArray<Texture2D, BasicTextureType>();

        /// <summary>
        /// Some initialization needed for actual drawing
        /// </summary>
        /// <param name="graphicsDevice">The graphics device used</param>
        /// <param name="spriteBatchIn">The spritebatch to use for drawing</param>
        /// <param name="contentPath">The full directory name where content like .png files can be found</param>
        public static void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatchIn)
        {
            BasicTextures[BasicTextureType.BlankPixel] = new Texture2D(graphicsDevice, 1, 1);
            BasicTextures[BasicTextureType.BlankPixel].SetData(new[] { Color.White });

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Orts.View.Resources.Signal.png");
            Texture2D signal = Texture2D.FromStream(graphicsDevice, stream);
            BasicTextures[BasicTextureType.Signal] = signal;
        }
    }
}
