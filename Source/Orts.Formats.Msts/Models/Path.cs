
using System.Collections.Generic;

using Orts.Common.Position;
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

        public PathFlags PathFlags { get; private set; }
        public int NextMainNode { get; private set; }
        public int NextSidingNode { get; private set; }

        public int WaitTime { get; }

        public ref readonly WorldLocation Location => ref location;

        #region Properties
        //Note : these flags are not understood in all detail
        public bool Junction => junctionFlag == 2;
        public bool Valid => (invalidFlag & 0b1000) != 0b1000;  //When bit 3 is set for flag2 (so 8, 9, 12, 13), it seems to denote a broken (or perhaps unfinished) path. Perhaps route was changed afterwards.
        #endregion


        internal PathNode(STFReader stf, List<PathDataPoint> pathDataPoints)
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
            uint pathFlags = stf.ReadHex(0);
            WaitTime = (int)(pathFlags >> 16);
            PathFlags = (PathFlags)(pathFlags & 0xFFFF);
            NextMainNode = (int)stf.ReadUInt(null);
            NextSidingNode = (int)stf.ReadUInt(null);
            int pathDataPoint = (int)stf.ReadUInt(null);
            stf.SkipRestOfBlock();

            location = pathDataPoints[pathDataPoint].Location;
            junctionFlag = pathDataPoints[pathDataPoint].JunctionFlag;
            invalidFlag = pathDataPoints[pathDataPoint].InvalidFlag;
        }
    }
}
