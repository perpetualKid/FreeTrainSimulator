using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;

namespace Orts.View.Track.Shapes
{
    public static class BasicShapes
    {
        public static EnumArray<Texture2D, BasicTextureType> BasicTextures { get; } = new EnumArray<Texture2D, BasicTextureType>();
        public static EnumArray<Texture2D, BasicTextureType> BasicHighlightTextures { get; } = new EnumArray<Texture2D, BasicTextureType>();

        private static GraphicsDevice graphicsDevice;
        private static SpriteBatch spriteBatch;

        /// <summary>
        /// Some initialization needed for actual drawing
        /// </summary>
        /// <param name="graphicsDevice">The graphics device used</param>
        /// <param name="spriteBatchIn">The spritebatch to use for drawing</param>
        /// <param name="contentPath">The full directory name where content like .png files can be found</param>
        public static void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            BasicShapes.graphicsDevice = graphicsDevice;
            BasicShapes.spriteBatch = spriteBatch;
            BasicTextures[BasicTextureType.BlankPixel] = new Texture2D(graphicsDevice, 1, 1);
            BasicTextures[BasicTextureType.BlankPixel].SetData(new[] { Color.White });

            LoadTexturesFromResources();
        }

        #region private implementations
        private static void LoadTexturesFromResources()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            Parallel.ForEach(assembly.GetManifestResourceNames(), new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, resourceName =>
        {
            string textureName = resourceName.Split('.').Reverse().Skip(1).Take(1).FirstOrDefault();
            if (!EnumExtension.GetValue(textureName, out BasicTextureType textureValue))
                return;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                Texture2D texture = Texture2D.FromStream(graphicsDevice, stream);
                BasicTextures[textureValue] = PrepapreColorScaledTexture(texture, 0);
                BasicHighlightTextures[textureValue] = PrepapreColorScaledTexture(texture, 80); ;
            }
        });
        }

        private static Texture2D PrepapreColorScaledTexture(Texture2D texture, byte offset)
        {
            int pixelCount = texture.Width * texture.Height;
            Color[] pixels = new Color[pixelCount];
            texture.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = HighlightColor(pixels[i], offset);
            }

            Texture2D outTexture = new Texture2D(graphicsDevice, texture.Width, texture.Height, false, SurfaceFormat.Color);
            outTexture.SetData(pixels);
            return outTexture;
        }

        /// <summary>
        /// Make a highlight variant of the color (making it more white).
        /// </summary>
        /// <returns>Scaled color</returns>
        private static Color HighlightColor(Color color, byte offset)
        {
            Color result = new Color
            {
                A = color.A
            };
            byte effectiveOffset = (byte)((color.A > 128) ? offset : 0);
            result.B = (byte)Math.Min(color.B + effectiveOffset, 255);
            result.R = (byte)Math.Min(color.R + effectiveOffset, 255);
            result.G = (byte)Math.Min(color.G + effectiveOffset, 255);
            return result;
        }

        #endregion

    }
}
