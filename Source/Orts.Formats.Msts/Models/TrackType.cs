using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Entities
{
    public class TrackType
    {
        public string Label { get; private set; }
        public string InsideSound { get; private set; }
        public string OutsideSound { get; private set; }

        public TrackType(STFReader stf)
        {
            stf.MustMatch("(");
            Label = stf.ReadString();
            InsideSound = stf.ReadString();
            OutsideSound = stf.ReadString();
            stf.SkipRestOfBlock();
        }
    } // TrackType
}
