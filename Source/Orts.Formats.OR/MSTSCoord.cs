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
using Orts.Common;
using System;
using System.Drawing;

namespace Orts.Formats.OR
{
    public class MSTSBase
    {
        public double TileX { get; set; }
        public double TileY { get; set; }

        public MSTSBase()
        {
            TileX = 0;
            TileY = 0;
        }
        public MSTSBase(TrackDatabaseFile TDB)
        {
            double minTileX = double.PositiveInfinity;
            double minTileY = double.PositiveInfinity;

            TrackNode[] nodes = TDB.TrackDB.TrackNodes;
            for (int nodeIdx = 0; nodeIdx < nodes.Length; nodeIdx++)
            {
                if (nodes[nodeIdx] == null)
                    continue;
                TrackNode currNode = nodes[nodeIdx];
                if (currNode.TrVectorNode != null && currNode.TrVectorNode.TrVectorSections != null)
                {
                    if (currNode.TrVectorNode.TrVectorSections.Length > 1)
                    {
                        foreach (TrPin pin in currNode.TrPins)
                        {

                            if (minTileX > nodes[pin.Link].UiD.TileX)
                                minTileX = nodes[pin.Link].UiD.TileX;
                            if (minTileY > nodes[pin.Link].UiD.TileZ)
                                minTileY = nodes[pin.Link].UiD.TileZ;
                        }
                    }
                    else
                    {
                        TrVectorSection s;
                        s = currNode.TrVectorNode.TrVectorSections[0];
                        if (minTileX > s.TileX)
                            minTileX = s.TileX;
                        if (minTileY > s.TileZ)
                            minTileY = s.TileZ;
                    }
                }
                else if (currNode.TrJunctionNode != null)
                {
                    if (minTileX > currNode.UiD.TileX)
                        minTileX = currNode.UiD.TileX;
                    if (minTileY > currNode.UiD.TileZ)
                        minTileY = currNode.UiD.TileZ;
                }
            }
            TileX = minTileX;
            TileY = minTileY;
        }

        public void reduce(TrackDatabaseFile TDB)
        {
            TrackNode[] nodes = TDB.TrackDB.TrackNodes;
            for (int nodeIdx = 0; nodeIdx < nodes.Length; nodeIdx++)
            {
                if (nodes[nodeIdx] == null)
                    continue;
                ((TrackNode)TDB.TrackDB.TrackNodes[nodeIdx]).reduce(TileX, TileY);
            }
            if (TDB.TrackDB.TrItemTable == null)
                return;
            foreach (var item in TDB.TrackDB.TrItemTable)
            {
                item.TileX -= (int)TileX;
                item.TileZ -= (int)TileY;
            }
        }
    }

    public readonly struct MSTSCoord: IEquatable<MSTSCoord>

    {
        public readonly float TileX; 
        public readonly float TileY;
        public readonly float X;
        public readonly float Y;
        private readonly bool reduced;

        public MSTSCoord(float tileX, float tileY, float x, float y, bool reduced = false)
        {
            TileX = tileX;
            TileY = tileY;
            X = x;
            Y = y;
            this.reduced = reduced;
        }

        public MSTSCoord(MSTSCoord coord): this(coord.TileX, coord.TileY, coord.X, coord.Y, coord.reduced)
        {
        }

        public MSTSCoord(in WorldLocation location): this(location.TileX, location.TileZ, location.Location.X, location.Location.Z, true)
        {
        }

        public MSTSCoord(TrVectorSection section): this(section.TileX, section.TileZ, section.X, section.Z, section.Reduced)
        {
        }

        public MSTSCoord(TrVectorSection section, bool reduced) : this(section.TileX, section.TileZ, section.X, section.Z, reduced)
        {
        }

        public MSTSCoord(TrackNode node): this(node.UiD.TileX, node.UiD.TileZ, node.UiD.X, node.UiD.Z, node.Reduced)
        {
        }

        public MSTSCoord(TrackNode node, bool reduced) : this(node.UiD.TileX, node.UiD.TileZ, node.UiD.X, node.UiD.Z, reduced)
        {
        }

        public MSTSCoord(PointF point)
        {
            point.X += 1024f;
            point.Y += 1024f;
            TileX = ((int)(point.X / 2048f));
            TileY = ((int)(point.Y / 2048f));
            X = ((point.X) % 2048f);
            Y = ((point.Y) % 2048f);

            if (point.X < 0) 
            {
                TileX -= 1;
                X += 2048f;
            }
            if (point.Y < 0)
            {
                TileY -= 1;
                Y += 2048f;
            }
            X -= 1024f;
            Y -= 1024F;
            reduced = true;
        }

        public MSTSCoord Unreduce(MSTSBase tileBase)
        {
            return (reduced ? new MSTSCoord(TileX + (int)tileBase.TileX, TileY + (int)tileBase.TileY, X, Y, false) : new MSTSCoord(TileX, TileY, X, Y, false));
        }

        public MSTSCoord Reduce(MSTSBase tileBase)
        {
            return (reduced ? new MSTSCoord(TileX, TileY, X, Y, true) : new MSTSCoord( TileX - (int)tileBase.TileX, TileY - (int)tileBase.TileY, X, Y, true));
        }

        // Equality operator. test if the coordinates are at the same point.
        public override bool Equals(object obj)
        {

            if (!(obj is MSTSCoord other)) // type pattern here
                return false;
            return Equals(other);
        }


        public bool Equals(MSTSCoord other)
        {
            return (this.X == other.X && this.Y == other.Y && this.TileX == other.TileX && this.TileY == other.TileY);
        }

        public static bool operator ==(MSTSCoord x, MSTSCoord y)
        {
            return x.Equals(y);  
        }

        public static bool operator !=(MSTSCoord x, MSTSCoord y)
        {
            return !x.Equals(y);
        }

        public static bool Near(in MSTSCoord x, in MSTSCoord y)
        {
            float squareA = (float)Math.Pow((x.X - y.X), 2);
            float squareB = (float)Math.Pow((x.Y - y.Y), 2);
            float AX = (float)Math.Round((double)(x.X - y.Y), 2, MidpointRounding.ToEven);
            float AY = (float)Math.Round((double)x.Y, 2, MidpointRounding.ToEven);
            float BX = (float)Math.Round((double)y.X, 2, MidpointRounding.ToEven);
            float BY = (float)Math.Round((double)y.Y, 2, MidpointRounding.ToEven);

            if ((float)Math.Round((double)(squareA + squareB)) < 0.1f && x.TileX == y.TileX && x.TileY == y.TileY)
                return true;
            return false;
        }

        public override int GetHashCode()
        {   // based on http://stackoverflow.com/questions/5221396/what-is-an-appropriate-gethashcode-algorithm-for-a-2d-point-struct-avoiding
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + TileX.GetHashCode();
                hash = hash * 23 + X.GetHashCode();
                hash = hash * 23 + TileY.GetHashCode();
                hash = hash * 23 + Y.GetHashCode();
                return hash;
            }
        }

        public PointF ConvertToPointF()
        {
            return new PointF((TileX * 2048f + X), (TileY * 2048f + Y));
        }

        public Vector2 ConvertVector2()
        {
            return new Vector2((float)((TileX * 2048f) + X), (float)((TileY * 2048f) + Y));
        }


        public override string ToString()
        {
            return $"({(int)(TileX * 2048f):d + X)},{(int)((TileY * 2048f) + Y):d})";
        }
    }

}
