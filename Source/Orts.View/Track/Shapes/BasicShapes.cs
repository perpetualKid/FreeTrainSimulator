using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.View.Xna;

namespace Orts.View.Track.Shapes
{
    public static class BasicShapes
    {
        public static EnumArray<Texture2D, BasicTextureType> BasicTextures { get; } = new EnumArray<Texture2D, BasicTextureType>();
        public static EnumArray<Texture2D, BasicTextureType> BasicHighlightTextures { get; } = new EnumArray<Texture2D, BasicTextureType>();

        private static EnumArray<Vector2, BasicTextureType> textureOffsets = new EnumArray<Vector2, BasicTextureType>();

        private static GraphicsDevice graphicsDevice;
        private static SpriteBatch spriteBatch;

        private const double minAngleDegree = 0.1f; // we do not care for angles smaller than 0.1 degrees
        private const double maxAngleDegree = 90; // allows for drawing up to 90 degree arcs
        private const double minAngleRad = minAngleDegree * Math.PI / 180.0; // minAngleDegree but in radians.
        private static readonly double[] cosTable = new double[(int)(Math.Ceiling(maxAngleDegree / minAngleDegree) + 1)]; // table with precalculated Cosine values: cosTable[numberDrawn] = cos(numberDrawn * 0.1degrees)
        private static readonly double[] sinTable = new double[(int)(Math.Ceiling(maxAngleDegree / minAngleDegree) + 1)]; // similar

        /// <summary>
        /// Some initialization needed for actual drawing
        /// </summary>
        /// <param name="graphicsDevice">The graphics device used</param>
        /// <param name="spriteBatch">The spritebatch to use for drawing</param>
        public static void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            const int diameter = 64; // Needs to be power of two for mipmapping
            BasicShapes.graphicsDevice = graphicsDevice;
            BasicShapes.spriteBatch = spriteBatch;
            BasicTextures[BasicTextureType.BlankPixel] = new Texture2D(graphicsDevice, 1, 1);
            BasicTextures[BasicTextureType.BlankPixel].SetData(new[] { Color.White });

            BasicTextures[BasicTextureType.Circle] = CreateCircleTexture(graphicsDevice, diameter);
            textureOffsets[BasicTextureType.Circle] = new Vector2(diameter / 2, diameter / 2);

            BasicTextures[BasicTextureType.Disc] = CreateDiscTexture(graphicsDevice, diameter);
            textureOffsets[BasicTextureType.Disc] = new Vector2(diameter / 2, diameter / 2);

            BasicTextures[BasicTextureType.Ring] = CreateRingTexture(graphicsDevice, diameter);
            textureOffsets[BasicTextureType.Ring] = new Vector2(diameter / 2, diameter / 2);

            BasicTextures[BasicTextureType.CrossedRing] = CreateCrossedRingTexture(graphicsDevice, diameter);
            textureOffsets[BasicTextureType.CrossedRing] = new Vector2(diameter / 2, diameter / 2);

            // textures modified from http://www.iconsdb.com
            LoadTexturesFromResources();
            //correct center point offsets for non-centered images
            textureOffsets[BasicTextureType.Signal] = new Vector2(29, 18);
            textureOffsets[BasicTextureType.Sound] = new Vector2(5, 5);
            textureOffsets[BasicTextureType.Platform] = new Vector2(31, 37);
            textureOffsets[BasicTextureType.Hazard] = Vector2.Zero;
            textureOffsets[BasicTextureType.Pickup] = Vector2.Zero;
            textureOffsets[BasicTextureType.CarSpawner] = Vector2.Zero;

            PrepareArcDrawing();

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
        public static void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, bool flipHorizontal, bool flipVertical, bool highlight)
        {
            SpriteEffects flipMode = (flipHorizontal ? SpriteEffects.FlipHorizontally : SpriteEffects.None) | (flipVertical ? SpriteEffects.FlipVertically : SpriteEffects.None);
            float scaledSize = 1 / size;/// BasicTextures[texture].Width;
                spriteBatch.Draw(highlight ? BasicHighlightTextures[texture] : BasicTextures[texture], point, null, color, (float)angle, textureOffsets[texture], new Vector2(scaledSize), flipMode, 0);
        }

