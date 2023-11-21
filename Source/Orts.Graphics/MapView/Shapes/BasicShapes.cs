using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Graphics.Xna;

namespace Orts.Graphics.MapView.Shapes
{
    internal class BasicShapes
    {
        private readonly EnumArray<Texture2D, BasicTextureType> basicTextures = new EnumArray<Texture2D, BasicTextureType>();
        private readonly EnumArray<Texture2D, BasicTextureType> basicHighlightTextures = new EnumArray<Texture2D, BasicTextureType>();
        private readonly EnumArray<Vector2, BasicTextureType> textureOffsets = new EnumArray<Vector2, BasicTextureType>();

        private const double minAngleDegree = 0.1; // we do not care for angles smaller than 0.1 degrees
        private const double maxAngleDegree = 90; // allows for drawing up to 90 degree arcs
        private const double minAngle = 0.1 * Math.PI / 180.0; // don't care for angles smaller than 0.1 degrees
        private static readonly double[] cosTable = new double[(int)(Math.Ceiling(maxAngleDegree / minAngleDegree) + 1)]; // table with precalculated Cosine values: cosTable[numberDrawn] = cos(numberDrawn * 0.1degrees)
        private static readonly double[] sinTable = new double[(int)(Math.Ceiling(maxAngleDegree / minAngleDegree) + 1)]; // similar

        static BasicShapes()
        {
            PrepareArcDrawing();
        }

        private BasicShapes()
        {
        }

        public static BasicShapes Instance(Game game)
        {
            ArgumentNullException.ThrowIfNull(game);

            BasicShapes instance;
            if ((instance = game.Components.OfType<BasicShapes>().FirstOrDefault()) == null)
            {
                instance = new BasicShapes();
                instance.LoadContent(game.GraphicsDevice);
            }
            return instance;
        }

        /// <summary>
        /// Some initialization needed for actual drawing
        /// </summary>
        /// <param name="graphicsDevice">The graphics device used</param>
        /// <param name="spriteBatch">The spritebatch to use for drawing</param>
        private void LoadContent(GraphicsDevice graphicsDevice)
        {
            basicTextures[BasicTextureType.BlankPixel] = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
            basicTextures[BasicTextureType.BlankPixel].SetData(new[] { Color.White });

            // textures modified from http://www.iconsdb.com
            LoadTexturesFromResources(graphicsDevice);
            //correct center point offsets for non-centered images
            textureOffsets[BasicTextureType.Signal] = new Vector2(-16, 128);
            textureOffsets[BasicTextureType.SignalGreen] = new Vector2(-16, 128);
            textureOffsets[BasicTextureType.SignalRed] = new Vector2(-16, 128);
            textureOffsets[BasicTextureType.SignalYellow] = new Vector2(-16, 128);
            textureOffsets[BasicTextureType.SignalSmall] = new Vector2(8, 64);
            textureOffsets[BasicTextureType.SignalSmallGreen] = new Vector2(0, 48);
            textureOffsets[BasicTextureType.SignalSmallRed] = new Vector2(0, 48);
            textureOffsets[BasicTextureType.SignalSmallYellow] = new Vector2(0, 48);
            textureOffsets[BasicTextureType.Sound] = new Vector2(5, 5);
            textureOffsets[BasicTextureType.Platform] = new Vector2(31, 37);
            textureOffsets[BasicTextureType.Hazard] = Vector2.Zero;
            textureOffsets[BasicTextureType.Pickup] = Vector2.Zero;
            textureOffsets[BasicTextureType.CarSpawner] = Vector2.Zero;
        }   

        #region Drawing
        /// <summary>
        /// Draw one of the (predefined) textures at the given location with the given angle
        /// </summary>
        /// <param name="point">Location where to draw</param>
        /// <param name="texture">name by which the texture is internally known</param>
        /// <param name="angle">Angle used to rotate the texture</param>
        /// <param name="size">Size of the texture in pixels</param>
        /// <param name="color">Color mask for the texture to draw (white will not affect the texture)</param>
        /// <param name="flip">Whether the texture needs to be flipped (vertically)</param>
        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight, SpriteBatch spriteBatch)
        {
            Vector2 scaledSize = size < 0 ? new Vector2(-size) : new Vector2(size / this.basicTextures[texture].Width);

            SpriteEffects flipMode = (flipHorizontal ? SpriteEffects.FlipHorizontally : SpriteEffects.None) | (flipVertical ? SpriteEffects.FlipVertically : SpriteEffects.None);
            spriteBatch.Draw(highlight ? this.basicHighlightTextures[texture] : this.basicTextures[texture], point, null, Color.White, (float)angle, this.textureOffsets[texture], scaledSize, flipMode, 0);
        }

