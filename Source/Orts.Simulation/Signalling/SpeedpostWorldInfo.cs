using System.IO;

using Orts.Formats.Msts.Models;

namespace Orts.Simulation.Signalling
{
    public class SpeedpostWorldInfo
    {
        public string SpeedPostFileName { get; }

        public SpeedpostWorldInfo(SpeedPostObject speedPostItem)
        {
            // get filename in Uppercase
            SpeedPostFileName = Path.GetFileName(speedPostItem?.FileName).ToUpperInvariant();
        }

        public SpeedpostWorldInfo(SpeedpostWorldInfo source)
        {
            SpeedPostFileName = source?.SpeedPostFileName;
        }
    }
}
