using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    internal class PathDataPoint
    {
        internal readonly WorldLocation Location;
        internal int JunctionFlag;
        internal int InvalidFlag;

        internal PathDataPoint(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                stf.ReadFloat(STFReader.Units.None, null), stf.ReadFloat(STFReader.Units.None, null), stf.ReadFloat(STFReader.Units.None, null));
            JunctionFlag = stf.ReadInt(null);
            InvalidFlag = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }
    }

    // for an explanation, see class PATfile 
    public class PathNode
    {
        private readonly WorldLocation location;
        private readonly int junctionFlag;
        private readonly int invalidFlag;

        public int NextMainNode { get; private set; }
        public int NextSidingNode { get; private set; }

        public int WaitTime { get; }

        public ref readonly WorldLocation Location => ref location;

        public bool Junction => junctionFlag == 2;
        public bool Invalid => (invalidFlag & 0b1000) == 0b1000;  //When bit 3 is set for flag2 (so 8, 9, 12, 13), it seems to denote a broken (or perhaps unfinished) path. Perhaps route was changed afterwards.

        public PathNodeType NodeType { get; }

        internal PathNode(STFReader stf, List<PathDataPoint> pathDataPoints, bool firstNode = false)
        {
            // Possible interpretation (as found on internet, by krausyao)
            // TrPathNode ( AAAABBBB mainIdx passingIdx pdpIdx )
            // AAAA wait time seconds in hexidecimal
            // BBBB (Also hexidecimal, so 16 bits)
            // Bit 0 - connected pdp-entry references a reversal-point (1/x1)
            // Bit 1 - waiting point (2/x2)
            // Bit 2 - intermediate point between switches (4/x4)
            // Bit 3 - 'other exit' is used (8/x8)
            // Bit 4 - 'optional Route' active (16/x10)
            stf.MustMatchBlockStart();
            uint fullPathFlags = stf.ReadHex(0);
            WaitTime = (int)(fullPathFlags >> 16);
            PathFlags pathFlags = (PathFlags)(fullPathFlags & 0xFFFF);
            NextMainNode = (int)stf.ReadUInt(null);
            NextSidingNode = (int)stf.ReadUInt(null);
            int pathDataPoint = (int)stf.ReadUInt(null);
            stf.SkipRestOfBlock();

            location = pathDataPoints[pathDataPoint].Location;
            junctionFlag = pathDataPoints[pathDataPoint].JunctionFlag;
            invalidFlag = pathDataPoints[pathDataPoint].InvalidFlag;

            if (firstNode)
            {
                NodeType = PathNodeType.Start;
            }
            else if (NextMainNode == -1  && NextSidingNode == -1)
            {
                NodeType = PathNodeType.End;
            }
            // if bit 0 is set: reversal
            else if ((pathFlags & PathFlags.ReversalPoint) == PathFlags.ReversalPoint)
            {
                NodeType = PathNodeType.Reversal;
            }
            // bit 0 is not set, but bit 1 is set:waiting point
            else if ((pathFlags & PathFlags.WaitPoint) == PathFlags.WaitPoint)
            {
                NodeType = PathNodeType.Wait;
            }
            else if ((pathFlags & PathFlags.IntermediatePoint) == PathFlags.IntermediatePoint)
            {
                NodeType = PathNodeType.Intermediate;
            }
        }
    }
}
