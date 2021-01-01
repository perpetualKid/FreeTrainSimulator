using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.View.Track.Shapes;

namespace Orts.View.Track
{
    public class ContentArea
    {
        private Rectangle bounds;
        private double maxScale;

        private TrackContent trackContent;

        public double Scale { get; private set; }

        public Vector2 Offset { get; private set; }

        public Point WindowSize { get; private set; }

        public Point WindowOffset { get; private set; }

        public SpriteBatch SpriteBatch { get; internal set; }

        public ContentArea(TrackContent trackContent)
        {
            this.trackContent = trackContent ?? throw new ArgumentNullException(nameof(trackContent));
            bounds = trackContent.Bounds;
        }

        public void UpdateSize(in Point windowSize, in Point offset)
        {
            WindowSize = windowSize;
            WindowOffset = offset;
            ScaleToFit();
            CenterView();
        }

        private void CenterView()
        {
            Offset = new Vector2(
                (float)((bounds.Left + bounds.Right) / 2 - WindowSize.X / 2 / Scale),
                (float)((bounds.Top + bounds.Bottom) / 2 - (WindowSize.Y - WindowOffset.X) / 2 / Scale));

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
            return world * (float)Scale + Offset * (float)Scale;
        }

        private Vector2 Translate(float x, float y)
        {
            return new Vector2(x + Offset.X, y + Offset.Y) * (float)Scale;
        }

        private Vector2 Translate(in WorldLocation worldLocation)
        {
            double x = worldLocation.TileX * WorldLocation.TileSize + worldLocation.Location.X;
            double y = worldLocation.TileZ * WorldLocation.TileSize + worldLocation.Location.Z;
            return new Vector2((float)(Scale * (x - Offset.X)),
                               (float)(WindowSize.Y - WindowOffset.Y - Scale * (y - Offset.Y)));
        }

        private void DrawTracks()
        {
            foreach (TrackNode trackNode in trackContent.TrackDB.TrackNodes)
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
