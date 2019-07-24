// COPYRIGHT 2014, 2015 by the Open Rails project.
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

using Microsoft.Xna.Framework;
using Orts.Formats.Msts;
using System;
using System.Drawing;

namespace Orts.Formats.OR
{
    public class AESegment
    {
        public PointF startPoint { get; protected set; }
        public PointF endPoint { get; protected set; }
        public PointF center { get; protected set; }
        public double lengthSegment { get; protected set; }
        public bool isCurved = false;
        public float radius { get; protected set; }
        public int step { get; protected set; }
        public double angleTot { get; protected set; }
        public double startAngle { get; protected set; }

        public AESegment(PointF start, PointF end)
        {
            startPoint = start;
            endPoint = end;
            step = 0;
            lengthSegment = 0;
        }

        public AESegment(AESegment original)
        {
            startPoint = original.startPoint;
            endPoint = original.endPoint;
            isCurved = original.isCurved;
            if (isCurved)
            {
                radius = original.radius;
                center = original.center;
                step = original.step;
                angleTot = original.angleTot;
                startAngle = original.startAngle;
                lengthSegment = original.lengthSegment;
            }
            else
            {
                step = 0;
                lengthSegment = original.lengthSegment;
            }
        }

        public AESegment(TrackSegment track)
        {
            startPoint = track.associateSegment.startPoint;
            endPoint = track.associateSegment.endPoint;
            isCurved = track.associateSegment.isCurved;
            if (isCurved)
            {
                radius = track.associateSegment.radius;
                center = track.associateSegment.center;
                step = track.associateSegment.step;
                angleTot = track.associateSegment.angleTot;
                startAngle = track.associateSegment.startAngle;
                lengthSegment = 0;
            }
        }
    }
}
