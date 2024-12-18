using Orts.Formats.Msts.Parsers;
using Orts.Formats.OpenRails.Models;

namespace Orts.Formats.OpenRails.Files
{
    public class ClockFile
    {
        public ClockList Clocks { get; private set; }

        public ClockFile(string fileName, string shapePath)
        {
            using (STFReader stf = new STFReader(fileName, false))
            {
                Clocks = new ClockList(stf, shapePath);
            }
        }
    }

}