        /// <summary>
        /// Basic method to draw a line. Coordinates are in screen coordinates.
        /// </summary>
        /// <param name="width">Width of the line to draw </param>
        /// <param name="color">Color of the line</param>
        /// <param name="point">Vector to the first point of the line</param>
        /// <param name="length">Length of the line</param>
        /// <param name="angle">Angle (in down from horizontal) of where the line is pointing</param>
        public static void DrawLine(float width, Color color, Vector2 point, float length, double angle)
        {
            // offset to compensate for the width of the line
            Vector2 offset = new Vector2((float)(width * Math.Sin(angle) / 2.0), (float)(-width * Math.Cos(angle) / 2));
            spriteBatch.Draw(BasicTextures[BasicTextureType.BlankPixel], point + offset, null, color, (float)angle, Vector2.Zero, new Vector2(length, width), SpriteEffects.None, 0);
        }

        /// <summary>
        /// Basic method to draw a line. Coordinates are in screen coordinates.
        /// <param name="width"> Width of the line to draw </param>
        /// <param name="color"> Color of the line</param>
        /// <param name="point1"> Vector to the first point of the line</param>
        /// <param name="point2"> Vector to the last point of the line</param>
        /// </summary>
        public static void DrawLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            double angle = Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
            float length = Vector2.Distance(point1, point2);
            DrawLine(width, color, point1, length, angle);
        }

        /// <summary>
        /// Basic method to draw a dashed line. Coordinates are in screen coordinates.
        /// <param name="width"> Width of the line to draw </param>
        /// <param name="color"> Color of the line</param>
        /// <param name="point1"> Vector to the first point of the line</param>
        /// <param name="point2"> Vector to the last point of the line</param>
        /// </summary>
        public static void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2)
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
                DrawLine(width, color, segmentStartPoint, lengthPerSegment, angle);
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
        /// <param name="arcDegrees">Number of degrees in the arc (360 would be full circle)</param>
        /// <param name="arcDegreesOffset">Instead of starting at 0 degrees in the circle, offset allows to start at a different position</param>
        public static void DrawArc(float width, Color color, Vector2 point, double radius, double angle, double arcDegrees, double arcDegreesOffset)
        {
            // Positive arcDegree means curving to the left, negative arcDegree means curving to the right
            int sign = Math.Sign(arcDegrees);
            arcDegrees = Math.Abs(arcDegrees);

            // We will draw an arc as a succession of straight lines. We do this in a way that reduces the amount
            // of goniometric calculations needed.
            // The idea is to start to find the center of the circle. The direction from center to origin is
            // 90 degrees different from angle
            Vector2 centerToPointDirection = sign * new Vector2(-(float)Math.Sin(angle), (float)Math.Cos(angle)); // unit vector
            Vector2 center = point - (float)radius * centerToPointDirection;

            // To determine the amount of straight lines we need to calculate we first 
            // determine then lenght of the arc, and divide that by the maximum we allow;
            // All angles go in steps of minAngleDegree
            double arcLength = radius * arcDegrees * Math.PI / 180.0;
            // We draw straight lines. The error in the middle of the line is: error = radius - radius*cos(alpha/2).
            // Here alpha is the angle drawn for a single arc-segment. Approximately error ~ radius * alpha^2/8.
            // The amount of pixels in the line is about L ~ radius * alpha => L ~ sqrt(8 * radius * error). 
            // We found that for thight curves, error can not be larger than half a pixel (otherwise it becomes visible)
            double maxStraightPixels = Math.Sqrt(4 * radius);
            double numberStraightLines = Math.Ceiling(arcLength / maxStraightPixels);
            // amount of minAngleDegrees we need to cover: 
            int arcStepsRemaining = (int)(Math.Round(arcDegrees / minAngleDegree));
            // amount of minAngleDegrees we cover per straight line:
            int arcStepsPerLine = (int)Math.Ceiling(arcDegrees / (minAngleDegree * numberStraightLines));

            // Add offset in angles
            if (arcDegreesOffset != 0f)
            {
                angle += arcDegreesOffset * Math.PI / 180.0;
                centerToPointDirection = sign * new Vector2(-(float)Math.Sin(angle), (float)Math.Cos(angle));
            }

            // All straight lines that we draw will be titled by half of the arc that is should cover.
            angle += -sign * arcStepsPerLine * minAngleRad / 2;

            // while we still have some arc steps to cover
            while (arcStepsRemaining > 0)
            {
                int arcSteps = Math.Min(arcStepsRemaining, arcStepsPerLine); //angle steps we cover in this line
                point = center + centerToPointDirection * (float)(radius - sign * width / 2.0);  // correct for width of line
                double length = radius * arcSteps * minAngleRad + 1; // the +1 to prevent white lines in between arc sections

                spriteBatch.Draw(BasicTextures[BasicTextureType.BlankPixel], point, null, color, (float)angle, Vector2.Zero, new Vector2((float)length, width), SpriteEffects.None, 0);

                // prepare for next straight line
                arcStepsRemaining -= arcSteps;

                if (arcStepsRemaining > 0)
                {
                    angle -= sign * arcSteps * minAngleRad;
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
                    BasicTextures[textureValue] = PrepareColorScaledTexture(texture);
                    BasicHighlightTextures[textureValue] = PrepareColorScaledTexture(texture, 0.8);
                    textureOffsets[textureValue] = new Vector2(texture.Width / 2 - 1, texture.Height / 2 - 1);
                }
            }
            );
        }

        private static Texture2D PrepareColorScaledTexture(Texture2D texture, double range = 1)
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
        /// private method to create a texture2D containing a circle
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="outerRadius">Outer radius (diameter) of the circle in pixels</param>
        /// <returns>The white texture</returns>
        private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, int outerRadius)
        {
            int radius = (outerRadius - 1) / 2;
            int innerRadius = radius - 1;
            Texture2D texture = new Texture2D(graphicsDevice, outerRadius, outerRadius, false, SurfaceFormat.Color);

            Color[] data = new Color[outerRadius * outerRadius];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int i = (x + radius) * outerRadius + (y + radius);
                    int r2 = x * x + y * y;
                    data[i] = (r2 <= radius * radius && r2 > innerRadius * innerRadius) ? Color.White : Color.Transparent;
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// private method to create a texture2D containing a disc (filled circle)
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="outerRadius">Outer radius (diameter) of the circle in pixels</param>
        /// <returns>The white texture</returns>
        private static Texture2D CreateDiscTexture(GraphicsDevice graphicsDevice, int outerRadius)
        {
            int radius = (outerRadius - 1) / 2;
            Texture2D texture = new Texture2D(graphicsDevice, outerRadius, outerRadius, false, SurfaceFormat.Color);

            Color[] data = new Color[outerRadius * outerRadius];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int i = (x + radius) * outerRadius + (y + radius);
                    data[i] = (x * x + y * y <= radius * radius) ? Color.White : Color.Transparent;
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// private method to create a texture2D containing a ring (circle with thick border)
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="outerRadius">Outer radius (diameter) of the circle in pixels</param>
        /// <returns>The white texture</returns>
        private static Texture2D CreateRingTexture(GraphicsDevice graphicsDevice, int outerRadius)
        {
            int radius = (outerRadius - 1) / 2;
            int innerRadius = (2 * radius) / 3;
            Texture2D texture = new Texture2D(graphicsDevice, outerRadius, outerRadius, false, SurfaceFormat.Color);

            Color[] data = new Color[outerRadius * outerRadius];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int i = (x + radius) * outerRadius + (y + radius);
                    int r2 = x * x + y * y;
                    data[i] = (r2 <= radius * radius && r2 > innerRadius * innerRadius) ? Color.White : Color.Transparent;
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// private method to create a texture2D containing a  (circle with thick border), with a cross in the middle
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="outerRadius">Outer radius (diameter) of the circle in pixels</param>
        /// <returns>The white texture</returns>
        private static Texture2D CreateCrossedRingTexture(GraphicsDevice graphicsDevice, int outerRadius)
        {
            int radius = (outerRadius - 1) / 2;
            int innerRadius = (3 * radius) / 4;
            int crossWidth = 5;
            Texture2D texture = new Texture2D(graphicsDevice, outerRadius, outerRadius, false, SurfaceFormat.Color);

            Color[] data = new Color[outerRadius * outerRadius];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int i = (x + radius) * outerRadius + (y + radius);
                    data[i] = Color.Transparent; //default
                    int r2 = x * x + y * y;
                    if (r2 <= radius * radius)
                    {
                        if (r2 > innerRadius * innerRadius)
                        {   //ring
                            data[i] = Color.White;
                        }
                        if ((x - y) * (x - y) < crossWidth * crossWidth)
                        {   //part of cross lower-left to upper-right
                            data[i] = Color.White;
                        }
                        if ((x + y) * (x + y) < crossWidth * crossWidth)
                        {   //part of cross lower-right to upper-left
                            data[i] = Color.White;
                        }
                    }

                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// Some preparation to be able to draw arcs more efficiently
        /// </summary>
        private static void PrepareArcDrawing()
        {
            for (int i = 0; i < cosTable.Length; i++)
            {
                cosTable[i] = Math.Cos(i * minAngleRad);
                sinTable[i] = Math.Sin(i * minAngleRad);
            }
        }
        #endregion

    }
}
