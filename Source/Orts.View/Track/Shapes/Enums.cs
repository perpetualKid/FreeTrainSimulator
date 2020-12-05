using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.View.Track.Shapes
{
    public enum BasicTextureType
    { 
        BlankPixel,
        Circle, 
        Disc,
        Ring,
        CrossedRing,
        // next ones are used to map Resources, hence names should match exactly (case insensitive) the resource file name
        ActiveBrokenNode,
        ActiveNode,
        ActiveNormalNode,
        ActiveTrackNode,
        CarSpawner,
        Hazard,
        PathEnd,
        PathNormal,
        PathReverse,
        PathStart,
        PathWait,
        Pickup,
        Platform,
        Signal,
        Sound,
        PlayerTrain,
        //
    }
}
