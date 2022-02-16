
using Orts.Common.Position;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class PathDataPoint
    {
        private readonly WorldLocation location;
        public ref readonly WorldLocation Location => ref location;
        public int JunctionFlag { get; private set; }
        public int InvalidFlag { get; private set; }

        #region Properties
        //Note : these flags are not understood in all detail
        public bool IsJunction => JunctionFlag == 2;
        public bool IsInvalid => InvalidFlag == 9;  //TODO: probably also 12 is invalid.
        #endregion

        internal PathDataPoint(STFReader stf)
        {
            stf.MustMatchBlockStart();
            location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                stf.ReadFloat(STFReader.Units.None, null), stf.ReadFloat(STFReader.Units.None, null), stf.ReadFloat(STFReader.Units.None, null));
            JunctionFlag = stf.ReadInt(null);
            InvalidFlag = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }
    }

    // for an explanation, see class PATfile 
    public class PathNode
    {
        public PathFlags PathFlags { get; private set; }
        public int NextMainNode { get; private set; }
        public int NextSidingNode { get; private set; }
        public int PathDataPoint { get; private set; }

        public int WaitTime { get; }

        internal PathNode(STFReader stf)
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
            PathDataPoint = (int)stf.ReadUInt(null);
            stf.SkipRestOfBlock();
        }
    }
}
