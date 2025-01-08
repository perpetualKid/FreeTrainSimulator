// COPYRIGHT 2012, 2013 by the Open Rails project.
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

using System;

using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;

namespace Orts.Formats.Msts
{
    /// <summary>
    /// Helper class to store details of a possible candidate where we can place the traveller.
    /// Used during initialization as part of constructer(s)
    /// </summary>
    internal class TrackNodeCandidate
    {
        // If a car has some overhang, than it will be offset toward the center of curvature
        // and won't be right along the center line.  I'll have to add some allowance for this
        // and accept a hit if it is within 2.5 meters of the center line - this was determined
        // experimentally to match MSTS's 'capture range'.
        private const float MaximumCenterlineOffset = 2.5f;

        // Maximum distance beyond the ends of the track we'll allow for initialization.
        private const float InitErrorMargin = 0.5f;

        public float Longitude { get; }               // longitude along the section
        public float DistanceToTrack { get; }   // lateral distance to the track
        public TrackNode TrackNode { get; private set; }     // the trackNode object
        public TrackVectorSection TrackVectorSection { get; private set; } // the trackvectorSection within the tracknode
        public int TrackVectorSectionIndex { get; private set; } = -1;   // the corresponding index of the trackvectorsection
        public TrackSection TrackSection { get; }          // the tracksection within the trackvectorsection

        /// <summary>
        /// Constructor will only be called deep into a section, where the actual lon(gitude) and lat(itude) are being calculated.
        /// </summary>
        private TrackNodeCandidate(float distanceToTrack, float lon, TrackSection trackSection)
        {
            Longitude = lon;
            DistanceToTrack = distanceToTrack;
            TrackSection = trackSection;
        }

