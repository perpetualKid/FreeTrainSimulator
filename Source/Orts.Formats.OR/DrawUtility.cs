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

using Orts.Formats.Msts;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Orts.Formats.OR
{
    public static class DrawUtility
    {
        // Calculate the distance between
        // point pt and the segment startPoint --> middlePoint.

        public static double FindDistancePoints(PointF point1, PointF point2)
        {
            double somme = Math.Pow((point2.X - point1.X), 2) + Math.Pow((point2.Y - point1.Y), 2);
            somme = Math.Sqrt(somme);

            return somme;
        }

        public static PointF FindIntersection(AESegment segArea, AESegment track)
        {
            PointF intersection;
            if (track.isCurved)
            {
                if ((int)(track.startPoint.X) == 45892)
                    intersection = PointF.Empty;
                intersection = DrawUtility.FindCurveIntersection(segArea, track);
            }
            else
            {
                intersection = DrawUtility.FindStraightIntersection(segArea, track);
            }
            return intersection;
        }

        //  public domain function by Darel Rex Finley, 2006
        //  Determines the intersection point of the line segment defined by points A and B
        //  with the line segment defined by points C and D.
        //
        //  Returns YES if the intersection point was found, and stores that point in X,Y.
        //  Returns NO if there is no determinable intersection point, in which case X,Y will
        //  be unmodified.

        public static PointF FindStraightIntersection(AESegment segArea, AESegment track)
        {
            PointF pt = PointF.Empty;
            double  distAB, theCos, theSin, newX, ABpos ;
            double distCD, theCos2, theSin2;
            double ABX, ABY, ACX, ACY, ADX, ADY;
            double AX = segArea.startPoint.X;
            double AY = segArea.startPoint.Y;
            double CDX, CDY, angle1, angle2;

            //  Fail if either line segment is zero-length.
            if ((segArea.startPoint.X == segArea.endPoint.X && segArea.startPoint.Y == segArea.endPoint.Y) 
                || (track.startPoint.X == track.endPoint.X && track.startPoint.Y == track.endPoint.Y))
                return pt;

            //  Fail if the segments share an end-point.
            if ((segArea.startPoint.X == track.startPoint.X && segArea.startPoint.Y == track.startPoint.Y) ||
                (segArea.endPoint.X == track.startPoint.X && segArea.endPoint.Y == track.startPoint.Y) ||
                (segArea.startPoint.X == track.endPoint.X && segArea.startPoint.Y == track.endPoint.Y) ||
                (segArea.endPoint.X == track.endPoint.X && segArea.endPoint.Y == track.endPoint.Y))
            {
                return pt; 
            }

            //  (1) Translate the system so that point A is on the origin.
            ABX = segArea.endPoint.X - segArea.startPoint.X;
            ABY = segArea.endPoint.Y - segArea.startPoint.Y;
            ACX = track.startPoint.X - segArea.startPoint.X;
            ACY = track.startPoint.Y - segArea.startPoint.Y;
            ADX = track.endPoint.X - segArea.startPoint.X;
            ADY = track.endPoint.Y - segArea.startPoint.Y;
            CDX = track.endPoint.X - track.startPoint.X;
            CDY = track.endPoint.Y - track.startPoint.Y;

            //  Discover the length of segment A-B.
            distAB = Math.Sqrt(ABX * ABX + ABY * ABY);
            distCD = Math.Sqrt(CDX * CDX + CDY * CDY);  // sup

            //  (2) Rotate the system so that point B is on the positive X axis.
            theCos = ABX / distAB;
            theSin = ABY / distAB;

            theCos2 = (CDX) / distCD;   // sup
            theSin2 = (CDY) / distCD;   // sup
            angle1 = Math.Acos(theCos) * 180 / Math.PI;   // sup
            angle2 = Math.Acos(theCos2) * 180 / Math.PI;   // sup
            newX = ACX * theCos + ACY * theSin;
            ACY  = ACY * theCos - ACX * theSin; 
            ACX = newX;
            newX = ADX * theCos + ADY * theSin;
            ADY  = ADY * theCos - ADX * theSin; 
            ADX = newX;

            if (Math.Abs(angle1 - angle2) < 5)   // sup
                return pt;   // sup
            //  Fail if segment C-D doesn't cross line A-B.
            if (ACY < 0 && ADY < 0 || ACY >= 0 && ADY >= 0) 
                return pt;

            //  (3) Discover the position of the intersection point along line A-B.
            ABpos = ADX + (ACX - ADX) * ADY / (ADY - ACY);

            //  Fail if segment C-D crosses line A-B outside of segment A-B.
            if (ABpos < 0 || ABpos > distAB) 
                return pt;

            //  (4) Apply the discovered position to line A-B in the original coordinate system.
            pt.X = (float)(AX + ABpos * theCos);
            pt.Y = (float)(AY + ABpos * theSin);
            if (FindDistancePoints(pt, segArea.startPoint) < 0.1 || FindDistancePoints(pt, segArea.endPoint) < 0.1)
                return PointF.Empty;
            //  Success.
            return pt;
        }

        public static PointF FindCurveIntersection(AESegment segArea, AESegment track)
        {
            PointF pointA = track.startPoint;
            PointF pointB = track.endPoint;
            AESegment partTrack;
            PointF intersect;

            for (int i = 1; i < track.step; i++)
            {
                double sub_angle = (i) * track.angleTot / track.step;
                double info2x = track.radius * Math.Cos(track.startAngle + sub_angle);
                double info2y = track.radius * Math.Sin(track.startAngle + sub_angle);
                double dx = (track.center.X + info2x);
                double dy = (track.center.Y + info2y);

                pointB = new PointF((float)dx, (float)dy);
                partTrack = new AESegment(pointA, pointB);
                intersect = FindStraightIntersection(segArea, partTrack);
                if (intersect != PointF.Empty)
                {
                    return intersect;
                }
                pointA = pointB;
            }
            pointB = track.endPoint;
            partTrack = new AESegment(pointA, pointB);
            intersect = FindStraightIntersection(segArea, partTrack);
            if (intersect != PointF.Empty)
            {
                return intersect;
            }

            return PointF.Empty;
        }

        public static bool PointInPolygon(PointF point, List<System.Drawing.PointF> polyPoints)
        {
            var j = polyPoints.Count - 1;
            var oddNodes = false;

            for (var i = 0; i < polyPoints.Count; i++)
            {
                if (polyPoints[i].Y < point.Y && polyPoints[j].Y >= point.Y ||
                    polyPoints[j].Y < point.Y && polyPoints[i].Y >= point.Y)
                {
                    if (polyPoints[i].X +
                        (point.Y - polyPoints[i].Y) / (polyPoints[j].Y - polyPoints[i].Y) * (polyPoints[j].X - polyPoints[i].X) < point.X)
                    {
                        oddNodes = !oddNodes;
                    }
                }
                j = i;
            }

            return oddNodes;
        }

    }
}