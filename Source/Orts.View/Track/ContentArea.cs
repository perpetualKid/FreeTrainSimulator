using System;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.View.DrawableComponents;
using Orts.View.Track.Shapes;

namespace Orts.View.Track
{
    public class ContentArea
    {
        private Rectangle bounds;
        private double maxScale;
        private Game game;

        public TrackContent TrackContent { get; }

        public double Scale { get; private set; }

        private double offsetX, offsetY;

        public Point WindowSize { get; private set; }

        public Point WindowOffset { get; private set; }

        public SpriteBatch SpriteBatch { get; internal set; }

        public ContentArea(Game game, TrackContent trackContent)
        {
            this.game = game;
            TrackContent = trackContent ?? throw new ArgumentNullException(nameof(trackContent));
            bounds = trackContent.Bounds;
            game.Components.OfType<ScaleRulerComponent>().FirstOrDefault().Enable(this);
        }

        public void ResetSize(in Point windowSize, in Point offset)
        {
            WindowSize = windowSize;
            WindowOffset = offset;
            ScaleToFit();
            CenterView();
        }

        public void UpdateSize(in Point windowSize, in Point offset)
        {
            WindowSize = windowSize;
            WindowOffset = offset;
            ScaleToFit();
        }

        public void UpdateScaleAt(in Vector2 scaleAt, int steps)
        {
            double scale = Scale * Math.Pow((steps > 0 ? 1 / 0.9 : (steps < 0 ? 0.9 : 1)), Math.Abs(steps));
            if (scale < maxScale || scale > 200)
                return;
            offsetX += scaleAt.X * (scale / Scale - 1.0) / scale;
            offsetY += (WindowSize.Y - WindowOffset.Y - scaleAt.Y) * (scale / Scale - 1.0) / scale;
            Scale = scale;
        }

        public void UpdatePosition(in Vector2 delta)
        {
            offsetX -= delta.X / Scale;
            offsetY += delta.Y / Scale;
        }

        private void CenterView()
        {
            offsetX = (bounds.Left + bounds.Right) / 2 - WindowSize.X / 2 / Scale;
            offsetY = (bounds.Top + bounds.Bottom) / 2 - (WindowSize.Y - WindowOffset.X) / 2 / Scale;
        }

        private void ScaleToFit()
        {
            double xScale = (double)WindowSize.X / bounds.Width;
            double yScale = (double)(WindowSize.Y  - WindowOffset.X - WindowOffset.Y) / bounds.Height;
            Scale = Math.Min(xScale, yScale);
            maxScale = Scale * 0.75;
        }

        public void Draw()
        {
            DrawTracks();
        }

        private Vector2 Translate(in Vector2 world)
        {
            return Translate(world.X, world.Y);
        }

        private Vector2 Translate(float x, float y)
        {
            return new Vector2((float)(x + offsetX * Scale), (float)(y + offsetY * Scale));
        }

        private Vector2 Translate(in WorldLocation worldLocation)
        {
            double x = worldLocation.TileX * WorldLocation.TileSize + worldLocation.Location.X;
            double y = worldLocation.TileZ * WorldLocation.TileSize + worldLocation.Location.Z;
            return new Vector2((float)(Scale * (x - offsetX)),
                               (float)(WindowSize.Y - WindowOffset.Y - Scale * (y - offsetY)));
        }

        private void DrawTracks()
        {
            foreach (TrackNode trackNode in TrackContent.TrackDB.TrackNodes)
            {
                switch (trackNode)
                {
                    case TrackVectorNode trackVectorNode:
                        if (trackVectorNode.TrackVectorSections.Length > 1)
                        {
                            for (int i = 0; i < trackVectorNode.TrackVectorSections.Length - 1; i++)
                            {
                                ref readonly WorldLocation start = ref trackVectorNode.TrackVectorSections[i].Location;
                                ref readonly WorldLocation end = ref trackVectorNode.TrackVectorSections[i + 1].Location;
                                BasicShapes.DrawLine(3, Color.Black, Translate(start), Translate(end));
//                                TrackSegments.Add(new TrackSegment(start, end, trackVectorNode.TrackVectorSections[i].SectionIndex));
                            }
                        }
                        //else
                        //{
                        //    TrackVectorSection section = trackVectorNode.TrackVectorSections[0];

                        //    foreach (TrackPin pin in trackVectorNode.TrackPins)
                        //    {
                        //        TrackNode connectedNode = simulator.TDB.TrackDB.TrackNodes[pin.Link];
                        //        TrackSegments.Add(new TrackSegment(section.Location, connectedNode.UiD.Location, null));
                        //        UpdateBounds(section.Location);
                        //        UpdateBounds(connectedNode.UiD.Location);
                        //    }
                        //}

                        break;

                }
            }
        }
    }
}
