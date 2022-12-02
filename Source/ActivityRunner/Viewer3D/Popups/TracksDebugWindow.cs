// COPYRIGHT 2012 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public class TracksDebugWindow : LayeredWindow
    {
        private const float DisplaySegmentLength = 10;
        private const float Tolerance = 0.0001F;
        private Viewport Viewport;
        private List<DispatcherPrimitive> Primitives = new List<DispatcherPrimitive>();

        public TracksDebugWindow(WindowManager owner)
            : base(owner, 1, 1, "Tracks and Roads Debug")
        {
        }

        internal override void ScreenChanged()
        {
            base.ScreenChanged();
            Viewport = Owner.Viewer.Game.GraphicsDevice.Viewport;
        }

        public override void PrepareFrame(in ElapsedTime elapsedTime, bool updateFull)
        {
            base.PrepareFrame(elapsedTime, updateFull);

            if (updateFull)
            {
                var primitives = new List<DispatcherPrimitive>(Primitives.Count);
                var camera = Owner.Viewer.Camera;
                TrackDB tdb = RuntimeData.Instance.TrackDB;
                RoadTrackDB rdb = RuntimeData.Instance.RoadTrackDB;
                foreach (var trackNode in tdb.TrackNodes.VectorNodes.Where(
                    trackVectorNode => Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileX - camera.TileX) <= 1
                    && Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileZ - camera.TileZ) <= 1).Cast<TrackVectorNode>())
                {
                    var currentPosition = new Traveller(trackNode);
                    while (true)
                    {
                        var previousLocation = currentPosition.WorldLocation;
                        var remaining = currentPosition.MoveInSection(DisplaySegmentLength);
                        if ((Math.Abs(remaining - DisplaySegmentLength) < Tolerance) && !currentPosition.NextVectorSection())
                            break;
                        primitives.Add(new DispatcherLineSegment(previousLocation, currentPosition.WorldLocation, Color.LightBlue, 2));
                    }
                }
                if (rdb != null && rdb.TrackNodes != null)
                {
                    foreach (var trackNode in rdb.TrackNodes.Where(
                        tn => tn is TrackVectorNode trackVectorNode 
                        && Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileX - camera.TileX) <= 1 
                        && Math.Abs(trackVectorNode.TrackVectorSections[0].Location.TileZ - camera.TileZ) <= 1).Cast<TrackVectorNode>())
                    {
                        var currentPosition = new Traveller(trackNode, true);
                        while (true)
                        {
                            var previousLocation = currentPosition.WorldLocation;
                            var remaining = currentPosition.MoveInSection(DisplaySegmentLength);
                            if ((Math.Abs(remaining - DisplaySegmentLength) < Tolerance) && !currentPosition.NextVectorSection())
                                break;
                            primitives.Add(new DispatcherLineSegment(previousLocation, currentPosition.WorldLocation, Color.LightSalmon, 2));
                        }
                    }
                }
                Primitives = primitives;
            }

            var labels = new List<Rectangle>();
            foreach (var primitive in Primitives)
                primitive.PrepareFrame(labels, Viewport, Owner.Viewer.Camera);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            foreach (var line in Primitives)
                line.Draw(spriteBatch);
        }
    }
}
