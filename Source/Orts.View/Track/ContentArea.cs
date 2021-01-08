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
    public class ContentArea: DrawableGameComponent
    {
        private Rectangle bounds;
        private double maxScale;

        public TrackContent TrackContent { get; }

        public double Scale { get; private set; }

        private double offsetX, offsetY;

        public Point WindowSize { get; private set; }

        public Point WindowOffset { get; private set; }

        public SpriteBatch SpriteBatch { get; set; }

        public ContentArea(Game game, TrackContent trackContent):
            base(game)
        {
            TrackContent = trackContent ?? throw new ArgumentNullException(nameof(trackContent));
            bounds = trackContent.Bounds;
            Game.Components.OfType<ScaleRulerComponent>().FirstOrDefault().Enable(this);
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

        public override void Draw(GameTime gameTime)
        {
            SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            DrawTracks();
            SpriteBatch.End();
            base.Draw(gameTime);
        }

        private Vector2 Translate(in Vector2 world)
        {
            return Translate(world.X, world.Y);
        }

        private Vector2 Translate(float x, float y)
        {
            return new Vector2((float)(x + offsetX * Scale), (float)(y + offsetY * Scale));
        }

        private Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            double x = worldLocation.TileX * WorldLocation.TileSize + worldLocation.Location.X;
            double y = worldLocation.TileZ * WorldLocation.TileSize + worldLocation.Location.Z;
            return new Vector2((float)(Scale * (x - offsetX)),
                               (float)(WindowSize.Y - WindowOffset.Y - Scale * (y - offsetY)));
        }

        private float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * Scale), minScreenSize);
        }

        private void DrawTracks()
        {
            foreach (TrackNode trackNode in TrackContent.TrackDB.TrackNodes)
            {
                switch (trackNode)
                {
                    case TrackVectorNode trackVectorNode:
                        foreach (TrackVectorSection trackVectorSection in trackVectorNode.TrackVectorSections)
                        {
                            TrackSection trackSection = TrackContent.TrackSectionsFile.TrackSections.Get(trackVectorSection.SectionIndex);
                            ref readonly WorldLocation start = ref trackVectorSection.Location;

                            if (trackSection.Curved)
                            {
                                BasicShapes.DrawArc(WorldToScreenSize(trackSection.Width), Color.Black, WorldToScreenCoordinates(start), WorldToScreenSize(trackSection.Radius), trackVectorSection.Direction.Y - MathHelper.PiOver2, trackSection.Angle, 0);
                            }
                            else
                            {
                                BasicShapes.DrawLine(WorldToScreenSize(trackSection.Width), Color.Black, WorldToScreenCoordinates(start), WorldToScreenSize(trackSection.Length), trackVectorSection.Direction.Y - MathHelper.PiOver2);
                            }
                        }
                        break;
                    case TrackEndNode trackEndNode:
                        float angle;
                        int connectedVectorNodeIndex = trackEndNode.TrackPins[0].Link;
                        if (!(TrackContent.TrackDB.TrackNodes[connectedVectorNodeIndex] is TrackVectorNode connectedVectorNode)) continue;

                        if (connectedVectorNode.TrackPins[0].Link == trackNode.Index)
                        {
                            //find angle at beginning of vector node
                            TrackVectorSection tvs = connectedVectorNode.TrackVectorSections[0];
                            angle = tvs.Direction.Y;
                        }
                        else
                        {
                            //find angle at end of vector node
                            TrackVectorSection tvs = connectedVectorNode.TrackVectorSections.Last();
                            angle = tvs.Direction.Y;
                            // try to get even better in case the last section is curved
                                TrackSection section = TrackContent.TrackSectionsFile.TrackSections.Get(tvs.SectionIndex);
                                if (section.Curved)
                                {
                                    angle += MathHelper.ToRadians(section.Angle);
                                }
                        }
                        BasicShapes.DrawLine(WorldToScreenSize(3f), Color.DarkOliveGreen, WorldToScreenCoordinates(trackEndNode.UiD.Location), WorldToScreenSize(2f), angle - MathHelper.PiOver2);
                        break;
                    case TrackJunctionNode trackJunctionNode:
                        BasicShapes.DrawTexture(BasicTextureType.Ring, WorldToScreenCoordinates(trackJunctionNode.UiD.Location), 0, WorldToScreenSize(3, 2), Color.GreenYellow, false, false, true);
                        break;

                }
            }
        }
    }
}