        /// <summary>
        /// Try whether the given location is indeed on (or at least close to) the tracknode given by its index.
        /// If it is, we return a TrackNodeCandidate object. 
        /// </summary>
        /// <param name="trackNode">The tracknode we are testing</param>
        /// <param name="location">The location for which we want to see if it is on the tracksection</param>
        /// <returns>Details on where exactly the location is on the track.</returns>
        internal static TrackNodeCandidate TryTrackNode(TrackNode trackNode, in WorldLocation location)
        {
            if (!(trackNode is TrackVectorNode trackVectorNode))
                return null;
            // TODO, we could do an additional cull here by calculating a bounding sphere for each node as they are being read.
            for (int tvsi = 0; tvsi < trackVectorNode.TrackVectorSections.Length; tvsi++)
            {
                TrackNodeCandidate candidate = TryTrackVectorSection(tvsi, location, trackVectorNode);
                if (candidate != null)
                {
                    candidate.TrackNode = trackVectorNode;
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// Try whether the given location is indeed on (or at least close to) the trackvectorsection given by its index.
        /// If it is, we return a TrackNodeCandidate object. 
        /// </summary>
        /// <param name="tvsi">The index of the trackvectorsection</param>
        /// <param name="location">The location for which we want to see if it is on the tracksection</param>
        /// <param name="trackNode">The parent trackNode of the vector section</param>
        /// <returns>Details on where exactly the location is on the track.</returns>
        private static TrackNodeCandidate TryTrackVectorSection(int tvsi, in WorldLocation location, TrackVectorNode trackNode)
        {
            TrackVectorSection trackVectorSection = trackNode.TrackVectorSections[tvsi];

            TrackSection trackSection = RuntimeData.Instance.TSectionDat.TrackSections.TryGet(trackVectorSection.SectionIndex);
            if (trackSection == null)
                return null;

            TrackNodeCandidate candidate = TryTrackSection(location, trackVectorSection, trackSection);
            if (candidate == null)
                return null;

            candidate.TrackVectorSectionIndex = tvsi;
            candidate.TrackVectorSection = trackVectorSection;
            return candidate;
        }

        /// <summary>
        /// Try whether the given location is indeed on (or at least close to) the tracksection given by its index.
        /// If it is, we return a TrackNodeCandidate object. 
        /// </summary>
        /// <param name="location">The location for which we want to see if it is on the tracksection</param>
        /// <param name="trackVectorSection">The parent track vector section</param>
        /// <returns>Details on where exactly the location is on the track.</returns>
        internal static TrackNodeCandidate TryTrackSection(in WorldLocation location, TrackVectorSection trackVectorSection, TrackSection trackSection)
        {
            if (trackSection == null || trackVectorSection == null)
                return null;

            return trackSection.Curved ? TryTrackSectionCurved(location, trackVectorSection, trackSection) : TryTrackSectionStraight(location, trackVectorSection, trackSection);
        }

        /// <summary>
        /// Try whether the given location is indeed on (or at least close to) the given curved tracksection.
        /// If it is, we return a TrackNodeCandidate object 
        /// </summary>
        /// <param name="location">The location we are looking for</param>
        /// <param name="trackVectorSection">The trackvector section that is parent of the tracksection</param>
        /// <param name="trackSection">the specific tracksection we want to try</param>
        /// <returns>Details on where exactly the location is on the track.</returns>
        private static TrackNodeCandidate TryTrackSectionCurved(in WorldLocation location, TrackVectorSection trackVectorSection, TrackSection trackSection)
        {
            // TODO: Consider adding y component.
            // We're working relative to the track section, so offset as needed.
            Vector3 l = location.Location + (location.Tile - trackVectorSection.Location.Tile).TileVector();
            float sx = trackVectorSection.Location.Location.X;
            float sz = trackVectorSection.Location.Location.Z;

            // Do a preliminary cull based on a bounding square around the track section.
            // Bounding distance is (radius * angle + error) by (radius * angle + error) around starting coordinates but no more than 2 for angle.
            float boundingDistance = trackSection.Radius * Math.Min(Math.Abs(MathHelper.ToRadians(trackSection.Angle)), 2) + MaximumCenterlineOffset;
            float dx = Math.Abs(l.X - sx);
            float dz = Math.Abs(l.Z - sz);
            if (dx > boundingDistance || dz > boundingDistance)
                return null;

            // To simplify the math, center around the start of the track section, rotate such that the track section starts out pointing north (+z) and flip so the track curves to the right.
            l.X -= sx;
            l.Z -= sz;
            l = Vector3.Transform(l, Matrix.CreateRotationY(-trackVectorSection.Direction.Y));
            if (trackSection.Angle < 0)
                l.X *= -1;

            // Compute distance to curve's center at (radius,0) then adjust to get distance from centerline.
            dx = l.X - trackSection.Radius;
            float lat = (float)Math.Sqrt(dx * dx + l.Z * l.Z) - trackSection.Radius;
            if (Math.Abs(lat) > MaximumCenterlineOffset)
                return null;

            // Compute distance along curve (ensure we are in the top right quadrant, otherwise our math goes wrong).
            if (l.Z < -InitErrorMargin || l.X > trackSection.Radius + InitErrorMargin || l.Z > trackSection.Radius + InitErrorMargin)
                return null;
            float radiansAlongCurve;
            if (l.Z > trackSection.Radius)
                radiansAlongCurve = MathHelper.PiOver2;
            else
                radiansAlongCurve = (float)Math.Asin(l.Z / trackSection.Radius);
            float lon = radiansAlongCurve * trackSection.Radius;
            if (lon < -InitErrorMargin || lon > trackSection.Length + InitErrorMargin)
                return null;

            return new TrackNodeCandidate(Math.Abs(lat), lon, trackSection);
        }

        /// <summary>
        /// Try whether the given location is indeed on (or at least close to) the given straight tracksection.
        /// If it is, we return a TrackNodeCandidate object 
        /// </summary>
        /// <param name="location">The location we are looking for</param>
        /// <param name="trackVectorSection">The trackvector section that is parent of the tracksection</param>
        /// <param name="trackSection">the specific tracksection we want to try</param>
        /// <returns>Details on where exactly the location is on the track.</returns>
        private static TrackNodeCandidate TryTrackSectionStraight(in WorldLocation location, TrackVectorSection trackVectorSection, TrackSection trackSection)
        {
            // TODO: Consider adding y component.
            // We're working relative to the track section, so offset as needed.
            Vector3 l = location.Location + (location.Tile - trackVectorSection.Location.Tile).TileVector();
            float sx = trackVectorSection.Location.Location.X;
            float sz = trackVectorSection.Location.Location.Z;

            // Do a preliminary cull based on a bounding square around the track section.
            // Bounding distance is (length + error) by (length + error) around starting coordinates.
            float boundingDistance = trackSection.Length + MaximumCenterlineOffset;
            float dx = Math.Abs(l.X - sx);
            float dz = Math.Abs(l.Z - sz);

            if (dx > boundingDistance || dz > boundingDistance)
                return null;

            // Calculate distance along and away from the track centerline.
            float lat, lon;
            (lat, lon) = EarthCoordinates.Survey(sx, sz, trackVectorSection.Direction.Y, l.X, l.Z);
            if (Math.Abs(lat) > MaximumCenterlineOffset)
                return null;
            if (lon < -InitErrorMargin || lon > trackSection.Length + InitErrorMargin)
                return null;

            return new TrackNodeCandidate(Math.Abs(lat), lon, trackSection);
        }
    }
}