        /// <summary>
        /// Draw one of the (predefined) textures at the given location with the given angle
        /// </summary>
        /// <param name="point">Location where to draw</param>
        /// <param name="texture">name by which the texture is internally known</param>
        /// <param name="angle">Angle used to rotate the texture</param>
        /// <param name="size">Size of the texture in pixels</param>
        /// <param name="color">Color mask for the texture to draw (white will not affect the texture)</param>
        /// <param name="flip">Whether the texture needs to be flipped (vertically)</param>
        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, SpriteBatch spriteBatch)
        {
            Vector2 scaledSize = size < 0 ? new Vector2(-size) : new Vector2(size / this.basicTextures[texture].Width);

            spriteBatch.Draw(this.basicTextures[texture], point, null, color, (float)angle, this.textureOffsets[texture], scaledSize, SpriteEffects.None, 0);
        }

        /// <summary>
        /// Draw one of the (predefined) textures at the given location with the given angle
        /// </summary>
        /// <param name="texture">name by which the texture is internally known<br/></param>
        /// <param name="targetRectangle">area where the texture is drawn<br/></param>
        /// <param name="color">Color mask for the texture to draw (white will not affect the texture)<br/></param>
        public void DrawTexture(BasicTextureType texture, Rectangle targetRectangle, Color color, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(this.basicTextures[texture], targetRectangle, color);
        }

        /// <summary>
        /// Basic method to draw a line. Coordinates are in screen coordinates.
        /// </summary>
        /// <param name="width">Width of the line to draw<br/></param>
        /// <param name="color">Color of the line<br/></param>
        /// <param name="point">Vector to the first point of the line<br/></param>
        /// <param name="length">Length of the line<br/></param>
        /// <param name="angle">Angle (in down from horizontal) of where the line is pointing<br/></param>
        public void DrawLine(float width, Color color, Vector2 point, float length, double angle, SpriteBatch spriteBatch)
        {
            // offset to compensate for the width of the line
            if (width >= 2)
            {
                Vector2 offset = new Vector2((float)(width * Math.Sin(angle) / 2.0), (float)(-width * Math.Cos(angle) / 2));
                spriteBatch.Draw(this.basicTextures[BasicTextureType.BlankPixel], point + offset, null, color, (float)angle, Vector2.Zero, new Vector2(length, width), SpriteEffects.None, 0);
            }
            else
                spriteBatch.Draw(this.basicTextures[BasicTextureType.BlankPixel], point, null, color, (float)angle, Vector2.Zero, new Vector2(length, width), SpriteEffects.None, 0);
        }

        /// <summary>
        /// Basic method to draw a line. Coordinates are in screen coordinates.
        /// <param name="width"> Width of the line to draw </param>
        /// <param name="color"> Color of the line</param>
        /// <param name="point1"> Vector to the first point of the line</param>
        /// <param name="point2"> Vector to the last point of the line</param>
        /// </summary>
        public void DrawLine(float width, Color color, Vector2 point1, Vector2 point2, SpriteBatch spriteBatch)
        {
            double angle = Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
            float length = Vector2.Distance(point1, point2);
            DrawLine(width, color, point1, length, angle, spriteBatch);
        }

        /// <summary>
        /// Basic method to draw a dashed line. Coordinates are in screen coordinates.
        /// <param name="width"> Width of the line to draw </param>
        /// <param name="color"> Color of the line</param>
        /// <param name="point1"> Vector to the first point of the line</param>
        /// <param name="point2"> Vector to the last point of the line</param>
        /// </summary>
        public void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2, SpriteBatch spriteBatch)
        {
            double angle = Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
            float cosAngle = (float)Math.Cos(angle);
            float sinAngle = (float)Math.Sin(angle);

            float length = Vector2.Distance(point1, point2);

            int pixelsPerSegment = 10; // this is a target value. We will always start and end with a segment.
            int segments = 1 + (int)(length / (2 * pixelsPerSegment));
            float lengthPerSegment = length / (2 * segments - 1);
            Vector2 segmentOffset = 2 * lengthPerSegment * new Vector2(cosAngle, sinAngle);

            for (int i = 0; i < segments; i++)
            {
                Vector2 segmentStartPoint = point1 + i * segmentOffset;
                DrawLine(width, color, segmentStartPoint, lengthPerSegment, angle, spriteBatch);
            }
        }

        /// <summary>
        /// Basic (but not trivial) method to draw an arc. Coordinates are in screen coordinates.
        /// </summary>
        /// <param name="width">Width of the line to draw </param>
        /// <param name="color">Color of the line</param>
        /// <param name="point">Vector to the first point of the line</param>
        /// <param name="radius">Radius of the circle to which the arc belongs. Positive means curving left</param>
        /// <param name="angle">Angle (in down from horizontal) of where the line is pointing</param>
        /// <param name="arcSize">Arc size in Radian (2Pi would be full circle)</param>
        public void DrawArc(float width, Color color, Vector2 point, double radius, double angle, double arcSize, SpriteBatch spriteBatch)
        {
            // Positive arcDegree means curving to the left, negative arcDegree means curving to the right
            int sign = -Math.Sign(arcSize);
            arcSize = Math.Abs(arcSize);

            // We will draw an arc as a succession of straight lines. We do this in a way that reduces the amount
            // of goniometric calculations needed.
            // The idea is to start to find the center of the circle. The direction from center to origin is
            // 90 degrees different from angle
            Vector2 centerToPointDirection = sign * new Vector2(-(float)Math.Sin(angle), (float)Math.Cos(angle)); // unit vector
            Vector2 center = point - (float)radius * centerToPointDirection;

            // To determine the amount of straight lines we need to calculate we first 
            // determine then lenght of the arc, and divide that by the maximum we allow;
            // All angles go in steps of minAngleDegree
            double arcLength = radius * arcSize;
            // We draw straight lines. The error in the middle of the line is: error = radius - radius*cos(alpha/2).
            // Here alpha is the angle drawn for a single arc-segment. Approximately error ~ radius * alpha^2/8.
            // The amount of pixels in the line is about L ~ radius * alpha => L ~ sqrt(8 * radius * error). 
            // We found that for thight curves, error can not be larger than half a pixel (otherwise it becomes visible)
            double maxStraightPixels = Math.Sqrt(4 * radius);
            double numberStraightLines = Math.Ceiling(arcLength / maxStraightPixels);
            // amount of minAngleDegrees we need to cover: 
            int arcStepsRemaining = (int)Math.Round(arcSize / minAngle);
            // amount of minAngleDegrees we cover per straight line:
            int arcStepsPerLine = (int)Math.Ceiling(arcSize / (minAngle * numberStraightLines));

            // All straight lines that we draw will be titled by half of the arc that is should cover.
            angle += -sign * arcStepsPerLine * minAngle / 2;

            // while we still have some arc steps to cover
            while (arcStepsRemaining > 0)
            {
                int arcSteps = Math.Min(arcStepsRemaining, arcStepsPerLine); //angle steps we cover in this line
                point = center + centerToPointDirection * (float)(radius - sign * width / 2.0);  // correct for width of line
                double length = radius * arcSteps * minAngle + 1; // the +1 to prevent white lines in between arc sections

                spriteBatch.Draw(this.basicTextures[BasicTextureType.BlankPixel], point, null, color, (float)angle, Vector2.Zero, new Vector2((float)length, width), SpriteEffects.None, 0);

                // prepare for next straight line
                arcStepsRemaining -= arcSteps;

                if (arcStepsRemaining > 0)
                {
                    angle -= sign * arcSteps * minAngle;
                    //Rotate the centerToPointDirection, and calculate new point
                    centerToPointDirection = new Vector2(
                             (float)(cosTable[arcSteps] * centerToPointDirection.X + sign * sinTable[arcSteps] * centerToPointDirection.Y),
                       (float)(-sign * sinTable[arcSteps] * centerToPointDirection.X + cosTable[arcSteps] * centerToPointDirection.Y)
                        );
                }
            }
        }

        #endregion

        #region private implementations
        private void LoadTexturesFromResources(GraphicsDevice graphicsDevice)
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
                    basicTextures[textureValue] = PrepareColorScaledTexture(graphicsDevice, texture);
                    basicHighlightTextures[textureValue] = PrepareColorScaledTexture(graphicsDevice, texture, 0.8);
                    textureOffsets[textureValue] = new Vector2(texture.Width / 2, texture.Height / 2);
                }
            }
            );
        }

        private static Texture2D PrepareColorScaledTexture(GraphicsDevice graphicsDevice, Texture2D texture, double range = 1)
        {
            int pixelCount = texture.Width * texture.Height;
            Color[] pixels = new Color[pixelCount];
            texture.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = pixels[i].HighlightColor(range);
            }

            Texture2D outTexture = new Texture2D(graphicsDevice, texture.Width, texture.Height, false, SurfaceFormat.Color);
            outTexture.SetData(pixels);
            return outTexture;
        }

        /// <summary>
        /// Some preparation to be able to draw arcs more efficiently
        /// </summary>
        private static void PrepareArcDrawing()
        {
            for (int i = 0; i < cosTable.Length; i++)
            {
                cosTable[i] = Math.Cos(i * minAngle);
                sinTable[i] = Math.Sin(i * minAngle);
            }
        }
        #endregion

    }
}
