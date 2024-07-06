using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public class Track3DOverlay : WindowControl
    {
        private const int Width = 2;

        private const int MaximumDistance = 1000;

        private readonly List<(WorldLocation Start, WorldLocation End, Color color)> trackSegments = new List<(WorldLocation, WorldLocation, Color)>();
        private List<(Vector2, float, float, Color)> drawSegments = new List<(Vector2, float, float, Color)>();
        private List<(Vector2, float, float, Color)> prepareSegments = new List<(Vector2, float, float, Color)>();

        public IViewProjection CameraView { get; set; }

        public int ViewDistance { get; set; } = MaximumDistance;

        public Track3DOverlay(FormBase window) : base(window, 0, 0, 0, 0)
        {
        }

        public void Clear()
        {
            trackSegments.Clear();
        }

        public void Add(in WorldLocation start, in WorldLocation end, Color color)
        {
            trackSegments.Add((start, end, color));
        }

        internal override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            prepareSegments.Clear();

            ref readonly Viewport viewport = ref Window.Owner.Viewport;

            foreach ((WorldLocation Start, WorldLocation End, Color Color) in trackSegments)
            {
                Vector3 start2d = Project3D(Normalize(Start, CameraView), viewport, CameraView);
                Vector3 end2d = Project3D(Normalize(End, CameraView), viewport, CameraView);
                Vector3 line2d = end2d - start2d;
                line2d.Normalize();

                float distance = WorldLocation.GetDistance(Start, CameraView.Location).Length();

                if (distance < ViewDistance && start2d.Z >= 0 && start2d.Z <= 1 && end2d.Z >= 0 && end2d.Z <= 1)
                {
                    Vector2 start2D = Flatten(start2d) + new Vector2(line2d.Y * Width / 2, -line2d.X * Width / 2);
                    float angle = (float)Math.Atan2(end2d.Y - start2d.Y, end2d.X - start2d.X);
                    float length = (end2d - start2d).Length();

                    prepareSegments.Add((start2D, angle, length, Color));
                }
            }
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            (drawSegments, prepareSegments) = (prepareSegments, drawSegments);
            foreach ((Vector2 start, float angle, float length, Color color) in drawSegments)
            {
                spriteBatch.Draw(Window.Owner.WhiteTexture, start, null, color, angle, Vector2.Zero, new Vector2(length, Width), SpriteEffects.None, 0);
            }
            base.Draw(spriteBatch, offset);
        }

        private static Vector3 Normalize(in WorldLocation location, IViewProjection cameraView)
        {
            return new Vector3(location.Location.X + (location.TileX - cameraView.Location.TileX) * 2048, location.Location.Y, -location.Location.Z - (location.TileZ - cameraView.Location.TileZ) * 2048);
        }

        private static Vector3 Project3D(Vector3 position, in Viewport viewport, IViewProjection cameraView)
        {
            return viewport.Project(position, cameraView.Projection, cameraView.View, Matrix.Identity);
        }

        protected static Vector2 Flatten(Vector3 position)
        {
            return new Vector2(position.X, position.Y);
        }

    }
}
