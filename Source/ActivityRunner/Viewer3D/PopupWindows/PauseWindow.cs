using System;

using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class PauseOverlay : OverlayBase
    {

        private const double AnimationLength = 0.6;
        private const double AnimationFade = 0.1;
        private const double AnimationSize = 0.2;

        private const int textureSize = 256;

        private readonly Texture2D pauseTexture;
        private readonly Viewer viewer;
        private bool animationRunning;
        private double animationStart = -1;
        private Rectangle animationSourceRectangle;
        private Rectangle animationTargetRectangle;
        private float fade = 1.0f;

        public PauseOverlay(WindowManager owner, Viewer viewer) :
            base(owner, CatalogManager.Catalog)
        {
            this.viewer = viewer;
            ZOrder = 80;
            Modal = true;

            Color backgroundColor = Color.Black * 0.5f;
            const int borderRadius = textureSize / 7;
            Color[] data = new Color[textureSize * textureSize * 2];

            // Rounded corner background.
            for (int y = 0; y < textureSize; y++)
                for (int x = 0; x < textureSize; x++)
                    if ((x > borderRadius && x < textureSize - borderRadius) || (y > borderRadius && y < textureSize - borderRadius)
                        || (Math.Sqrt((x - borderRadius) * (x - borderRadius) + (y - borderRadius) * (y - borderRadius)) < borderRadius)
                        || (Math.Sqrt((x - textureSize + borderRadius) * (x - textureSize + borderRadius) + (y - borderRadius) * (y - borderRadius)) < borderRadius)
                        || (Math.Sqrt((x - borderRadius) * (x - borderRadius) + (y - textureSize + borderRadius) * (y - textureSize + borderRadius)) < borderRadius)
                        || (Math.Sqrt((x - textureSize + borderRadius) * (x - textureSize + borderRadius) + (y - textureSize + borderRadius) * (y - textureSize + borderRadius)) < borderRadius))
                        data[y * textureSize + x] = backgroundColor;

            // Clone the background for pause texture (it has two states).
            Array.Copy(data, 0, data, textureSize * textureSize, textureSize * textureSize);

            // Play ">" symbol.
            for (int y = textureSize / 7; y < textureSize - textureSize / 7; y++)
            {
                for (int x = textureSize / 7; x < textureSize - textureSize / 7 - 2 * Math.Abs(y - textureSize / 2); x++)
                    data[y * textureSize + x] = Color.White;
            }

            // Pause "||" symbol.
            for (int y = textureSize + textureSize / 7; y < 2 * textureSize - textureSize / 7; y++)
            {
                for (int x = textureSize * 2 / 7; x < textureSize * 3 / 7; x++)
                    data[y * textureSize + x] = Color.White;
                for (int x = textureSize * 4 / 7; x < textureSize * 5 / 7; x++)
                    data[y * textureSize + x] = Color.White;
            }

            pauseTexture = new Texture2D(owner.GraphicsDevice, textureSize, textureSize * 2, false, SurfaceFormat.Color);
            pauseTexture.SetData(data);
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
        {
            return base.Layout(layout, headerScaling);
        }

        public override bool Open()
        {
            animationRunning = true;
            animationStart = viewer.RealTime;
            animationSourceRectangle = new Rectangle(0, Simulator.Instance.GamePaused ? textureSize : 0, textureSize, textureSize);
            return base.Open();
        }

        public override bool Close()
        {
            return base.Close();
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (!animationRunning)
                _ = Close();
            else
            {
                double currentLength = viewer.RealTime - animationStart;
                if (currentLength > AnimationLength)
                {
                    currentLength = AnimationLength;
                    animationRunning = false;
                }

                int currentSize = (int)(AnimationSize * Borders.Height * (1 - 0.5 / (1 + currentLength)));

                if (currentLength < AnimationFade)
                    fade = (float)(currentLength / AnimationFade);
                else if (currentLength > AnimationLength - AnimationFade)
                    fade = (float)((currentLength - AnimationLength) / -AnimationFade);

                animationTargetRectangle = new Rectangle(Borders.Width / 2, Borders.Height / 2, 0, 0);
                animationTargetRectangle.Inflate(currentSize, currentSize);

                base.Update(gameTime, shouldUpdate);
            }
        }

        protected override void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(pauseTexture, animationTargetRectangle, animationSourceRectangle, Color.White * fade);
            base.Draw(spriteBatch);
        }

        protected override void Dispose(bool disposing)
        {
            pauseTexture?.Dispose();
            base.Dispose(disposing);
        }
    }
}
