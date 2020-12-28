using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.View.Track.Shapes;

namespace Orts.TrackEditor
{
    internal class ContentArea
    {
        private Rectangle bounds = new Rectangle(-100, 0, 300, 200);
        private double maxScale;

        public double Scale { get; private set; }

        public Vector2 Offset { get; private set; }


        public Point WindowSize { get; private set; }

        public SpriteBatch SpriteBatch { get; internal set; }
        public void UpdateSize(in Point windowSize)
        {
            WindowSize = windowSize;
            ScaleToFit();
            CenterView();
        }

        private void CenterView()
        {
            Offset = new Vector2((float)((WindowSize.X / Scale - bounds.Size.X) / 2), (float)((WindowSize.Y / Scale - bounds.Size.Y) / 2.0));
        }

        private void ScaleToFit()
        {
            double xScale = (double)WindowSize.X / bounds.Width;
            double yScale = (double)WindowSize.Y / bounds.Height;
            Scale = Math.Min(xScale, yScale);
            maxScale = Scale * 0.75;
        }

        public void DrawVoid()
        {
            BasicShapes.DrawLine(3, Color.Red, Translate(0 + 10, 0 + 10), Translate(bounds.Width - 10, bounds.Height - 10));
            BasicShapes.DrawLine(3, Color.Red, Translate(0 + 10, bounds.Height - 10), Translate(bounds.Width - 10, 0 + 10));
            BasicShapes.DrawTexture(BasicTextureType.Signal, Translate(0 + 15, 0 + 15), MathHelper.ToRadians(-45), 0.5f, Color.White, false, false, false);
        }

        public void DrawLine(Vector2 start, Vector2 end, float width, Color color)
        {
            BasicShapes.DrawLine(width, color, Translate(start), Translate(end));
        }

        private Vector2 Translate(in Vector2 world)
        {
            return world * (float)Scale + Offset * (float)Scale;
        }
        private Vector2 Translate(float x, float y)
        {
            return new Vector2(x + Offset.X, y + Offset.Y) * (float)Scale;
        }
    }
}
